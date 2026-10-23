namespace Backend.Domain.Exceptions;

/// <summary>
/// Constantes de los mensajes de negocio del calendario institucional. Se centralizan para que
/// la API traduzca cada mensaje a un <c>type</c> estable del contrato RFC 7807.
/// </summary>
public static class MensajesCalendario
{
    /// <summary>El nombre del periodo académico es obligatorio.</summary>
    public const string PeriodoNombreRequerido = "El nombre del periodo académico es obligatorio.";

    /// <summary>El nombre del periodo académico excede la longitud admitida.</summary>
    public const string PeriodoNombreDemasiadoLargo = "El nombre del periodo académico no puede superar los 40 caracteres.";

    /// <summary>La fecha de fin es anterior a la fecha de inicio.</summary>
    public const string PeriodoFechasInvertidas = "La fecha de fin del periodo debe ser posterior o igual a la fecha de inicio.";

    /// <summary>El periodo se cruza con otro ya registrado.</summary>
    public const string PeriodoSolapado = "El periodo se cruza con otro periodo registrado.";

    /// <summary>El periodo deja ocupación confirmada fuera de su nueva ventana.</summary>
    public const string PeriodoConOcupacion = "El periodo no puede modificarse: existen reservas, series o solicitudes dentro del rango actual.";

    /// <summary>El periodo indicado no existe.</summary>
    public const string PeriodoNoEncontrado = "El periodo académico solicitado no existe.";

    /// <summary>No existe un periodo académico activo.</summary>
    public const string SinPeriodoActivo = "No existe un periodo académico activo. La operación requiere un periodo vigente.";

    /// <summary>La franja solicitada queda fuera del periodo académico activo.</summary>
    public const string FueraDePeriodoAcademico = "La franja solicitada queda fuera del periodo académico activo.";

    /// <summary>La franja se cruza con una franja no operativa del calendario institucional.</summary>
    public const string FranjaNoOperativa = "La franja se cruza con una franja no operativa del calendario institucional.";

    /// <summary>La franja se cruza con una franja no operativa concreta del calendario.</summary>
    public const string FranjaNoOperativaConFecha = "La franja del {0} coincide con la franja no operativa '{1}'.";

    /// <summary>Los criterios de la franja no operativa son excluyentes.</summary>
    public const string FranjaCriteriosExcluyentes = "Debe indicar el día de la semana o la fecha específica, pero no ambos.";

    /// <summary>La franja no operativa requiere un criterio de aplicación.</summary>
    public const string FranjaCriterioRequerido = "Debe indicar el día de la semana o la fecha específica de la franja.";

    /// <summary>El día de la semana recibido no es válido.</summary>
    public const string FranjaDiaInvalido = "El día de la semana de la franja no es válido.";

    /// <summary>El fin de la franja no operativa es anterior al inicio.</summary>
    public const string FranjaHorasInvalidas = "La hora de fin de la franja no operativa debe ser posterior a la hora de inicio.";

    /// <summary>El motivo de la franja no operativa es obligatorio.</summary>
    public const string FranjaMotivoRequerido = "El motivo de la franja no operativa es obligatorio.";

    /// <summary>El motivo de la franja no operativa excede la longitud admitida.</summary>
    public const string FranjaMotivoDemasiadoLargo = "El motivo de la franja no operativa no puede superar los 120 caracteres.";

    /// <summary>Ya existe una franja no operativa idéntica.</summary>
    public const string FranjaDuplicada = "Ya existe una franja no operativa con el mismo criterio y horario.";

    /// <summary>La franja no operativa indicada no existe.</summary>
    public const string FranjaNoEncontrada = "La franja no operativa solicitada no existe.";
}
