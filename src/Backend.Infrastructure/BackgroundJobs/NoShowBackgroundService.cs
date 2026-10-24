using Backend.Application.Common;
using Backend.Application.Modules.Asistencia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.BackgroundJobs;

/// <summary>
/// Barrido periódico de inasistencias: libera las reservas confirmadas cuya ventana de check-in
/// venció. Usa <see cref="PeriodicTimer"/> para no solapar ciclos, un alcance de servicios por
/// ciclo —como el despachador de correos— y un <c>try/catch</c> que registra el fallo sin detener
/// el host: el siguiente ciclo reintenta el lote completo.
/// </summary>
public sealed class NoShowBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _alcances;
    private readonly OpcionesReservas _opcionesReservas;
    private readonly OpcionesCheckIn _opcionesCheckIn;
    private readonly ILogger<NoShowBackgroundService> _registro;

    /// <summary>Inicializa el barrido con la fábrica de alcances y la configuración de check-in.</summary>
    public NoShowBackgroundService(
        IServiceScopeFactory alcances,
        OpcionesReservas opcionesReservas,
        OpcionesCheckIn opcionesCheckIn,
        ILogger<NoShowBackgroundService> registro)
    {
        _alcances = alcances ?? throw new ArgumentNullException(nameof(alcances));
        _opcionesReservas = opcionesReservas ?? throw new ArgumentNullException(nameof(opcionesReservas));
        _opcionesCheckIn = opcionesCheckIn ?? throw new ArgumentNullException(nameof(opcionesCheckIn));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opcionesCheckIn.Habilitado)
        {
            _registro.LogInformation(
                "El barrido de inasistencias está deshabilitado (CheckIn:Habilitado = false).");
            return;
        }

        // Primer ciclo inmediato: recupera lo vencido durante una caída del proceso sin esperar
        // la cadencia completa, de modo que las franjas retenidas se liberen cuanto antes.
        await EjecutarCicloAsync(stoppingToken);

        using var temporizador = new PeriodicTimer(_opcionesCheckIn.ObtenerIntervaloBarrido());

        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
            {
                await EjecutarCicloAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado ordenado del host.
        }
    }

    /// <summary>
    /// Ejecuta un ciclo completo del barrido con alcance propio. Nunca propaga fallos del ciclo:
    /// los registra y deja que el temporizador dispare el siguiente intento.
    /// </summary>
    public async Task EjecutarCicloAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var alcance = _alcances.CreateScope();

            var handler = alcance.ServiceProvider.GetRequiredService<LiberarReservasVencidasHandler>();
            var tolerancia = _opcionesReservas.ObtenerVentanaTolerancia();

            var resultado = await handler.HandleAsync(cancellationToken);

            if (!resultado.Exitoso)
            {
                _registro.LogError(
                    "El barrido de inasistencias (tolerancia {ToleranciaMinutos} min) no pudo confirmarse: {Mensaje}",
                    (int)tolerancia.TotalMinutes,
                    resultado.Mensaje);
                return;
            }

            if (resultado.Valor is { Liberadas: > 0 } liberacion)
            {
                _registro.LogInformation(
                    "Barrido de inasistencias (tolerancia {ToleranciaMinutos} min): {Liberadas} reservas liberadas.",
                    (int)tolerancia.TotalMinutes,
                    liberacion.Liberadas);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excepcion)
        {
            _registro.LogError(
                excepcion,
                "Falló el barrido de inasistencias; el servicio continúa y el siguiente ciclo lo reintentará.");
        }
    }
}
