namespace Backend.Domain.Exceptions;

/// <summary>
/// Constantes de los mensajes de negocio de las series semestrales. Las pruebas, la API y la
/// SPA comparten el mismo texto para presentar la causa exacta del rechazo.
/// </summary>
public static class MensajesSemestre
{
    /// <summary>El rango semestral es inverso o vacío.</summary>
    public const string PeriodoInvalido = "La fecha final del periodo semestral debe ser posterior a la inicial.";

    /// <summary>El periodo semestral excede el máximo de semanas configurado.</summary>
    public const string PeriodoExcedido = "El periodo solicitado supera el máximo de {0} semanas por serie.";

    /// <summary>Una fecha fija queda fuera del periodo semestral declarado.</summary>
    public const string FechaFueraDePeriodo = "La fecha {0} queda fuera del periodo semestral solicitado.";

    /// <summary>El patrón semanal o bisemanal requiere día de la semana.</summary>
    public const string DiaSemanaRequerido = "El patrón semanal o bisemanal requiere el día de la semana.";

    /// <summary>El patrón de fechas fijas requiere al menos una fecha.</summary>
    public const string FechasFijasRequeridas = "El patrón de fechas fijas requiere al menos una fecha.";

    /// <summary>El patrón de fechas fijas no admite día de la semana.</summary>
    public const string FechasFijasSinDia = "El patrón de fechas fijas no admite día de la semana.";

    /// <summary>El tipo de recurrencia recibido no es válido.</summary>
    public const string TipoRecurrenciaInvalido = "El tipo de recurrencia no es válido.";

    /// <summary>La hora de fin del patrón no es posterior a la de inicio.</summary>
    public const string FranjaPatronInvalida = "La hora de fin del patrón debe ser posterior a la hora de inicio.";

    /// <summary>La serie no genera ninguna ocurrencia operativa.</summary>
    public const string SerieSinOcurrencias = "La serie no genera ninguna ocurrencia operativa en el periodo solicitado.";

    /// <summary>La primera ocurrencia no cumple la antelación mínima.</summary>
    public const string AntelacionMinima = "La primera ocurrencia debe programarse con al menos {0} días de antelación.";

    /// <summary>Alguna ocurrencia de la serie choca con la agenda vigente del área.</summary>
    public const string ColisionSerie = "La serie se cruza con la agenda vigente del área en {0} ocurrencia(s).";

    /// <summary>La materia de la serie es obligatoria.</summary>
    public const string MateriaRequerida = "La materia de la serie es obligatoria.";

    /// <summary>La materia de la serie excede la longitud admitida.</summary>
    public const string MateriaDemasiadoLarga = "La materia no puede superar los 120 caracteres.";

    /// <summary>La serie indicada no existe.</summary>
    public const string SerieNoEncontrada = "La serie solicitada no existe.";

    /// <summary>El área no está activa para recibir la serie.</summary>
    public const string EspacioNoDisponible = "El área no está disponible para reservas: se encuentra inactiva.";

    /// <summary>La modalidad del área no admite series semestrales.</summary>
    public const string ModalidadIncompatible = "La modalidad de préstamo del área no admite series semestrales.";

    /// <summary>El área indicada no existe.</summary>
    public const string EspacioNoEncontrado = "El área solicitada no existe.";
}
