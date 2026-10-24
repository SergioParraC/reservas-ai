using System.Net;
using System.Net.Http.Json;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Backend.IntegrationTests.Asistencia;

/// <summary>
/// Concurrencia sobre una franja que ya está en uso y sobre el remanente liberado por una salida
/// anticipada: la transacción serializable y el índice único filtrado con <c>(1, 4)</c> garantizan
/// que solo una solicitud prospere.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class ConcurrenciaReservasEnUsoTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido de SQL Server.</summary>
    public ConcurrenciaReservasEnUsoTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task DosSolicitudesSobreUnaFranjaEnUsoSeRechazanSinCrearReservas()
    {
        await using var escenario = await PrepararAsync("asistencia_concurrencia_uso");

        using var titular = await escenario.CrearEstudianteAsync("ana@uninpahu.edu.co");
        using var primero = await escenario.CrearEstudianteAsync("luis@uninpahu.edu.co");
        using var segundo = await escenario.CrearEstudianteAsync("marta@uninpahu.edu.co");

        var reserva = await SoporteApiPruebas.ReservarAsync(titular, escenario.Puesto.PuestoId, escenario.Inicio);

        escenario.Reloj.SituarEn(escenario.Inicio);
        var ingreso = await titular.PostAsJsonAsync(
            $"/api/reservas/{reserva.Id}/check-in",
            new { codigoQr = escenario.Puesto.CodigoQr });
        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);

        var respuestas = await Task.WhenAll(
            ReservarAsync(primero, escenario.Puesto.PuestoId, escenario.Inicio),
            ReservarAsync(segundo, escenario.Puesto.PuestoId, escenario.Inicio));

        Assert.Equal(0, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Created));
        Assert.Equal(2, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Conflict));

        var mias = await primero.GetFromJsonAsync<PaginaRespuesta>("/api/reservas/me");
        Assert.NotNull(mias);
        Assert.Equal(0, mias.Total);
    }

    [DockerFact]
    public async Task DosSolicitudesSimultaneasSobreElRemanenteDejanUnaSolaReserva()
    {
        await using var escenario = await PrepararAsync("asistencia_concurrencia_remanente");

        using var titular = await escenario.CrearEstudianteAsync("ana@uninpahu.edu.co");
        using var primero = await escenario.CrearEstudianteAsync("luis@uninpahu.edu.co");
        using var segundo = await escenario.CrearEstudianteAsync("marta@uninpahu.edu.co");

        var reserva = await SoporteApiPruebas.ReservarAsync(titular, escenario.Puesto.PuestoId, escenario.Inicio);

        escenario.Reloj.SituarEn(escenario.Inicio);
        var ingreso = await titular.PostAsJsonAsync(
            $"/api/reservas/{reserva.Id}/check-in",
            new { codigoQr = escenario.Puesto.CodigoQr });
        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);

        escenario.Reloj.SituarEn(escenario.Inicio.AddMinutes(45));

        var salida = await titular.PostAsync($"/api/reservas/{reserva.Id}/check-out", content: null);
        Assert.Equal(HttpStatusCode.OK, salida.StatusCode);

        var remanente = escenario.Inicio.AddMinutes(45);
        var respuestas = await Task.WhenAll(
            ReservarAsync(primero, escenario.Puesto.PuestoId, remanente),
            ReservarAsync(segundo, escenario.Puesto.PuestoId, remanente));

        Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Conflict));
    }

    private static Task<HttpResponseMessage> ReservarAsync(HttpClient cliente, Guid puestoId, DateTimeOffset inicio) =>
        cliente.PostAsJsonAsync("/api/reservas", new
        {
            puestoId,
            inicio,
            fin = inicio.AddMinutes(15)
        });

    private async Task<Escenario> PrepararAsync(string baseDatos)
    {
        var cadena = await _sql.CrearBaseDeDatosAsync(baseDatos);
        var reloj = new RelojPruebas();

        var fabrica = FabricaApiPruebas.CrearConReloj(
            cadena,
            reloj,
            new Dictionary<string, string?>
            {
                ["Reservas:AntelacionMinimaMinutos"] = "0",
                ["Reservas:DuracionMinimaMinutos"] = "15"
            });

        var administrador = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(administrador);
        var puesto = await SoporteApiPruebas.CrearAreaConPuestoAsync(administrador, "PC-07");
        await SoporteApiPruebas.ActivarPeriodoAsync(
            administrador,
            SoporteApiPruebas.FechaLocal(-30),
            SoporteApiPruebas.FechaLocal(60));

        var inicio = SoporteApiPruebas.EnZona(SoporteApiPruebas.FechaLocal(1), 10);

        return new Escenario(fabrica, administrador, reloj, puesto, inicio);
    }

    private sealed record ReservaResumenRespuesta(Guid Id, int Estado);

    private sealed record PaginaRespuesta(IReadOnlyList<ReservaResumenRespuesta> Elementos, int Total);

    private sealed class Escenario : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _fabrica;
        private readonly HttpClient _administrador;

        public Escenario(
            WebApplicationFactory<Program> fabrica,
            HttpClient administrador,
            RelojPruebas reloj,
            PuestoPreparado puesto,
            DateTimeOffset inicio)
        {
            _fabrica = fabrica;
            _administrador = administrador;
            Reloj = reloj;
            Puesto = puesto;
            Inicio = inicio;
        }

        public RelojPruebas Reloj { get; }

        public PuestoPreparado Puesto { get; }

        public DateTimeOffset Inicio { get; }

        public Task<HttpClient> CrearEstudianteAsync(string correo) =>
            SoporteApiPruebas.CrearEstudianteAsync(_fabrica, _administrador, correo);

        public async ValueTask DisposeAsync()
        {
            _administrador.Dispose();
            await _fabrica.DisposeAsync();
        }
    }
}
