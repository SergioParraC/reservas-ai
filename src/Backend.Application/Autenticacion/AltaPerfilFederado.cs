using Backend.Domain.Enums;

namespace Backend.Application.Autenticacion;

/// <summary>
/// Datos necesarios para dar de alta un perfil institucional vinculado a una identidad federada.
/// La autorización del alta corresponde a la capa de presentación; este contrato solo transporta datos.
/// </summary>
public sealed record AltaPerfilFederado(
    string NombreCompleto,
    string CorreoInstitucional,
    string IdentificadorFederado,
    TipoActor TipoActor);
