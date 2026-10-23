using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Identity;

/// <summary>
/// Resuelve el perfil institucional de una identidad federada de Entra ID.
/// Solo acepta identidades previamente vinculadas por <c>oid</c> y aplica el rol
/// asignado en la plataforma: nunca aprovisiona cuentas ni deriva roles del token.
/// </summary>
public sealed class EntraIdProfileResolver : IEntraIdProfileResolver
{
    private readonly IIdentityService _identidad;
    private readonly IRelojSistema _reloj;
    private readonly ILogger<EntraIdProfileResolver> _registro;

    /// <summary>Inicializa el resolutor con el almacén de identidad y el reloj del sistema.</summary>
    public EntraIdProfileResolver(IIdentityService identidad, IRelojSistema reloj, ILogger<EntraIdProfileResolver> registro)
    {
        _identidad = identidad ?? throw new ArgumentNullException(nameof(identidad));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
    }

    /// <inheritdoc />
    public async Task<ResultadoPerfilFederado> ResolverAsync(IdentidadFederada identidad, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identidad);

        if (string.IsNullOrWhiteSpace(identidad.Oid))
        {
            _registro.LogWarning("Acceso federado rechazado: token sin identificador institucional.");
            return ResultadoPerfilFederado.Fallo(ErrorAutenticacion.SinVinculacion);
        }

        var cuenta = await _identidad.BuscarPorIdentificadorFederadoAsync(identidad.Oid, cancellationToken);

        if (cuenta is null)
        {
            _registro.LogWarning(
                "Acceso federado rechazado: identidad sin vínculo local. oid={Oid}",
                identidad.Oid);

            return ResultadoPerfilFederado.Fallo(ErrorAutenticacion.SinVinculacion);
        }

        if (!cuenta.Activo)
        {
            return ResultadoPerfilFederado.Fallo(ErrorAutenticacion.PerfilInexistente);
        }

        var rolesLocales = await _identidad.ObtenerRolesAsync(cuenta.Id, cancellationToken);

        if (rolesLocales.Count == 0)
        {
            _registro.LogWarning(
                "Acceso federado rechazado: cuenta sin rol vigente. usuario={UsuarioId} oid={Oid}",
                cuenta.Id,
                identidad.Oid);

            return ResultadoPerfilFederado.Fallo(ErrorAutenticacion.SinRolHabilitado);
        }

        await _identidad.ActualizarUltimoAccesoAsync(cuenta.Id, _reloj.Ahora, cancellationToken);

        _registro.LogInformation(
            "Acceso federado autorizado: usuario={UsuarioId} rol={Rol} oid={Oid}",
            cuenta.Id,
            string.Join(",", rolesLocales),
            identidad.Oid);

        return ResultadoPerfilFederado.Exito(
            new PerfilUsuario(
                cuenta.Id,
                cuenta.NombreCompleto,
                cuenta.CorreoInstitucional,
                cuenta.TipoActor,
                rolesLocales,
                Vinculado: true));
    }
}
