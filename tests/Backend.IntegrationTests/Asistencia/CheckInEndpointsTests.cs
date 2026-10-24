using System.Net;
using System.Net.Http.Json;
using Backend.IntegrationTests.Infraestructura;

namespace Backend.IntegrationTests.Asistencia;

/// <summary>
/// Flujo completo de asistencia sobre la API real: check-in con el QR del puesto, bloqueo de la
/// franja en uso, salida anticipada y liberación del remanente para otro usuario.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class CheckInEndpointsTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido de SQL Server.</summary>
    public CheckInEndpointsTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElCheckInHabilitaLaFranjaYElCheckOutLiberaElRemanente()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("asistencia_flujo");
        var reloj = new RelojPruebas();

        await using var fabrica = FabricaApiPruebas.CrearConReloj(
            cadena,
            reloj,
            new Dictionary<string, string?> { ["Reservas:DuracionMinimaMinutos"] = "15" });

        using var administrador = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(administrador);
        var puesto = await SoporteApiPruebas.CrearAreaConPuestoAsync(administrador, "PC-07");
        await SoporteApiPruebas.ActivarPeriodoAsync(
            administrador,
            SoporteApiPruebas.FechaLocal(-30),
            SoporteApiPruebas.FechaLocal(60));

        using var titular = await SoporteApiPruebas.CrearEstudianteAsync(fabrica, administrador, "ana@uninpahu.edu.co");
        using var otro = await SoporteApiPruebas.CrearEstudianteAsync(fabrica, administrador, "luis@uninpahu.edu.co");

        var fecha = SoporteApiPruebas.FechaLocal(1);
        var inicio = SoporteApiPruebas.EnZona(fecha, 10);
        var reserva = await SoporteApiPruebas.ReservarAsync(titular, puesto.PuestoId, inicio);

        Assert.Equal(inicio, reserva.Inicio);
        Assert.Equal(inicio.AddHours(1), reserva.FinPlanificado);

        var ajeno = await otro.PostAsJsonAsync("/api/reservas", new
        {
            puestoId = puesto.PuestoId,
            inicio,
            fin = inicio.AddHours(1)
        });
        Assert.Equal(HttpStatusCode.Conflict, ajeno.StatusCode);

        reloj.SituarEn(inicio.AddMinutes(2));

        var checkIn = await titular.PostAsJsonAsync($"/api/reservas/{reserva.Id}/check-in", new { codigoQr = puesto.CodigoQr });
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);

        var enUso = await checkIn.Content.ReadFromJsonAsync<ReservaRespuesta>();
        Assert.NotNull(enUso);
        Assert.Equal(4, enUso.Estado);
        Assert.Equal(inicio.AddMinutes(2), enUso.FechaCheckIn);
        Assert.Null(enUso.FechaCheckOut);
        Assert.Equal(reserva.FinPlanificado, enUso.Fin);
        Assert.Equal(reserva.FinPlanificado, enUso.FinPlanificado);

        var mias = await titular.GetFromJsonAsync<PaginaRespuesta>("/api/reservas/me");
        Assert.NotNull(mias);
        var agenda = Assert.Single(mias.Elementos);
        Assert.Equal(4, agenda.Estado);
        Assert.Equal(reserva.FinPlanificado, agenda.FinPlanificado);
        Assert.Equal(inicio.AddMinutes(2), agenda.FechaCheckIn);

        var duranteUso = await otro.GetFromJsonAsync<DisponibilidadRespuesta>(
            $"/api/puestos/{puesto.PuestoId}/disponibilidad?fecha={fecha:yyyy-MM-dd}&duracionMinutos=15");
        Assert.NotNull(duranteUso);
        Assert.Equal("Ocupada", Assert.Single(duranteUso.Franjas, franja => franja.Inicio == inicio).Motivo);

        var antesDeSalir = await otro.GetFromJsonAsync<DisponibilidadRespuesta>(
            $"/api/puestos/{puesto.PuestoId}/disponibilidad?fecha={fecha:yyyy-MM-dd}&duracionMinutos=15&desde=10:45");
        Assert.NotNull(antesDeSalir);
        var remanenteOcupado = Assert.Single(antesDeSalir.Franjas, franja => franja.Inicio == inicio.AddMinutes(45));
        Assert.False(remanenteOcupado.Disponible);
        Assert.Equal("Ocupada", remanenteOcupado.Motivo);

        reloj.SituarEn(inicio.AddMinutes(45));

        var checkOut = await titular.PostAsync($"/api/reservas/{reserva.Id}/check-out", content: null);
        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);

        var finalizada = await checkOut.Content.ReadFromJsonAsync<ReservaRespuesta>();
        Assert.NotNull(finalizada);
        Assert.Equal(5, finalizada.Estado);
        Assert.Equal(inicio.AddMinutes(45), finalizada.Fin);
        Assert.Equal(reserva.FinPlanificado, finalizada.FinPlanificado);
        Assert.Equal(inicio.AddMinutes(45), finalizada.FechaCheckOut);

        reloj.SituarEn(inicio.AddMinutes(-90));

        var despues = await otro.GetFromJsonAsync<DisponibilidadRespuesta>(
            $"/api/puestos/{puesto.PuestoId}/disponibilidad?fecha={fecha:yyyy-MM-dd}&duracionMinutos=15");
        Assert.NotNull(despues);
        Assert.True(Assert.Single(despues.Franjas, franja => franja.Inicio == inicio.AddMinutes(45)).Disponible);
        Assert.True(Assert.Single(despues.Franjas, franja => franja.Inicio == inicio).Disponible);

        var remanente = await otro.PostAsJsonAsync("/api/reservas", new
        {
            puestoId = puesto.PuestoId,
            inicio = inicio.AddMinutes(45),
            fin = inicio.AddHours(1)
        });
        Assert.Equal(HttpStatusCode.Created, remanente.StatusCode);
    }

    private sealed record ReservaRespuesta(
        Guid Id,
        DateTimeOffset Inicio,
        DateTimeOffset Fin,
        DateTimeOffset FinPlanificado,
        int Estado,
        DateTimeOffset? FechaCheckIn,
        DateTimeOffset? FechaCheckOut);

    private sealed record ReservaResumenRespuesta(
        Guid Id,
        DateTimeOffset Inicio,
        DateTimeOffset Fin,
        DateTimeOffset FinPlanificado,
        int Estado,
        DateTimeOffset? FechaCheckIn);

    private sealed record PaginaRespuesta(IReadOnlyList<ReservaResumenRespuesta> Elementos, int Total);

    private sealed record FranjaRespuesta(DateTimeOffset Inicio, DateTimeOffset Fin, bool Disponible, string? Motivo);

    private sealed record DisponibilidadRespuesta(IReadOnlyList<FranjaRespuesta> Franjas);
}
