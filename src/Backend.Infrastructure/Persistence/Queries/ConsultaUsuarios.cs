using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Usuarios.Dtos;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Queries;

/// <summary>
/// Proyección administrativa de las cuentas de Identity con <c>AsNoTracking</c>.
/// Nunca proyecta hashes, sellos de seguridad ni tokens.
/// </summary>
public sealed class ConsultaUsuarios : IConsultaUsuarios
{
    private const string Collation = "Latin1_General_CI_AI";

    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa la consulta con el contexto de lectura.</summary>
    public ConsultaUsuarios(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<PaginaResultado<UsuarioAdministrativo>> ListarAsync(
        FiltroUsuarios filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = _contexto.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Q))
        {
            var texto = filtro.Q.Trim();
            consulta = consulta.Where(usuario =>
                EF.Functions.Collate(usuario.NombreCompleto, Collation).Contains(texto)
                || (usuario.Email != null && EF.Functions.Collate(usuario.Email, Collation).Contains(texto)));
        }

        if (filtro.Vinculado is { } vinculado)
        {
            consulta = vinculado
                ? consulta.Where(usuario => usuario.IdentificadorFederado != null)
                : consulta.Where(usuario => usuario.IdentificadorFederado == null);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Rol))
        {
            var rol = filtro.Rol.Trim();
            consulta = from usuario in consulta
                       join asignacion in _contexto.UserRoles on usuario.Id equals asignacion.UserId
                       join rolEntidad in _contexto.Roles on asignacion.RoleId equals rolEntidad.Id
                       where rolEntidad.Name == rol
                       select usuario;
        }

        var total = await consulta.CountAsync(cancellationToken);

        var filas = await consulta
            .OrderBy(usuario => usuario.NombreCompleto)
            .Skip((filtro.Pagina - 1) * filtro.Tamano)
            .Take(filtro.Tamano)
            .Select(usuario => new
            {
                usuario.Id,
                usuario.NombreCompleto,
                Correo = usuario.Email ?? string.Empty,
                usuario.TipoActor,
                usuario.Activo,
                Vinculado = usuario.IdentificadorFederado != null,
                usuario.CreadoEn,
                usuario.UltimoAccesoEn,
                Rol = (from asignacion in _contexto.UserRoles
                       join rolEntidad in _contexto.Roles on asignacion.RoleId equals rolEntidad.Id
                       where asignacion.UserId == usuario.Id
                       select rolEntidad.Name).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var elementos = filas
            .Select(fila => new UsuarioAdministrativo(
                fila.Id,
                fila.NombreCompleto,
                fila.Correo,
                fila.Rol,
                fila.TipoActor,
                fila.Activo,
                fila.Vinculado,
                fila.CreadoEn,
                fila.UltimoAccesoEn))
            .ToArray();

        return new PaginaResultado<UsuarioAdministrativo>(elementos, total, filtro.Pagina, filtro.Tamano);
    }
}
