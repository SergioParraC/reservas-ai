namespace Backend.Application.Common;

/// <summary>
/// Configuración del barrido de inasistencias. Solo contiene la cadencia, el tamaño del lote y el
/// interruptor: la tolerancia del check-in vive exclusivamente en <see cref="OpcionesReservas"/>
/// para que el endpoint y el worker no puedan divergir.
/// </summary>
public sealed class OpcionesCheckIn
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "CheckIn";

    /// <summary>Minutos entre barridos consecutivos de reservas vencidas sin check-in.</summary>
    public int MinutosIntervaloBarrido { get; set; } = 5;

    /// <summary>Tope de reservas liberadas por ciclo, para no bloquear la tabla en horas pico.</summary>
    public int MaximoReservasPorBarrido { get; set; } = 200;

    /// <summary>Indica si el servicio de barrido está activo; en pruebas puede quedar apagado.</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>Cadencia del barrido como <see cref="TimeSpan"/>.</summary>
    public TimeSpan ObtenerIntervaloBarrido() => TimeSpan.FromMinutes(MinutosIntervaloBarrido);

    /// <summary>Valida la coherencia de la configuración y falla el arranque si es inválida.</summary>
    public void Validar()
    {
        if (MinutosIntervaloBarrido is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                "CheckIn:MinutosIntervaloBarrido debe estar entre 1 y 1440 minutos.");
        }

        if (MaximoReservasPorBarrido is < 1 or > 1000)
        {
            throw new InvalidOperationException(
                "CheckIn:MaximoReservasPorBarrido debe estar entre 1 y 1000 reservas.");
        }
    }
}
