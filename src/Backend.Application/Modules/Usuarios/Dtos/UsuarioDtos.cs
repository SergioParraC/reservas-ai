using Backend.Domain.Enums;

namespace Backend.Application.Modules.Usuarios.Dtos;

/// <summary>Cuenta expuesta en la consola administrativa, sin datos de seguridad.</summary>
public sealed record UsuarioAdministrativo(
    Guid Id,
    string NombreCompleto,
    string CorreoInstitucional,
    string? Rol,
    TipoActor TipoActor,
    bool Activo,
    bool Vinculado,
    DateTimeOffset CreadoEn,
    DateTimeOffset? UltimoAccesoEn);

/// <summary>Filtros de la consola de usuarios.</summary>
public sealed record FiltroUsuarios(string? Q, string? Rol, bool? Vinculado, int Pagina, int Tamano);

/// <summary>Respuesta del autorregistro; no incluye token de acceso.</summary>
public sealed record ResultadoRegistroUsuario(
    Guid Id,
    string NombreCompleto,
    string CorreoInstitucional,
    string Rol);

/// <summary>Estado del vínculo federado de la cuenta autenticada.</summary>
public sealed record EstadoVinculacion(bool Vinculado, string CorreoInstitucional);

/// <summary>Resultado de un cambio de rol administrativo.</summary>
public sealed record ResultadoCambioRol(
    Guid Id,
    string CorreoInstitucional,
    string Rol,
    bool RequiereRenovacion);
