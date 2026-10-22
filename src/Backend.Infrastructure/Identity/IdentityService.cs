using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Domain.Exceptions;
using Backend.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
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
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el adaptador con el almacén de usuarios y el reloj del sistema.</summary>
    public IdentityService(UserManager<Usuario> usuarios, IRelojSistema reloj)
    {
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
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
    public async Task<CuentaUsuario?> CrearPerfilFederadoAsync(AltaPerfilFederado alta, string rol, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alta);

        if (!RolesAplicacion.EsRolAcademico(rol))
        {
            throw new DomainException($"El rol '{rol}' no es un rol académico y no puede asignarse a un perfil federado.");
        }

        var usuario = Usuario.Crear(alta.NombreCompleto, alta.CorreoInstitucional, alta.TipoActor, _reloj.Ahora);
        usuario.VincularIdentidadFederada(alta.IdentificadorFederado);

        // Cuenta federada: se crea sin contraseña local.
        var creacion = await _usuarios.CreateAsync(usuario);

        if (!creacion.Succeeded)
        {
            var existente = await BuscarPorIdentificadorFederadoAsync(alta.IdentificadorFederado, cancellationToken);

            if (existente is not null)
            {
                return existente;
            }

            var detalle = string.Join("; ", creacion.Errors.Select(error => $"{error.Code}: {error.Description}"));
            throw new InvalidOperationException($"No se pudo crear el perfil federado '{alta.IdentificadorFederado}': {detalle}");
        }

        var asignacion = await _usuarios.AddToRoleAsync(usuario, rol);

        if (!asignacion.Succeeded)
        {
            var detalle = string.Join("; ", asignacion.Errors.Select(error => $"{error.Code}: {error.Description}"));
            throw new InvalidOperationException($"No se pudo asignar el rol '{rol}' al perfil federado '{alta.IdentificadorFederado}': {detalle}");
        }

        return Mapear(usuario);
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
            usuario.LockoutEnd is not null && usuario.LockoutEnd > _reloj.Ahora);
}
