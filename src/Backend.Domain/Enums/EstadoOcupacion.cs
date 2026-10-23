namespace Backend.Domain.Enums;

/// <summary>
/// Estado transaccional de una fila de la agenda del área. Solo las activas bloquean la franja.
/// </summary>
public enum EstadoOcupacion
{
    /// <summary>La franja está apartada y bloquea el área.</summary>
    Activa = 1,

    /// <summary>La franja fue liberada y ya no bloquea el área.</summary>
    Cancelada = 2
}
