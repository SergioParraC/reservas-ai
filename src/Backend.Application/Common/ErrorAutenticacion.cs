namespace Backend.Application.Common;

/// <summary>
/// Clases de fallo que un caso de uso puede devolver. La capa de presentación las traduce
/// al catálogo de <c>type</c> estables del contrato RFC 7807.
/// </summary>
public enum ErrorAutenticacion
{
    /// <summary>Sin error: la operación fue exitosa.</summary>
    Ninguno = 0,

    /// <summary>Credenciales inválidas o cuenta inexistente (mismo mensaje genérico).</summary>
    CredencialesInvalidas = 1,

    /// <summary>La cuenta está deshabilitada.</summary>
    CuentaInactiva = 2,

    /// <summary>La cuenta está bloqueada temporalmente por intentos fallidos.</summary>
    CuentaBloqueada = 3,

    /// <summary>No existe perfil habilitado para la identidad autenticada.</summary>
    PerfilInexistente = 4,

    /// <summary>La identidad no tiene un rol habilitado para operar.</summary>
    SinRolHabilitado = 5,

    /// <summary>El token presentado expiró.</summary>
    SesionExpirada = 6,

    /// <summary>La sesión fue revocada.</summary>
    SesionRevocada = 7,

    /// <summary>El token no tiene firma, emisor o audiencia válidos.</summary>
    TokenInvalido = 8,

    /// <summary>El proveedor de identidad federado no está disponible.</summary>
    ProveedorIdentidadNoDisponible = 9
}
