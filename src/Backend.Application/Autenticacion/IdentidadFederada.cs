namespace Backend.Application.Autenticacion;

/// <summary>
/// Notificaciones de una identidad federada ya validada por el esquema de Entra ID.
/// </summary>
public sealed record IdentidadFederada(
    string Oid,
    string NombreCompleto,
    string CorreoInstitucional,
    IReadOnlyCollection<string> RolesInstitucionales);
