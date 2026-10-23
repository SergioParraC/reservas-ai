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

    /// <summary>Mensaje cuando un token federado no está vinculado a ninguna cuenta.</summary>
    public const string SinVinculacion = "La cuenta debe registrarse y vincular su identidad institucional desde la configuración del usuario.";

    /// <summary>Mensaje cuando el correo ya está registrado en la plataforma.</summary>
    public const string CorreoYaRegistrado = "El correo institucional ya está registrado en la plataforma.";

    /// <summary>Mensaje cuando la identidad federada ya está asociada a otra cuenta.</summary>
    public const string IdentidadYaVinculada = "La identidad institucional ya está vinculada a otra cuenta.";

    /// <summary>Mensaje cuando el correo del token no coincide con el de la cuenta local.</summary>
    public const string CorreoNoCoincide = "El correo de la cuenta institucional no coincide con el correo registrado.";

    /// <summary>Mensaje cuando la sesión ya es federada.</summary>
    public const string SesionYaFederada = "La cuenta ya accede con la identidad institucional.";

    /// <summary>Mensaje cuando se intenta crear un usuario sin nombre completo.</summary>
    public const string NombreCompletoRequerido = "El nombre completo es obligatorio.";

    /// <summary>Mensaje cuando se intenta crear un usuario sin correo institucional.</summary>
    public const string CorreoInstitucionalRequerido = "El correo institucional es obligatorio.";
}
