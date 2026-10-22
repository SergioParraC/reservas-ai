namespace Backend.Domain.Exceptions;

/// <summary>
/// Constantes de los mensajes de negocio del dominio de identidad.
/// Se centralizan para que la API y las pruebas compartan el mismo texto sin duplicarlo.
/// </summary>
public static class MensajesIdentidad
{
    /// <summary>Mensaje genérico de credenciales inválidas: no revela si el correo existe.</summary>
    public const string CredencialesInvalidas = "El correo institucional o la contraseña no son válidos.";

    /// <summary>Mensaje genérico para cuentas deshabilitadas.</summary>
    public const string CuentaInactiva = "La cuenta no está habilitada para acceder al sistema.";

    /// <summary>Mensaje para cuentas bloqueadas temporalmente por intentos fallidos.</summary>
    public const string CuentaBloqueada = "La cuenta está bloqueada temporalmente por intentos fallidos. Intente nuevamente más tarde.";

    /// <summary>Mensaje cuando no existe un perfil habilitado para una identidad federada.</summary>
    public const string SinHabilitacionInstitucional = "La identidad no cuenta con un perfil habilitado en la plataforma. Contacte al área administrativa.";

    /// <summary>Mensaje cuando la intersección de roles locales y federados queda vacía.</summary>
    public const string SinRolHabilitado = "La identidad no tiene un rol habilitado para operar en la plataforma.";

    /// <summary>Mensaje cuando un perfil federado no tiene identificador institucional.</summary>
    public const string IdentificadorFederadoRequerido = "El identificador federado es obligatorio para un perfil institucional.";

    /// <summary>Mensaje cuando se intenta crear un usuario sin nombre completo.</summary>
    public const string NombreCompletoRequerido = "El nombre completo es obligatorio.";

    /// <summary>Mensaje cuando se intenta crear un usuario sin correo institucional.</summary>
    public const string CorreoInstitucionalRequerido = "El correo institucional es obligatorio.";
}
