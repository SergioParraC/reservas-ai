namespace Backend.Domain.Enums;

/// <summary>
/// Estado del ciclo de vida de una cuenta institucional.
/// </summary>
public enum EstadoUsuario
{
    /// <summary>La cuenta puede iniciar sesión si sus credenciales son válidas.</summary>
    Activo = 1,

    /// <summary>La cuenta fue deshabilitada y no admite nuevos accesos.</summary>
    Inactivo = 2,

    /// <summary>La cuenta está bloqueada temporalmente por intentos fallidos consecutivos.</summary>
    Bloqueado = 3
}
