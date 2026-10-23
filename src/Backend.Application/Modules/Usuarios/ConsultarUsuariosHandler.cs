using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Usuarios.Dtos;

namespace Backend.Application.Modules.Usuarios;

/// <summary>Listado administrativo y paginado de las cuentas registradas.</summary>
public sealed class ConsultarUsuariosHandler
{
    private readonly IConsultaUsuarios _consulta;

    /// <summary>Inicializa el caso de uso con el puerto de lectura de cuentas.</summary>
    public ConsultarUsuariosHandler(IConsultaUsuarios consulta)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    /// <summary>Normaliza los filtros y ejecuta la consulta.</summary>
    public Task<Resultado<PaginaResultado<UsuarioAdministrativo>>> HandleAsync(
        FiltroUsuarios filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var rol = string.IsNullOrWhiteSpace(filtro.Rol) ? null : filtro.Rol.Trim();

        if (rol is not null
            && !string.Equals(rol, RolesAplicacion.Administrativo, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(rol, RolesAplicacion.Docente, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(rol, RolesAplicacion.Estudiante, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Resultado<PaginaResultado<UsuarioAdministrativo>>.Fallo(
                TipoFallo.Validacion,
                "El rol indicado no es válido.",
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["rol"] = ["El rol indicado no es válido."] }));
        }

        var normalizado = filtro with
        {
            Q = string.IsNullOrWhiteSpace(filtro.Q) ? null : filtro.Q.Trim(),
            Rol = rol,
            Pagina = Paginacion.NormalizarPagina(filtro.Pagina),
            Tamano = Paginacion.NormalizarTamano(filtro.Tamano)
        };

        return EjecutarAsync(normalizado, cancellationToken);
    }

    private async Task<Resultado<PaginaResultado<UsuarioAdministrativo>>> EjecutarAsync(
        FiltroUsuarios filtro,
        CancellationToken cancellationToken)
    {
        var pagina = await _consulta.ListarAsync(filtro, cancellationToken);

        return Resultado<PaginaResultado<UsuarioAdministrativo>>.Exito(pagina);
    }
}
