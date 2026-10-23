using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.IntegrationTests.Infraestructura;

namespace Backend.IntegrationTests.Calendario;

/// <summary>
/// Pruebas HTTP del calendario institucional: administración restringida, solapes, duplicados,
/// activación y consulta del calendario vigente.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class CalendarioEndpointsTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public CalendarioEndpointsTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElAdministrativoGestionaPeriodosYFranjasConSusConflictos()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("calendario_crud");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(cliente);

        var hoy = SoporteApiPruebas.Hoy();

        var alta = await cliente.PostAsJsonAsync("/api/periodos-academicos", new
        {
            nombre = "2026-2",
            fechaInicio = hoy.AddDays(-10),
            fechaFin = hoy.AddDays(100)
        });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        using var documento = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var periodoId = documento.RootElement.GetProperty("id").GetGuid();
        Assert.False(documento.RootElement.GetProperty("estadoActivo").GetBoolean());

        var solapado = await cliente.PostAsJsonAsync("/api/periodos-academicos", new
        {
            nombre = "2026-3",
            fechaInicio = hoy.AddDays(50),
            fechaFin = hoy.AddDays(150)
        });

        Assert.Equal(HttpStatusCode.Conflict, solapado.StatusCode);
        Assert.Contains("periodo-solapado", await solapado.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var invertido = await cliente.PostAsJsonAsync("/api/periodos-academicos", new
        {
            nombre = "2027-1",
            fechaInicio = hoy.AddDays(200),
            fechaFin = hoy.AddDays(100)
        });

        Assert.Equal(HttpStatusCode.BadRequest, invertido.StatusCode);
        Assert.Contains("fechaFin", await invertido.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var activar = await cliente.PutAsync($"/api/periodos-academicos/{periodoId}/activar", content: null);
        Assert.Equal(HttpStatusCode.OK, activar.StatusCode);

        var vigente = await cliente.GetAsync("/api/calendario/vigente");
        Assert.Equal(HttpStatusCode.OK, vigente.StatusCode);
        Assert.Contains("2026-2", await vigente.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var franja = await cliente.PostAsJsonAsync("/api/franjas-no-operativas", new
        {
            diaSemana = "Sunday",
            horaInicio = "00:00",
            horaFin = "23:59",
            esFeriado = false,
            motivo = "Cierre dominical"
        });

        Assert.Equal(HttpStatusCode.Created, franja.StatusCode);

        var duplicada = await cliente.PostAsJsonAsync("/api/franjas-no-operativas", new
        {
            diaSemana = "Sunday",
            horaInicio = "00:00",
            horaFin = "23:59",
            esFeriado = false,
            motivo = "Cierre dominical"
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicada.StatusCode);
        Assert.Contains("franja-duplicada", await duplicada.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var detalle = JsonDocument.Parse(await franja.Content.ReadAsStringAsync());
        var franjaId = detalle.RootElement.GetProperty("id").GetGuid();

        var eliminar = await cliente.DeleteAsync($"/api/franjas-no-operativas/{franjaId}");
        Assert.Equal(HttpStatusCode.NoContent, eliminar.StatusCode);

        using var anonimo = fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/calendario/vigente")).StatusCode);

        using var docente = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            cliente,
            "docente.calendario@uninpahu.edu.co",
            "Docente");

        var denegado = await docente.PostAsJsonAsync("/api/periodos-academicos", new
        {
            nombre = "2027-1",
            fechaInicio = hoy.AddDays(120),
            fechaFin = hoy.AddDays(200)
        });

        Assert.Equal(HttpStatusCode.Forbidden, denegado.StatusCode);
    }

    [DockerFact]
    public async Task LaActivacionConcurrenteDejaUnUnicoPeriodoActivo()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("calendario_activacion");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(cliente);

        var hoy = SoporteApiPruebas.Hoy();

        var primero = await cliente.PostAsJsonAsync("/api/periodos-academicos", new
        {
            nombre = "2026-1",
            fechaInicio = hoy.AddDays(-120),
            fechaFin = hoy.AddDays(-10)
        });

        var segundo = await cliente.PostAsJsonAsync("/api/periodos-academicos", new
        {
            nombre = "2026-2",
            fechaInicio = hoy.AddDays(-9),
            fechaFin = hoy.AddDays(120)
        });

        using var documentoPrimero = JsonDocument.Parse(await primero.Content.ReadAsStringAsync());
        using var documentoSegundo = JsonDocument.Parse(await segundo.Content.ReadAsStringAsync());

        var idPrimero = documentoPrimero.RootElement.GetProperty("id").GetGuid();
        var idSegundo = documentoSegundo.RootElement.GetProperty("id").GetGuid();

        var activaciones = await Task.WhenAll(
            cliente.PutAsync($"/api/periodos-academicos/{idPrimero}/activar", content: null),
            cliente.PutAsync($"/api/periodos-academicos/{idSegundo}/activar", content: null));

        Assert.All(activaciones, activacion => Assert.Equal(HttpStatusCode.OK, activacion.StatusCode));

        var vigente = await cliente.GetStringAsync("/api/calendario/vigente");
        using var calendario = JsonDocument.Parse(vigente);

        Assert.True(calendario.RootElement.GetProperty("tienePeriodoActivo").GetBoolean());

        using var periodos = JsonDocument.Parse(await cliente.GetStringAsync("/api/periodos-academicos"));
        var activos = periodos.RootElement.EnumerateArray()
            .Count(periodo => periodo.GetProperty("estadoActivo").GetBoolean());

        Assert.Equal(1, activos);
    }
}
