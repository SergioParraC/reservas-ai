namespace Backend.Application.Autenticacion;

/// <summary>
/// Resultado de la verificación de credenciales locales: o la cuenta queda acreditada,
/// o se informa la clase de rechazo sin revelar si el correo existe.
/// </summary>
public sealed record ResultadoCredenciales
{
    private ResultadoCredenciales(EstadoCredenciales estado, CuentaUsuario? cuenta)
    {
        Estado = estado;
        Cuenta = cuenta;
    }

    /// <summary>Clase del resultado de la verificación.</summary>
    public EstadoCredenciales Estado { get; }

    /// <summary>Cuenta acreditada; solo presente cuando <see cref="Estado"/> es <see cref="EstadoCredenciales.Validas"/>.</summary>
    public CuentaUsuario? Cuenta { get; }

    /// <summary>Indica si las credenciales son válidas.</summary>
    public bool SonValidas => Estado == EstadoCredenciales.Validas;

    /// <summary>Crea un resultado acreditado con la cuenta verificada.</summary>
    public static ResultadoCredenciales Exito(CuentaUsuario cuenta)
    {
        ArgumentNullException.ThrowIfNull(cuenta);

        return new ResultadoCredenciales(EstadoCredenciales.Validas, cuenta);
    }

    /// <summary>Crea un resultado rechazado con la clase de fallo indicada.</summary>
    public static ResultadoCredenciales Fallo(EstadoCredenciales estado)
    {
        if (estado == EstadoCredenciales.Validas)
        {
            throw new ArgumentOutOfRangeException(nameof(estado), estado, "Un resultado rechazado requiere un estado distinto de Validas.");
        }

        return new ResultadoCredenciales(estado, null);
    }
}
