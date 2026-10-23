using Backend.Application.Common;
using Backend.Application.Interfaces;

namespace Backend.Application.Autenticacion;

/// <summary>
/// Caso de uso de inicio de sesión con credenciales locales. Orquesta la verificación
/// de credenciales, el tratamiento del bloqueo, la obtención de roles y la emisión del token.
/// </summary>
public sealed class LoginLocalHandler
{
    private readonly IIdentityService _identidad;
    private readonly ITokenService _tokens;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos.</summary>
    public LoginLocalHandler(IIdentityService identidad, ITokenService tokens, IRelojSistema reloj)
    {
        _identidad = identidad ?? throw new ArgumentNullException(nameof(identidad));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Ejecuta el inicio de sesión local.</summary>
    public async Task<ResultadoAutenticacion> HandleAsync(CredencialesLocales credenciales, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credenciales);

        var verificacion = await _identidad.VerificarCredencialesAsync(credenciales, cancellationToken);

        var error = Traducir(verificacion.Estado);

        if (error != ErrorAutenticacion.Ninguno)
        {
            return ResultadoAutenticacion.Fallo(error);
        }

        if (verificacion.Cuenta is not { } cuenta)
        {
            // Defensa: un almacén que acredita credenciales debe devolver la cuenta asociada.
            return ResultadoAutenticacion.Fallo(ErrorAutenticacion.CredencialesInvalidas);
        }

        var roles = await _identidad.ObtenerRolesAsync(cuenta.Id, cancellationToken);
        var perfil = new PerfilUsuario(
            cuenta.Id,
            cuenta.NombreCompleto,
            cuenta.CorreoInstitucional,
            cuenta.TipoActor,
            roles,
            cuenta.IdentificadorFederado is not null);
        var emitidoEn = _reloj.Ahora;
        var token = _tokens.Emitir(perfil, emitidoEn);

        await _identidad.ActualizarUltimoAccesoAsync(cuenta.Id, emitidoEn, cancellationToken);

        return ResultadoAutenticacion.Exito(token, perfil);
    }

    private static ErrorAutenticacion Traducir(EstadoCredenciales estado) => estado switch
    {
        EstadoCredenciales.Validas => ErrorAutenticacion.Ninguno,
        EstadoCredenciales.CuentaBloqueada => ErrorAutenticacion.CuentaBloqueada,
        EstadoCredenciales.CuentaInactiva => ErrorAutenticacion.CuentaInactiva,
        _ => ErrorAutenticacion.CredencialesInvalidas
    };
}
