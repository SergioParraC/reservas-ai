using Backend.Domain.Enums;

namespace Backend.Application.Autenticacion;

/// <summary>
/// Instantánea de una cuenta del almacén de identidad con los datos que la capa de aplicación
/// necesita para decidir un acceso. No expone contraseñas, hashes ni sellos de seguridad.
/// </summary>
public sealed record CuentaUsuario(
    Guid Id,
    string NombreCompleto,
    string CorreoInstitucional,
    TipoActor TipoActor,
    bool Activo,
    bool Bloqueada,
    string? IdentificadorFederado = null);
