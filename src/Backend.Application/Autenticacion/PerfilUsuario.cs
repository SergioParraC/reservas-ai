using Backend.Domain.Enums;

namespace Backend.Application.Autenticacion;

/// <summary>
/// Perfil institucional que la API puede exponer. Contiene solo datos de negocio:
/// nombre, correo, tipo de actor y roles; nunca hash, sellos de seguridad ni estado de bloqueo.
/// </summary>
public sealed record PerfilUsuario(
    Guid Id,
    string NombreCompleto,
    string CorreoInstitucional,
    TipoActor TipoActor,
    IReadOnlyCollection<string> Roles,
    bool Vinculado = false);
