namespace Backend.Application.Autenticacion;

/// <summary>Clases de resultado de la verificación de credenciales locales.</summary>
public enum EstadoCredenciales
{
    /// <summary>Las credenciales son válidas y la cuenta puede acceder.</summary>
    Validas = 0,

    /// <summary>El correo no existe o la contraseña es incorrecta (no se distingue cuál).</summary>
    CredencialesInvalidas = 1,

    /// <summary>La cuenta está deshabilitada.</summary>
    CuentaInactiva = 2,

    /// <summary>La cuenta está bloqueada temporalmente por intentos fallidos.</summary>
    CuentaBloqueada = 3
}
