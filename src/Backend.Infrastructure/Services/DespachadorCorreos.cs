using Backend.Application.Common;
using Backend.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

/// <summary>
/// Despachador en segundo plano de la cola de correos. El envío ocurre fuera del ciclo HTTP:
/// cada fallo incrementa el contador de intentos y la decisión del revisor nunca se revierte.
/// </summary>
public sealed class DespachadorCorreos : BackgroundService
{
    private const int LoteMaximo = 20;

    private readonly IServiceScopeFactory _alcances;
    private readonly OpcionesSmtp _opciones;
    private readonly ILogger<DespachadorCorreos> _registro;

    /// <summary>Inicializa el despachador con la fábrica de alcances y la configuración SMTP.</summary>
    public DespachadorCorreos(
        IServiceScopeFactory alcances,
        OpcionesSmtp opciones,
        ILogger<DespachadorCorreos> registro)
    {
        _alcances = alcances ?? throw new ArgumentNullException(nameof(alcances));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opciones.Habilitado)
        {
            _registro.LogInformation(
                "El despachador de correos está deshabilitado (Smtp:Habilitado = false).");
            return;
        }

        var intervalo = TimeSpan.FromSeconds(_opciones.IntervaloDespachoSegundos);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DespacharLoteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception excepcion)
            {
                _registro.LogError(excepcion, "Falló el barrido de la cola de correos.");
            }

            try
            {
                await Task.Delay(intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DespacharLoteAsync(CancellationToken cancellationToken)
    {
        using var alcance = _alcances.CreateScope();

        var cola = alcance.ServiceProvider.GetRequiredService<IColaCorreos>();
        var correo = alcance.ServiceProvider.GetRequiredService<IEmailService>();
        var reloj = alcance.ServiceProvider.GetRequiredService<IRelojSistema>();

        var pendientes = await cola.TomarPendientesAsync(LoteMaximo, reloj.Ahora, cancellationToken);

        foreach (var mensaje in pendientes)
        {
            var envio = await correo.EnviarAsync(
                mensaje.Destinatario,
                mensaje.Asunto,
                mensaje.CuerpoHtml,
                cancellationToken);

            if (envio.Exitoso)
            {
                await cola.MarcarEnviadoAsync(mensaje.Id, reloj.Ahora, cancellationToken);
                _registro.LogInformation("Correo {CorreoId} enviado.", mensaje.Id);
            }
            else
            {
                await cola.MarcarFalloAsync(mensaje.Id, envio.Error ?? "Fallo de envío", reloj.Ahora, cancellationToken);
                _registro.LogWarning(
                    "No fue posible enviar el correo {CorreoId}: {Error}",
                    mensaje.Id,
                    envio.Error);
            }
        }
    }
}
