using System.Net;
using System.Net.Http.Json;
using Backend.IntegrationTests.Infraestructura;

namespace Backend.IntegrationTests.Monitoreo;

/// <summary>
/// Mapa de ocupación contra la API real: totales y desglose por área con una sesión en uso, una
/// reserva pendiente de check-in, una reserva futura y un área sin puestos; más la autorización.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class MonitoreoEndpointsTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido de SQL Server.</summary>
    public MonitoreoEndpointsTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElMapaReflejaLaOcupacionYExigeRolAdministrativo()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("monitoreo_ocupacion");
        var reloj = new RelojPruebas();

        await using var fabrica = FabricaApiPruebas.CrearConReloj(cadena, reloj);

        using var administrador = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(administrador);

        var area = await SoporteApiPruebas.CrearAreaConPuestoAsync(administrador, "PC-01", aforo: 3);
        var segundo = await CrearPuestoAsync(administrador, area.EspacioId, "PC-02");
        var tercero = await CrearPuestoAsync(administrador, area.EspacioId, "PC-03");
        await CrearAreaVaciaAsync(administrador, "Depósito de equipos");

        await SoporteApiPruebas.ActivarPeriodoAsync(
            administrador,
            SoporteApiPruebas.FechaLocal(-30),
            SoporteApiPruebas.FechaLocal(60));

        using var titular = await SoporteApiPruebas.CrearEstudianteAsync(fabrica, administrador, "ana@uninpahu.edu.co");
        using var pendiente = await SoporteApiPruebas.CrearEstudianteAsync(fabrica, administrador, "luis@uninpahu.edu.co");
        using var futuro = await SoporteApiPruebas.CrearEstudianteAsync(fabrica, administrador, "marta@uninpahu.edu.co");

        var fecha = SoporteApiPruebas.FechaLocal(1);
        var inicio = SoporteApiPruebas.EnZona(fecha, 10);

        var enUso = await SoporteApiPruebas.ReservarAsync(titular, area.PuestoId, inicio);
        await SoporteApiPruebas.ReservarAsync(pendiente, segundo, inicio);
        await SoporteApiPruebas.ReservarAsync(futuro, tercero, SoporteApiPruebas.EnZona(fecha, 16));

        reloj.SituarEn(inicio.AddMinutes(5));

        var ingreso = await titular.PostAsJsonAsync($"/api/reservas/{enUso.Id}/check-in", new { codigoQr = area.CodigoQr });
        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);

        var mapa = await administrador.GetFromJsonAsync<MapaRespuesta>("/api/monitoreo/ocupacion-actual");
        Assert.NotNull(mapa);
        Assert.Equal(15, mapa.ToleranciaMinutos);
        Assert.Equal(inicio.AddMinutes(5), mapa.GeneradoEn);

        var laboratorio = Assert.Single(mapa.Areas, item => item.EspacioId == area.EspacioId);
        Assert.Equal(3, laboratorio.Aforo);
        Assert.Equal(3, laboratorio.PuestosActivos);
        Assert.Equal(1, laboratorio.EnUso);
        Assert.Equal(1, laboratorio.PendientesCheckIn);
        Assert.Equal(0, laboratorio.SesionesSinCerrar);
        Assert.Equal(1, laboratorio.Libres);
        Assert.Equal(33.33m, laboratorio.PorcentajeOcupacion);
        Assert.Equal("PC-01", Assert.Single(laboratorio.EnCurso).PuestoIdentificador);

        var deposito = Assert.Single(mapa.Areas, item => item.Nombre == "Depósito de equipos");
        Assert.Equal(0, deposito.PuestosActivos);
        Assert.Equal(0, deposito.Libres);
        Assert.Equal(0m, deposito.PorcentajeOcupacion);
        Assert.Empty(deposito.EnCurso);

        var prohibido = await titular.GetAsync("/api/monitoreo/ocupacion-actual");
        Assert.Equal(HttpStatusCode.Forbidden, prohibido.StatusCode);
        Assert.Equal("application/problem+json", prohibido.Content.Headers.ContentType?.MediaType);

        using var anonimo = fabrica.CreateClient();
        var sinSesion = await anonimo.GetAsync("/api/monitoreo/ocupacion-actual");
        Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);
    }

    private static async Task<Guid> CrearPuestoAsync(HttpClient administrador, Guid espacioId, string identificador)
    {
        var respuesta = await administrador.PostAsJsonAsync($"/api/espacios/{espacioId}/puestos", new
        {
            identificador,
            descripcionEquipo = "Equipo de pruebas"
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var documento = System.Text.Json.JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task CrearAreaVaciaAsync(HttpClient administrador, string nombre)
    {
        var respuesta = await administrador.PostAsJsonAsync("/api/espacios", new
        {
            nombre,
            ubicacion = "Bloque D",
            categoria = "SalaComputo",
            aforo = 2,
            tipoAprobacion = "Automatica",
            tipoModalidadPrestamo = "PuestoIndividual",
            tipoElementoPrestamo = "EquipoComputo"
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    private sealed record SesionRespuesta(Guid ReservaId, string PuestoIdentificador, string Titular);

    private sealed record AreaRespuesta(
        Guid EspacioId,
        string Nombre,
        int Aforo,
        int PuestosActivos,
        int EnUso,
        int PendientesCheckIn,
        int SesionesSinCerrar,
        int Libres,
        decimal PorcentajeOcupacion,
        IReadOnlyList<SesionRespuesta> EnCurso);

    private sealed record TotalesRespuesta(int PuestosActivos, int EnUso, int PendientesCheckIn, int Libres);

    private sealed record MapaRespuesta(
        DateTimeOffset GeneradoEn,
        int ToleranciaMinutos,
        TotalesRespuesta Totales,
        IReadOnlyList<AreaRespuesta> Areas);
}
