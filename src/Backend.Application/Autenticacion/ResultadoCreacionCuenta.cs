namespace Backend.Application.Autenticacion;

/// <summary>Datos de entrada del autorregistro de una cuenta local.</summary>
public sealed record RegistroCuentaLocal(string NombreCompleto, string CorreoInstitucional, string Contrasena);

/// <summary>
/// Resultado del alta en el almacén de identidad. Traduce los errores de Identity a
/// mensajes de negocio sin exponer códigos internos ni hashes.
/// </summary>
public sealed record ResultadoCreacionCuenta(
    CuentaUsuario? Cuenta,
    string? Rol,
    IReadOnlyList<string> Errores,
    bool CorreoDuplicado)
{
    /// <summary>Indica si la cuenta se creó y recibió rol.</summary>
    public bool Exitoso => Cuenta is not null && string.IsNullOrEmpty(Rol) is false && Errores.Count == 0;

    /// <summary>Crea el resultado exitoso del alta.</summary>
    public static ResultadoCreacionCuenta Exito(CuentaUsuario cuenta, string rol) =>
        new(cuenta, rol, [], false);

    /// <summary>Crea el resultado de un correo ya registrado.</summary>
    public static ResultadoCreacionCuenta Duplicado() =>
        new(null, null, [Domain.Exceptions.MensajesIdentidad.CorreoYaRegistrado], true);

    /// <summary>Crea el resultado de un alta rechazada por el almacén de identidad.</summary>
    public static ResultadoCreacionCuenta Fallo(IReadOnlyList<string> errores) =>
        new(null, null, errores, false);
}

/// <summary>Resultado de una operación de identidad sin valor de retorno.</summary>
public sealed record ResultadoOperacionIdentidad(bool Exitoso, IReadOnlyList<string> Errores)
{
    /// <summary>Crea el resultado exitoso.</summary>
    public static ResultadoOperacionIdentidad Exito() => new(true, []);

    /// <summary>Crea el resultado fallido con el detalle de los errores.</summary>
    public static ResultadoOperacionIdentidad Fallo(IReadOnlyList<string> errores) => new(false, errores);
}
