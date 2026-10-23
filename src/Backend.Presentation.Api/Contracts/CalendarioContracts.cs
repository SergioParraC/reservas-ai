using System.ComponentModel.DataAnnotations;

namespace Backend.Presentation.Api.Contracts;

/// <summary>Datos de alta de un periodo académico.</summary>
public class SolicitudCrearPeriodo
{
    /// <summary>Nombre visible del periodo (<c>2026-2</c>).</summary>
    [Required(ErrorMessage = "El nombre del periodo académico es obligatorio.")]
    [MaxLength(40, ErrorMessage = "El nombre del periodo académico no puede superar los 40 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Primer día del periodo.</summary>
    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    public DateOnly FechaInicio { get; init; }

    /// <summary>Último día del periodo.</summary>
    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    public DateOnly FechaFin { get; init; }
}

/// <summary>Datos de edición de un periodo académico.</summary>
public sealed class SolicitudActualizarPeriodo : SolicitudCrearPeriodo
{
    /// <summary>Token de concurrencia devuelto por la consulta anterior.</summary>
    public string? RowVersion { get; init; }
}

/// <summary>Datos de alta de una franja no operativa global.</summary>
public class SolicitudCrearFranjaNoOperativa
{
    /// <summary>Día de la semana recurrente; excluyente con <see cref="FechaEspecifica"/>.</summary>
    public string? DiaSemana { get; init; }

    /// <summary>Fecha específica de la no operación; excluyente con <see cref="DiaSemana"/>.</summary>
    public DateOnly? FechaEspecifica { get; init; }

    /// <summary>Hora de inicio en formato <c>HH:mm</c>.</summary>
    [Required(ErrorMessage = "La hora de inicio es obligatoria.")]
    public string HoraInicio { get; init; } = string.Empty;

    /// <summary>Hora de fin en formato <c>HH:mm</c>.</summary>
    [Required(ErrorMessage = "La hora de fin es obligatoria.")]
    public string HoraFin { get; init; } = string.Empty;

    /// <summary>Indica si la franja corresponde a un feriado.</summary>
    public bool EsFeriado { get; init; }

    /// <summary>Motivo visible de la no operación.</summary>
    [Required(ErrorMessage = "El motivo de la franja no operativa es obligatorio.")]
    [MaxLength(120, ErrorMessage = "El motivo de la franja no operativa no puede superar los 120 caracteres.")]
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>Datos de edición de una franja no operativa.</summary>
public sealed class SolicitudActualizarFranjaNoOperativa : SolicitudCrearFranjaNoOperativa
{
    /// <summary>Token de concurrencia devuelto por la consulta anterior.</summary>
    public string? RowVersion { get; init; }
}
