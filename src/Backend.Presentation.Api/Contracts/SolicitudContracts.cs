using System.ComponentModel.DataAnnotations;

namespace Backend.Presentation.Api.Contracts;

/// <summary>Cuerpo del alta de una serie semestral de área completa.</summary>
public sealed class SolicitudCrearSerie
{
    /// <summary>Área completa que se reserva durante el semestre.</summary>
    [Required(ErrorMessage = "El área es obligatoria.")]
    public Guid EspacioId { get; init; }

    /// <summary>Cadencia del patrón: <c>Semanal</c>, <c>Bisemanal</c> o <c>FechasFijas</c>.</summary>
    [Required(ErrorMessage = "El tipo de recurrencia es obligatorio.")]
    public string TipoRecurrencia { get; init; } = string.Empty;

    /// <summary>Día de la semana del patrón recurrente.</summary>
    public string? DiaSemana { get; init; }

    /// <summary>Fechas explícitas del patrón de fechas fijas (<c>yyyy-MM-dd</c>).</summary>
    public IReadOnlyList<string>? FechasFijas { get; init; }

    /// <summary>Hora local de inicio de cada ocurrencia (<c>HH:mm</c>).</summary>
    [Required(ErrorMessage = "La hora de inicio es obligatoria.")]
    public string HoraInicio { get; init; } = string.Empty;

    /// <summary>Hora local de fin de cada ocurrencia (<c>HH:mm</c>).</summary>
    [Required(ErrorMessage = "La hora de fin es obligatoria.")]
    public string HoraFin { get; init; } = string.Empty;

    /// <summary>Primer día del periodo semestral declarado.</summary>
    [Required(ErrorMessage = "El inicio del periodo semestral es obligatorio.")]
    public DateOnly PeriodoInicio { get; init; }

    /// <summary>Último día del periodo semestral declarado.</summary>
    [Required(ErrorMessage = "El fin del periodo semestral es obligatorio.")]
    public DateOnly PeriodoFin { get; init; }

    /// <summary>Materia o asignatura asociada a la serie.</summary>
    [Required(ErrorMessage = "La materia es obligatoria.")]
    [MaxLength(120, ErrorMessage = "La materia no puede superar los 120 caracteres.")]
    public string Materia { get; init; } = string.Empty;
}

/// <summary>Cuerpo del cambio de estado de una solicitud.</summary>
public sealed class SolicitudCambiarEstado
{
    /// <summary>Estado destino: <c>EnRevision</c>, <c>Aprobado</c> o <c>Rechazado</c>.</summary>
    [Required(ErrorMessage = "El estado es obligatorio.")]
    public string Estado { get; init; } = string.Empty;

    /// <summary>Motivo del rechazo; obligatorio y de al menos 10 caracteres al rechazar.</summary>
    [MaxLength(500, ErrorMessage = "El motivo del rechazo no puede superar los 500 caracteres.")]
    public string? Motivo { get; init; }
}

/// <summary>Parte multipart de la radicación de una solicitud con su pre-informe.</summary>
public sealed class SolicitudRadicarSolicitud
{
    /// <summary>Área completa solicitada.</summary>
    [Required(ErrorMessage = "El área es obligatoria.")]
    public Guid EspacioId { get; init; }

    /// <summary>Inicio de la franja solicitada.</summary>
    [Required(ErrorMessage = "El inicio de la franja es obligatorio.")]
    public DateTimeOffset Inicio { get; init; }

    /// <summary>Fin de la franja solicitada.</summary>
    [Required(ErrorMessage = "El fin de la franja es obligatorio.")]
    public DateTimeOffset Fin { get; init; }

    /// <summary>Materia o actividad de la solicitud.</summary>
    [Required(ErrorMessage = "La materia es obligatoria.")]
    [MaxLength(120, ErrorMessage = "La materia no puede superar los 120 caracteres.")]
    public string Materia { get; init; } = string.Empty;

    /// <summary>Docente responsable de la actividad.</summary>
    [Required(ErrorMessage = "El docente responsable es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El nombre del docente responsable no puede superar los 150 caracteres.")]
    public string DocenteResponsable { get; init; } = string.Empty;

    /// <summary>Justificación de la solicitud.</summary>
    [Required(ErrorMessage = "La justificación es obligatoria.")]
    [MaxLength(1000, ErrorMessage = "La justificación no puede superar los 1000 caracteres.")]
    public string Justificacion { get; init; } = string.Empty;

    /// <summary>Pre-informe PDF adjunto.</summary>
    public IFormFile? Archivo { get; init; }
}
