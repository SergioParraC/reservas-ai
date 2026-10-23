using System.Data;
using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Identity;

/// <summary>
/// Adaptador de ASP.NET Core Identity. Es la única clase que conoce <see cref="UserManager{TUser}"/>
/// y <see cref="RoleManager{TRole}"/>; hacia la capa de aplicación solo expone
/// <see cref="CuentaUsuario"/> y <see cref="ResultadoCredenciales"/>.
/// </summary>
public sealed class IdentityService : IIdentityService
{
    /// <summary>Usuario ficticio usado para igualar el tiempo de una verificación fallida.</summary>
    private static readonly Usuario UsuarioSenuelo = Usuario.Crear(
        "Verificación señuelo",
        "senuelo@invalid.local",
        Backend.Domain.Enums.TipoActor.Estudiante,
        DateTimeOffset.UnixEpoch);

    /// <summary>Hash de referencia que se verifica cuando el correo no existe.</summary>
    private static readonly string HashSenuelo =
        new PasswordHasher<Usuario>().HashPassword(UsuarioSenuelo, "Clave.Senuelo.2026");

    private readonly UserManager<Usuario> _usuarios;
    private readonly ApplicationDbContext _contexto;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el adaptador con el almacén de usuarios, la unidad de trabajo y el reloj.</summary>
    public IdentityService(UserManager<Usuario> usuarios, ApplicationDbContext contexto, IRelojSistema reloj)
    {
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <inheritdoc />
    public async Task<CuentaUsuario?> BuscarPorCorreoAsync(string correoInstitucional, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correoInstitucional))
        {
            return null;
        }

        var usuario = await _usuarios.FindByEmailAsync(correoInstitucional.Trim());

