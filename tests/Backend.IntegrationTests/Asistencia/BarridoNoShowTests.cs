using System.Net;
using System.Net.Http.Json;
using Backend.Application.Modules.Asistencia;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.IntegrationTests.Asistencia;

/// <summary>
/// Barrido real contra SQL Server: libera las reservas cuya ventana de check-in venció, deja
/// intactas las sesiones en uso, libera la franja y es idempotente en ciclos consecutivos.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class BarridoNoShowTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido de SQL Server.</summary>
    public BarridoNoShowTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElBarridoLiberaLaReservaVencidaYLaFranjaVuelveAEstarDisponible()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("asistencia_barrido");
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

        var fecha = SoporteApiPruebas.FechaLocal(1);
        var inicio = SoporteApiPruebas.EnZona(fecha, 10);
        var reserva = await SoporteApiPruebas.ReservarAsync(titular, puesto.PuestoId, inicio);

        reloj.SituarEn(inicio.AddMinutes(14));
        Assert.Equal(0, await EjecutarBarridoAsync(fabrica));

        reloj.SituarEn(inicio.AddMinutes(15));
        Assert.Equal(1, await EjecutarBarridoAsync(fabrica));

        var mias = await titular.GetFromJsonAsync<PaginaRespuesta>("/api/reservas/me");
        Assert.NotNull(mias);
        var liberada = Assert.Single(mias.Elementos);
        Assert.Equal(reserva.Id, liberada.Id);
        Assert.Equal(3, liberada.Estado);
        Assert.NotNull(liberada.CanceladaEn);

        reloj.SituarEn(inicio.AddMinutes(-40));

        var disponibilidad = await titular.GetFromJsonAsync<DisponibilidadRespuesta>(
            $"/api/puestos/{puesto.PuestoId}/disponibilidad?fecha={fecha:yyyy-MM-dd}&duracionMinutos=15");
        Assert.NotNull(disponibilidad);
        Assert.True(Assert.Single(disponibilidad.Franjas, franja => franja.Inicio == inicio).Disponible);

        Assert.Equal(0, await EjecutarBarridoAsync(fabrica));
    }

    [DockerFact]
    public async Task UnaSesionEnUsoNoSeLiberaConElBarrido()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("asistencia_sesion");
        var reloj = new RelojPruebas();

        await using var fabrica = FabricaApiPruebas.CrearConReloj(cadena, reloj);

        using var administrador = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(administrador);
        var puesto = await SoporteApiPruebas.CrearAreaConPuestoAsync(administrador, "PC-01");
        await SoporteApiPruebas.ActivarPeriodoAsync(
            administrador,
            SoporteApiPruebas.FechaLocal(-30),
            SoporteApiPruebas.FechaLocal(60));

        using var titular = await SoporteApiPruebas.CrearEstudianteAsync(fabrica, administrador, "ana@uninpahu.edu.co");

        var fecha = SoporteApiPruebas.FechaLocal(1);
        var inicio = SoporteApiPruebas.EnZona(fecha, 10);
        var reserva = await SoporteApiPruebas.ReservarAsync(titular, puesto.PuestoId, inicio);

        reloj.SituarEn(inicio.AddMinutes(2));
        var ingreso = await titular.PostAsJsonAsync($"/api/reservas/{reserva.Id}/check-in", new { codigoQr = puesto.CodigoQr });
        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);

        reloj.SituarEn(inicio.AddMinutes(120));
        Assert.Equal(0, await EjecutarBarridoAsync(fabrica));

        var mias = await titular.GetFromJsonAsync<PaginaRespuesta>("/api/reservas/me");
        Assert.NotNull(mias);
        Assert.Equal(4, Assert.Single(mias.Elementos).Estado);
    }

    private static async Task<int> EjecutarBarridoAsync(WebApplicationFactory<Program> fabrica)
    {
        using var alcance = fabrica.Services.CreateScope();
        var handler = alcance.ServiceProvider.GetRequiredService<LiberarReservasVencidasHandler>();

        var resultado = await handler.HandleAsync();

        Assert.True(resultado.Exitoso, resultado.Mensaje);
        return resultado.Valor?.Liberadas ?? 0;
    }

    private sealed record ReservaResumenRespuesta(
        Guid Id,
        int Estado,
        DateTimeOffset? CanceladaEn,
        DateTimeOffset FinPlanificado);

    private sealed record PaginaRespuesta(IReadOnlyList<ReservaResumenRespuesta> Elementos, int Total);

    private sealed record FranjaRespuesta(DateTimeOffset Inicio, DateTimeOffset Fin, bool Disponible, string? Motivo);

    private sealed record DisponibilidadRespuesta(IReadOnlyList<FranjaRespuesta> Franjas);
}
