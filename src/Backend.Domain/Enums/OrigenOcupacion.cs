namespace Backend.Domain.Enums;

/// <summary>
/// Procedencia de una fila de la agenda del área, conservada para trazabilidad.
/// </summary>
public enum OrigenOcupacion
{
    /// <summary>La ocupación proviene de una serie semestral.</summary>
    Recurrente = 1,

    /// <summary>La ocupación proviene de una solicitud aprobada.</summary>
    Solicitud = 2
}
