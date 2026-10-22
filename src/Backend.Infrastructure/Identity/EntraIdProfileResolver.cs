using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Identity;

/// <summary>
/// Resuelve el perfil institucional de una identidad federada de Entra ID.
/// Reutiliza el perfil existente por <c>oid</c> o lo aprovisiona cuando el rol
/// institucional es académico (<c>Docente</c> o <c>Estudiante</c>).
/// Nunca crea ni modifica roles administrativos.
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

        var rolesInstitucionales = NormalizarRoles(identidad.RolesInstitucionales);

        if (rolesInstitucionales.Count == 0)
        {
            return ResultadoPerfilFederado.Fallo(ErrorAutenticacion.SinRolHabilitado);
        }

        var cuenta = await _identidad.BuscarPorIdentificadorFederadoAsync(identidad.Oid, cancellationToken);

        if (cuenta is null)
        {
            var rol = rolesInstitucionales[0];
            var tipoActor = string.Equals(rol, RolesAplicacion.Docente, StringComparison.OrdinalIgnoreCase)
                ? TipoActor.Docente
                : TipoActor.Estudiante;

            cuenta = await _identidad.CrearPerfilFederadoAsync(
                new AltaPerfilFederado(identidad.NombreCompleto, identidad.CorreoInstitucional, identidad.Oid, tipoActor),
                rol,
                cancellationToken);

            if (cuenta is null)
            {
                return ResultadoPerfilFederado.Fallo(ErrorAutenticacion.PerfilInexistente);
            }

            _registro.LogInformation(
                "Perfil federado aprovisionado: usuario={UsuarioId} rol={Rol} oid={Oid}",
                cuenta.Id,
                rol,
                identidad.Oid);
        }
        else if (!cuenta.Activo)
        {
            return ResultadoPerfilFederado.Fallo(ErrorAutenticacion.PerfilInexistente);
        }

        var rolesLocales = await _identidad.ObtenerRolesAsync(cuenta.Id, cancellationToken);
        var rolesEfectivos = rolesLocales
            .Intersect(rolesInstitucionales, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rolesEfectivos.Length == 0)
        {
            return ResultadoPerfilFederado.Fallo(ErrorAutenticacion.SinRolHabilitado);
        }

        await _identidad.ActualizarUltimoAccesoAsync(cuenta.Id, _reloj.Ahora, cancellationToken);

        _registro.LogInformation(
            "Acceso federado autorizado: usuario={UsuarioId} rol={Rol} oid={Oid}",
            cuenta.Id,
            string.Join(",", rolesEfectivos),
            identidad.Oid);

        return ResultadoPerfilFederado.Exito(
            new PerfilUsuario(cuenta.Id, cuenta.NombreCompleto, cuenta.CorreoInstitucional, cuenta.TipoActor, rolesEfectivos));
    }

    private static List<string> NormalizarRoles(IReadOnlyCollection<string> rolesInstitucionales)
    {
        var normalizados = new List<string>(2);

        if (rolesInstitucionales.Any(rol => string.Equals(rol, RolesAplicacion.Docente, StringComparison.OrdinalIgnoreCase)))
        {
            normalizados.Add(RolesAplicacion.Docente);
        }

        if (rolesInstitucionales.Any(rol => string.Equals(rol, RolesAplicacion.Estudiante, StringComparison.OrdinalIgnoreCase)))
        {
            normalizados.Add(RolesAplicacion.Estudiante);
        }

        return normalizados;
    }
}
