namespace Backend.Domain.Exceptions;

/// <summary>
/// Excepción base para el incumplimiento de una regla de negocio del dominio.
/// Las capas exteriores la traducen al contrato RFC 7807 de la API.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Inicializa la excepción con el mensaje de negocio que describe la regla incumplida.</summary>
    public DomainException(string message)
        : base(message)
    {
    }

    /// <summary>Inicializa la excepción conservando la causa original.</summary>
    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