        return usuario is null ? null : Mapear(usuario);
    }

    /// <inheritdoc />
    public async Task<CuentaUsuario?> BuscarPorIdAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());

        return usuario is null ? null : Mapear(usuario);
    }

    /// <inheritdoc />
    public async Task<CuentaUsuario?> BuscarPorIdentificadorFederadoAsync(string identificadorFederado, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identificadorFederado))
        {
            return null;
        }

        var buscado = identificadorFederado.Trim();
        var usuario = await _usuarios.Users.FirstOrDefaultAsync(candidato => candidato.IdentificadorFederado == buscado);

        return usuario is null ? null : Mapear(usuario);
    }

    /// <inheritdoc />
    public Task<int> ContarCuentasAsync(CancellationToken cancellationToken = default) =>
        _contexto.Users.CountAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken = default) =>
        (from usuario in _contexto.Users
         join asignacion in _contexto.UserRoles on usuario.Id equals asignacion.UserId
         join rol in _contexto.Roles on asignacion.RoleId equals rol.Id
         where rol.Name == RolesAplicacion.Administrativo && usuario.Activo
         select usuario.Id).CountAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ResultadoCreacionCuenta> RegistrarCuentaLocalAsync(
        RegistroCuentaLocal registro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);

        var estrategia = _contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            // La estrategia de reintento exige que la transacción sea la unidad reintentable.
            _contexto.ChangeTracker.Clear();

            await using var transaccion = await _contexto.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                // El conteo y el alta comparten transacción serializable: dos registros concurrentes
                // sobre un sistema vacío no pueden producir dos administradores iniciales.
                var totalCuentas = await _contexto.Users.CountAsync(cancellationToken);
                var rol = totalCuentas == 0 ? RolesAplicacion.Administrativo : RolesAplicacion.Estudiante;
                var tipoActor = totalCuentas == 0 ? TipoActor.Administrativo : TipoActor.Estudiante;

                var usuario = Usuario.Crear(registro.NombreCompleto, registro.CorreoInstitucional, tipoActor, _reloj.Ahora);
                var creacion = await _usuarios.CreateAsync(usuario, registro.Contrasena);

                if (!creacion.Succeeded)
                {
                    await transaccion.RollbackAsync(cancellationToken);

                    return creacion.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName")
                        ? ResultadoCreacionCuenta.Duplicado()
                        : ResultadoCreacionCuenta.Fallo(TraducirErrores(creacion));
                }

                var asignacion = await _usuarios.AddToRoleAsync(usuario, rol);

                if (!asignacion.Succeeded)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return ResultadoCreacionCuenta.Fallo(TraducirErrores(asignacion));
                }

                await transaccion.CommitAsync(cancellationToken);

                return ResultadoCreacionCuenta.Exito(Mapear(usuario), rol);
            }
            catch (DbUpdateException)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return ResultadoCreacionCuenta.Duplicado();
            }
            catch (SqlException excepcion) when (excepcion.Number is 1205 or 2601 or 2627)
            {
                // Interbloqueo o índice único de la carrera de registro: se informa como conflicto.
                await transaccion.RollbackAsync(cancellationToken);
                return ResultadoCreacionCuenta.Duplicado();
            }
        });
    }

    /// <inheritdoc />
    public async Task<ResultadoOperacionIdentidad> CambiarRolAsync(
        Guid usuarioId,
        string rol,
        CancellationToken cancellationToken = default)
    {
        if (!EsRolSoportado(rol))
        {
            return ResultadoOperacionIdentidad.Fallo([$"El rol '{rol}' no está soportado por la plataforma."]);
        }

        var estrategia = _contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();

            var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());

            if (usuario is null)
            {
                return ResultadoOperacionIdentidad.Fallo(["La cuenta indicada no existe."]);
            }

            await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var rolesActuales = await _usuarios.GetRolesAsync(usuario);

                foreach (var actual in rolesActuales.Where(actual => !string.Equals(actual, rol, StringComparison.OrdinalIgnoreCase)))
                {
                    var retiro = await _usuarios.RemoveFromRoleAsync(usuario, actual);

                    if (!retiro.Succeeded)
                    {
                        await transaccion.RollbackAsync(cancellationToken);
                        return ResultadoOperacionIdentidad.Fallo(TraducirErrores(retiro));
                    }
                }

                if (!rolesActuales.Contains(rol, StringComparer.OrdinalIgnoreCase))
                {
                    var asignacion = await _usuarios.AddToRoleAsync(usuario, rol);

                    if (!asignacion.Succeeded)
                    {
                        await transaccion.RollbackAsync(cancellationToken);
                        return ResultadoOperacionIdentidad.Fallo(TraducirErrores(asignacion));
                    }
                }

                usuario.SincronizarTipoActor(TipoActorDe(rol));
                var actualizacion = await _usuarios.UpdateAsync(usuario);

                if (!actualizacion.Succeeded)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return ResultadoOperacionIdentidad.Fallo(TraducirErrores(actualizacion));
                }

                await transaccion.CommitAsync(cancellationToken);

                return ResultadoOperacionIdentidad.Exito();
            }
            catch (DbUpdateException)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return ResultadoOperacionIdentidad.Fallo(["No fue posible actualizar el rol de la cuenta."]);
            }
        });
    }

    /// <inheritdoc />
    public async Task<ResultadoOperacionIdentidad> VincularIdentidadAsync(
        Guid usuarioId,
        string identificadorFederado,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());

        if (usuario is null)
        {
            return ResultadoOperacionIdentidad.Fallo(["La cuenta indicada no existe."]);
        }

        try
        {
            usuario.VincularIdentidadFederada(identificadorFederado);
            var actualizacion = await _usuarios.UpdateAsync(usuario);

            return actualizacion.Succeeded
                ? ResultadoOperacionIdentidad.Exito()
                : ResultadoOperacionIdentidad.Fallo(TraducirErrores(actualizacion));
        }
        catch (DomainException excepcion)
        {
            return ResultadoOperacionIdentidad.Fallo([excepcion.Message]);
        }
        catch (DbUpdateException)
        {
            return ResultadoOperacionIdentidad.Fallo([MensajesIdentidad.IdentidadYaVinculada]);
        }
    }

    /// <inheritdoc />
    public async Task<ResultadoOperacionIdentidad> DesvincularIdentidadAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());

        if (usuario is null)
        {
            return ResultadoOperacionIdentidad.Fallo(["La cuenta indicada no existe."]);
        }

        if (usuario.IdentificadorFederado is null)
        {
            return ResultadoOperacionIdentidad.Exito();
        }

        usuario.RetirarIdentidadFederada();
        var actualizacion = await _usuarios.UpdateAsync(usuario);

        return actualizacion.Succeeded
            ? ResultadoOperacionIdentidad.Exito()
            : ResultadoOperacionIdentidad.Fallo(TraducirErrores(actualizacion));
    }

    /// <inheritdoc />
    public async Task<ResultadoCredenciales> VerificarCredencialesAsync(CredencialesLocales credenciales, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credenciales);

        var usuario = await _usuarios.FindByEmailAsync(credenciales.CorreoInstitucional?.Trim() ?? string.Empty);

        if (usuario is null)
        {
            // Verificación señuelo: el tiempo de respuesta no debe delatar si el correo existe.
            _usuarios.PasswordHasher.VerifyHashedPassword(UsuarioSenuelo, HashSenuelo, credenciales.Contrasena ?? string.Empty);

            return ResultadoCredenciales.Fallo(EstadoCredenciales.CredencialesInvalidas);
        }

        if (await _usuarios.IsLockedOutAsync(usuario))
        {
            return ResultadoCredenciales.Fallo(EstadoCredenciales.CuentaBloqueada);
        }

        if (!await _usuarios.CheckPasswordAsync(usuario, credenciales.Contrasena ?? string.Empty))
        {
            await _usuarios.AccessFailedAsync(usuario);

            return ResultadoCredenciales.Fallo(EstadoCredenciales.CredencialesInvalidas);
        }

        if (!usuario.Activo)
        {
            return ResultadoCredenciales.Fallo(EstadoCredenciales.CuentaInactiva);
        }

        await _usuarios.ResetAccessFailedCountAsync(usuario);

        return ResultadoCredenciales.Exito(Mapear(usuario));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ObtenerRolesAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());

        if (usuario is null)
        {
            return [];
        }

        var roles = await _usuarios.GetRolesAsync(usuario);

        return roles.ToArray();
    }

    /// <inheritdoc />
    public async Task ActualizarUltimoAccesoAsync(Guid usuarioId, DateTimeOffset momento, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.FindByIdAsync(usuarioId.ToString());

        if (usuario is null)
        {
            return;
        }

        usuario.RegistrarAcceso(momento);
        var resultado = await _usuarios.UpdateAsync(usuario);

        if (!resultado.Succeeded)
        {
            var detalle = string.Join("; ", resultado.Errors.Select(error => $"{error.Code}: {error.Description}"));
            throw new InvalidOperationException($"No se pudo actualizar el último acceso del usuario '{usuarioId}': {detalle}");
        }
    }

    private CuentaUsuario Mapear(Usuario usuario) =>
        new(
            usuario.Id,
            usuario.NombreCompleto,
            usuario.Email ?? string.Empty,
            usuario.TipoActor,
            usuario.Activo,
            usuario.LockoutEnd is not null && usuario.LockoutEnd > _reloj.Ahora,
            usuario.IdentificadorFederado);

    private static bool EsRolSoportado(string? rol) =>
        string.Equals(rol, RolesAplicacion.Administrativo, StringComparison.OrdinalIgnoreCase)
        || string.Equals(rol, RolesAplicacion.Docente, StringComparison.OrdinalIgnoreCase)
        || string.Equals(rol, RolesAplicacion.Estudiante, StringComparison.OrdinalIgnoreCase);

    private static TipoActor TipoActorDe(string rol) =>
        string.Equals(rol, RolesAplicacion.Administrativo, StringComparison.OrdinalIgnoreCase)
            ? TipoActor.Administrativo
            : string.Equals(rol, RolesAplicacion.Docente, StringComparison.OrdinalIgnoreCase)
                ? TipoActor.Docente
                : TipoActor.Estudiante;

    private static IReadOnlyList<string> TraducirErrores(IdentityResult resultado) =>
        resultado.Errors.Select(error => error.Description).ToArray();
}
