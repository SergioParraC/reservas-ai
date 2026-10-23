namespace Backend.Domain.Exceptions;

/// <summary>
/// Incumplimiento de una regla de negocio que representa un conflicto con el estado actual
/// del recurso (unicidad o límite de aforo). La API lo traduce a <c>409 Conflict</c>.
/// </summary>
public sealed class ConflictoException : DomainException
{
    /// <summary>Inicializa la excepción con el mensaje de negocio del conflicto.</summary>
    public ConflictoException(string message)
        : base(message)
    {
    }
}
