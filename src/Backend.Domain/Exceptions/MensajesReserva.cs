namespace Backend.Domain.Exceptions;

/// <summary>
/// Constantes de los mensajes de negocio del dominio de reservas.
/// Se centralizan para que la API, la SPA y las pruebas compartan el mismo texto.
/// </summary>
public static class MensajesReserva
{
    /// <summary>El fin debe ser posterior al inicio.</summary>
    public const string FranjaInvalida = "El fin de la franja debe ser posterior al inicio.";

    /// <summary>El inicio y el fin no comparten desplazamiento horario.</summary>
    public const string DesplazamientoInvalido = "El inicio y el fin deben compartir el mismo desplazamiento horario.";

    /// <summary>La duración solicitada está fuera del rango configurado.</summary>
    public const string DuracionFueraDeRango = "La duración debe estar entre {0} y {1} minutos.";

    /// <summary>El inicio no respeta la granularidad de las franjas.</summary>
    public const string InicioNoAlineado = "El inicio de la franja debe estar alineado cada {0} minutos.";

    /// <summary>La franja cae fuera de la jornada institucional.</summary>
    public const string FranjaFueraDeJornada = "La franja debe estar dentro de la jornada institucional de {0} a {1}.";

    /// <summary>La reserva no cumple la antelación mínima.</summary>
    public const string InicioFueraDeAntelacion = "La reserva debe realizarse con al menos {0} minutos de antelación.";

    /// <summary>El inicio supera el horizonte máximo de reserva.</summary>
    public const string InicioFueraDeHorizonte = "La reserva solo puede programarse hasta {0} días después.";

    /// <summary>La fecha consultada o solicitada está fuera del horizonte permitido.</summary>
    public const string FechaFueraDeHorizonte = "La fecha solicitada está fuera del horizonte de reserva permitido.";

    /// <summary>El rango de consulta es inverso.</summary>
    public const string RangoInvalido = "La fecha inicial no puede ser posterior a la fecha final.";

    /// <summary>El puesto no admite préstamo directo autoconfirmado.</summary>
    public const string PuestoNoReservable = "El puesto no admite préstamo directo.";

    /// <summary>La franja ya está ocupada por otra reserva vigente.</summary>
    public const string FranjaNoDisponible = "La franja solicitada ya está ocupada en el puesto.";

    /// <summary>La cuenta agotó su cupo de reservas activas.</summary>
    public const string CupoAgotado = "La cuenta alcanzó el máximo de {0} reservas activas.";

    /// <summary>La reserva ya está dentro del margen de cancelación.</summary>
    public const string FueraDePlazo = "La reserva ya no puede cancelarse: faltan menos de {0} minutos para su inicio.";

    /// <summary>La reserva no está en un estado cancelable.</summary>
    public const string EstadoNoCancelable = "La reserva no está en estado confirmado.";

    /// <summary>La reserva indicada no existe.</summary>
    public const string ReservaNoEncontrada = "La reserva solicitada no existe.";

    /// <summary>La reserva pertenece a otro usuario.</summary>
    public const string ReservaAjena = "La reserva pertenece a otro usuario.";

    /// <summary>El puesto indicado no existe.</summary>
    public const string PuestoNoEncontrado = "El puesto solicitado no existe.";

    /// <summary>La operación exige una sesión autenticada.</summary>
    public const string SinSesion = "La operación requiere una sesión autenticada.";

    /// <summary>El check-in cae fuera de la ventana de tolerancia configurada.</summary>
    public const string CheckInFueraDeVentana =
        "Solo se admite el check-in entre {0:HH:mm} y {1:HH:mm} del inicio de la franja.";

    /// <summary>El código escaneado no pertenece al puesto reservado.</summary>
    public const string CheckInCodigoInvalido = "El código QR no corresponde al puesto reservado.";

    /// <summary>El estado actual de la reserva no admite la transición solicitada.</summary>
    public const string TransicionInvalida = "La reserva no admite esta transición desde su estado actual.";

    /// <summary>La transición no pudo persistirse por un conflicto de concurrencia.</summary>
    public const string TransicionNoPersistida =
        "No fue posible registrar el cambio de estado de la reserva. Intente nuevamente.";

    /// <summary>El lote de liberación por inasistencia no pudo confirmarse.</summary>
    public const string LiberacionNoPersistida =
        "No fue posible persistir la liberación por inasistencia; el lote se reintentará en el siguiente ciclo.";
}
