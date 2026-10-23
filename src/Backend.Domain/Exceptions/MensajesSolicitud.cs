namespace Backend.Domain.Exceptions;

/// <summary>
/// Constantes de los mensajes de negocio de las solicitudes de préstamo y de su bandeja
/// de aprobaciones. La API traduce cada constante a un <c>type</c> estable.
/// </summary>
public static class MensajesSolicitud
{
    /// <summary>La solicitud indicada no existe.</summary>
    public const string SolicitudNoEncontrada = "La solicitud solicitada no existe.";

    /// <summary>La solicitud pertenece a otro usuario y no hay competencia para verla.</summary>
    public const string SolicitudAjena = "No tiene competencia sobre esta solicitud.";

    /// <summary>La transición de estado solicitada no es válida.</summary>
    public const string TransicionInvalida = "La transición de estado solicitada no es válida para el estado actual.";

    /// <summary>El motivo del rechazo es obligatorio.</summary>
    public const string MotivoRequerido = "El motivo del rechazo es obligatorio.";

    /// <summary>El motivo del rechazo no alcanza la longitud mínima.</summary>
    public const string MotivoDemasiadoCorto = "El motivo del rechazo debe tener al menos {0} caracteres.";

    /// <summary>El motivo del rechazo excede la longitud admitida.</summary>
    public const string MotivoDemasiadoLargo = "El motivo del rechazo no puede superar los 500 caracteres.";

    /// <summary>El área no admite solicitudes porque su aprobación es automática.</summary>
    public const string EspacioNoSolicitable = "El área no admite solicitudes: su aprobación es automática.";

    /// <summary>La modalidad del área no admite solicitudes de área completa.</summary>
    public const string ModalidadIncompatible = "La modalidad de préstamo del área no admite solicitudes de área completa.";

    /// <summary>El área no está activa para recibir solicitudes.</summary>
    public const string EspacioNoDisponible = "El área no está disponible para solicitudes: se encuentra inactiva.";

    /// <summary>La franja ya está ocupada en la agenda del área.</summary>
    public const string FranjaNoDisponible = "La franja solicitada ya está ocupada en la agenda del área.";

    /// <summary>La cuenta agotó su cupo de solicitudes activas.</summary>
    public const string LimiteSolicitudes = "La cuenta alcanzó el máximo de {0} solicitudes activas.";

    /// <summary>El archivo adjunto no es un PDF válido.</summary>
    public const string ArchivoNoValido = "El archivo adjunto no es un PDF válido.";

    /// <summary>El archivo adjunto supera el tamaño máximo permitido.</summary>
    public const string ArchivoDemasiadoGrande = "El archivo adjunto supera el tamaño máximo de {0} MB.";

    /// <summary>La solicitud requiere el archivo del pre-informe.</summary>
    public const string ArchivoRequerido = "El archivo del pre-informe es obligatorio.";

    /// <summary>La materia es obligatoria.</summary>
    public const string MateriaRequerida = "La materia es obligatoria.";

    /// <summary>La materia excede la longitud admitida.</summary>
    public const string MateriaDemasiadoLarga = "La materia no puede superar los 120 caracteres.";

    /// <summary>El docente responsable es obligatorio.</summary>
    public const string DocenteRequerido = "El docente responsable es obligatorio.";

    /// <summary>El docente responsable excede la longitud admitida.</summary>
    public const string DocenteDemasiadoLargo = "El nombre del docente responsable no puede superar los 150 caracteres.";

    /// <summary>La justificación es obligatoria.</summary>
    public const string JustificacionRequerida = "La justificación es obligatoria.";

    /// <summary>La justificación excede la longitud admitida.</summary>
    public const string JustificacionDemasiadoLarga = "La justificación no puede superar los 1000 caracteres.";

    /// <summary>La franja solicitada es inválida.</summary>
    public const string FranjaInvalida = "La franja solicitada no es válida: el fin debe ser posterior al inicio.";

    /// <summary>La franja solicitada está fuera de la jornada institucional.</summary>
    public const string FranjaFueraDeJornada = "La franja debe estar dentro de la jornada institucional de {0} a {1}.";

    /// <summary>La duración solicitada está fuera del rango configurado.</summary>
    public const string DuracionFueraDeRango = "La duración debe estar entre {0} y {1} minutos.";

    /// <summary>La franja no respeta la granularidad de las franjas.</summary>
    public const string InicioNoAlineado = "El inicio de la franja debe estar alineado cada {0} minutos.";

    /// <summary>La franja no cumple la antelación mínima.</summary>
    public const string InicioFueraDeAntelacion = "La solicitud debe realizarse con al menos {0} minutos de antelación.";

    /// <summary>El archivo del pre-informe no está disponible en el almacén.</summary>
    public const string ArchivoNoEncontrado = "El pre-informe de la solicitud no está disponible.";
}
