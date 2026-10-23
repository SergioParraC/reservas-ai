namespace Backend.Domain.Enums;

/// <summary>
/// Cadencia con la que una serie semestral genera sus ocurrencias.
/// </summary>
public enum TipoRecurrencia
{
    /// <summary>Una ocurrencia cada siete días en el día de la semana indicado.</summary>
    Semanal = 1,

    /// <summary>Una ocurrencia cada catorce días desde la primera fecha.</summary>
    Bisemanal = 2,

    /// <summary>Ocurrencias en las fechas indicadas explícitamente por el docente.</summary>
    FechasFijas = 3
}
