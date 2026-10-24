using System.Net;
using System.Net.Http.Json;
using Backend.IntegrationTests.Infraestructura;

namespace Backend.IntegrationTests.Asistencia;

/// <summary>
/// Rechazos del contrato de asistencia: 401 sin sesión, 400 sin código, 404 inexistente,
/// 403 reserva ajena y 409 con los slugs estables de ventana, código y transición.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class CheckInRechazosTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido de SQL Server.</summary>
    public CheckInRechazosTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElCheckInYElCheckOutRechazanLoQueNoCorrespondeConRfc7807()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("asistencia_rechazos");
        var reloj = new RelojPruebas();

        await using var fabrica = FabricaApiPruebas.CrearConReloj(cadena, reloj);

        using var administrador = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(administrador);
        var puesto = await SoporteApiPruebas.CrearAreaConPuestoAsync(administrador, "PC-07");
        var otroPuesto = await SoporteApiPruebas.CrearAreaConPuestoAsync(administrador, "PC-08");
        await SoporteApiPruebas.ActivarPeriodoAsync(
            administrador,
            SoporteApiPruebas.FechaLocal(-30),
            SoporteApiPruebas.FechaLocal(60));

        using var titular = await SoporteApiPruebas.CrearEstudianteAsync(fabrica, administrador, "ana@uninpahu.edu.co");
        using var otro = await SoporteApiPruebas.CrearEstudianteAsync(fabrica, administrador, "luis@uninpahu.edu.co");

        var fecha = SoporteApiPruebas.FechaLocal(1);
        var inicio = SoporteApiPruebas.EnZona(fecha, 10);
        var reserva = await SoporteApiPruebas.ReservarAsync(titular, puesto.PuestoId, inicio);

        using var anonimo = fabrica.CreateClient();
        var sinSesion = await anonimo.PostAsJsonAsync($"/api/reservas/{reserva.Id}/check-in", new { codigoQr = puesto.CodigoQr });
        Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);

        var sinCodigo = await titular.PostAsJsonAsync($"/api/reservas/{reserva.Id}/check-in", new { });
        Assert.Equal(HttpStatusCode.BadRequest, sinCodigo.StatusCode);
        Assert.Contains("codigoQr", await sinCodigo.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var inexistente = await titular.PostAsJsonAsync($"/api/reservas/{Guid.NewGuid()}/check-in", new { codigoQr = puesto.CodigoQr });
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);

        var ajena = await otro.PostAsJsonAsync($"/api/reservas/{reserva.Id}/check-in", new { codigoQr = puesto.CodigoQr });
        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);
        Assert.Contains("reserva-ajena", await ajena.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        reloj.SituarEn(inicio.AddMinutes(-16));

        var fueraDeVentana = await titular.PostAsJsonAsync($"/api/reservas/{reserva.Id}/check-in", new { codigoQr = puesto.CodigoQr });
        Assert.Equal(HttpStatusCode.Conflict, fueraDeVentana.StatusCode);
        Assert.Equal("application/problem+json", fueraDeVentana.Content.Headers.ContentType?.MediaType);

        var problemaVentana = await fueraDeVentana.Content.ReadAsStringAsync();
        Assert.Contains("check-in-fuera-de-ventana", problemaVentana, StringComparison.Ordinal);
        Assert.Contains("09:45", problemaVentana, StringComparison.Ordinal);
        Assert.Contains("10:15", problemaVentana, StringComparison.Ordinal);
        Assert.Contains("traceId", problemaVentana, StringComparison.Ordinal);

        reloj.SituarEn(inicio.AddMinutes(2));

        var codigoAjeno = await titular.PostAsJsonAsync($"/api/reservas/{reserva.Id}/check-in", new { codigoQr = otroPuesto.CodigoQr });
        Assert.Equal(HttpStatusCode.Conflict, codigoAjeno.StatusCode);
        Assert.Contains("check-in-codigo-invalido", await codigoAjeno.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var ingreso = await titular.PostAsJsonAsync($"/api/reservas/{reserva.Id}/check-in", new { codigoQr = puesto.CodigoQr });
        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);

        var repetido = await titular.PostAsJsonAsync($"/api/reservas/{reserva.Id}/check-in", new { codigoQr = puesto.CodigoQr });
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Contains("transicion-invalida", await repetido.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var salidaAjena = await otro.PostAsync($"/api/reservas/{reserva.Id}/check-out", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, salidaAjena.StatusCode);
        Assert.Contains("reserva-ajena", await salidaAjena.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var pendiente = await SoporteApiPruebas.ReservarAsync(
            otro,
            puesto.PuestoId,
            SoporteApiPruebas.EnZona(fecha, 12));

        var salidaSinIngreso = await otro.PostAsync($"/api/reservas/{pendiente.Id}/check-out", content: null);
        Assert.Equal(HttpStatusCode.Conflict, salidaSinIngreso.StatusCode);
        Assert.Contains("transicion-invalida", await salidaSinIngreso.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var anonimoSalida = fabrica.CreateClient();
        var salidaSinSesion = await anonimoSalida.PostAsync($"/api/reservas/{reserva.Id}/check-out", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, salidaSinSesion.StatusCode);
    }

    private sealed record ReservaRespuesta(Guid Id);
}
