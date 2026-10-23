namespace Backend.Domain.Enums;

/// <summary>
/// Estado transaccional de una reserva. El código QR del puesto nunca cambia con el estado:
/// identifica al recurso físico, no al apartado.
/// </summary>
public enum EstadoReserva
{
    /// <summary>La franja está apartada y bloquea el puesto.</summary>
    Confirmada = 1,

    /// <summary>El titular liberó la franja dentro del plazo permitido.</summary>
    Cancelada = 2,

    /// <summary>Liberada automáticamente por inasistencia; la produce la Etapa 3.</summary>
    CanceladaPorNoShow = 3
}
