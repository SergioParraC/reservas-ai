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

    /// <summary>Liberada automáticamente por inasistencia al vencer la ventana de check-in.</summary>
    NoShow = 3,

    /// <summary>El titular confirmó su presencia con el QR del puesto y ocupa la franja.</summary>
    EnUso = 4,

    /// <summary>El titular registró su salida; la franja deja de ocuparse en ese instante.</summary>
    Finalizada = 5
}
