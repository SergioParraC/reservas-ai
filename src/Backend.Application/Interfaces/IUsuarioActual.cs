namespace Backend.Application.Interfaces;

/// <summary>
/// Identidad del portador del token resuelta por la capa de presentación,
/// sin exponer tipos de los esquemas de autenticación.
/// </summary>
public interface IUsuarioActual
{
    /// <summary>Indica si la solicitud tiene una identidad autenticada.</summary>
    bool EstaAutenticado { get; }

    /// <summary>Identificador interno del usuario o <see langword="null"/> si no hay sesión.</summary>
    Guid? UsuarioId { get; }

    /// <summary>Correo institucional del portador o <see langword="null"/> si no hay sesión.</summary>
    string? CorreoInstitucional { get; }

    /// <summary>Roles efectivos concedidos al portador.</summary>
    IReadOnlyCollection<string> Roles { get; }
}
