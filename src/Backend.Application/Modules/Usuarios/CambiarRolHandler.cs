using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Usuarios.Dtos;

namespace Backend.Application.Modules.Usuarios;

/// <summary>
/// Cambio administrativo del rol de una cuenta. Impide la autoedición y que el sistema
/// quede sin administradores activos; el rol local es la única fuente de autorización.
/// </summary>
public sealed class CambiarRolHandler
{
    private readonly IIdentityService _identidad;

    /// <summary>Inicializa el caso de uso con el almacén de identidad.</summary>
    public CambiarRolHandler(IIdentityService identidad)
    {
        _identidad = identidad ?? throw new ArgumentNullException(nameof(identidad));
    }

    /// <summary>Asigna el rol indicado a la cuenta objetivo.</summary>
    public async Task<Resultado<ResultadoCambioRol>> HandleAsync(
        Guid objetivoId,
        string rol,
        Guid portadorId,
        CancellationToken cancellationToken = default)
    {
        var rolNormalizado = NormalizarRol(rol);

        if (rolNormalizado is null)
        {
            return Resultado<ResultadoCambioRol>.Fallo(
                TipoFallo.Validacion,
                "El rol debe ser Administrativo, Docente o Estudiante.",
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["rol"] = ["El rol debe ser Administrativo, Docente o Estudiante."]
                });
        }

        if (objetivoId == portadorId)
        {
            return Resultado<ResultadoCambioRol>.Fallo(
                TipoFallo.Conflicto,
                "No puede cambiar su propio rol.");
        }

        var objetivo = await _identidad.BuscarPorIdAsync(objetivoId, cancellationToken);

        if (objetivo is null)
        {
            return Resultado<ResultadoCambioRol>.Fallo(TipoFallo.NoEncontrado, "La cuenta indicada no existe.");
        }

        var rolesActuales = await _identidad.ObtenerRolesAsync(objetivoId, cancellationToken);
        var eraAdministrador = rolesActuales.Any(candidato =>
            string.Equals(candidato, RolesAplicacion.Administrativo, StringComparison.OrdinalIgnoreCase));

        if (eraAdministrador && !string.Equals(rolNormalizado, RolesAplicacion.Administrativo, StringComparison.Ordinal))
        {
            var administradores = await _identidad.ContarAdministradoresActivosAsync(cancellationToken);

            if (administradores <= 1)
            {
                return Resultado<ResultadoCambioRol>.Fallo(
                    TipoFallo.Conflicto,
                    "El sistema debe conservar al menos un administrador activo.");
            }
        }

        var resultado = await _identidad.CambiarRolAsync(objetivoId, rolNormalizado, cancellationToken);

        return resultado.Exitoso
            ? Resultado<ResultadoCambioRol>.Exito(new ResultadoCambioRol(
                objetivo.Id,
                objetivo.CorreoInstitucional,
                rolNormalizado,
                RequiereRenovacion: true))
            : Resultado<ResultadoCambioRol>.Fallo(TipoFallo.Conflicto, string.Join(" ", resultado.Errores));
    }

    private static string? NormalizarRol(string? rol)
    {
        if (string.IsNullOrWhiteSpace(rol))
        {
            return null;
        }

        var buscado = rol.Trim();

        if (string.Equals(buscado, RolesAplicacion.Administrativo, StringComparison.OrdinalIgnoreCase))
        {
            return RolesAplicacion.Administrativo;
        }

        if (string.Equals(buscado, RolesAplicacion.Docente, StringComparison.OrdinalIgnoreCase))
        {
            return RolesAplicacion.Docente;
        }

        return string.Equals(buscado, RolesAplicacion.Estudiante, StringComparison.OrdinalIgnoreCase)
            ? RolesAplicacion.Estudiante
            : null;
    }
}
