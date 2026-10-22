using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Persistence;

/// <summary>Contrato de siembra idempotente de roles base y administrador inicial.</summary>
public interface IInicializadorIdentity
{
    /// <summary>Crea los roles base y el administrador inicial si no existen.</summary>
    Task InicializarAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Siembra idempotente del esquema de identidad: exactamente tres roles
/// (<c>Administrativo</c>, <c>Docente</c>, <c>Estudiante</c>) y un administrador inicial
/// configurado por <c>Seed:AdminEmail</c> y <c>Seed:AdminPassword</c>.
/// </summary>
public sealed class InicializadorIdentity : IInicializadorIdentity
{
    /// <summary>Rol del personal administrativo y de soporte.</summary>
    public const string RolAdministrativo = RolesAplicacion.Administrativo;

    /// <summary>Rol de los docentes.</summary>
    public const string RolDocente = RolesAplicacion.Docente;

    /// <summary>Rol de los estudiantes.</summary>
    public const string RolEstudiante = RolesAplicacion.Estudiante;

    /// <summary>Roles oficiales del dominio; no se admiten otros.</summary>
    public static readonly IReadOnlyList<string> RolesBase = [RolesAplicacion.Administrativo, RolesAplicacion.Docente, RolesAplicacion.Estudiante];

    private const string NombreAdministradorPorDefecto = "Administrador del Sistema";

    private readonly RoleManager<Rol> _roles;
    private readonly UserManager<Usuario> _usuarios;
    private readonly IConfiguration _configuracion;
    private readonly IRelojSistema _reloj;
    private readonly ILogger<InicializadorIdentity> _registro;

    /// <summary>Inicializa el sembrador con los servicios de identidad y la configuración.</summary>
    public InicializadorIdentity(
        RoleManager<Rol> roles,
        UserManager<Usuario> usuarios,
        IConfiguration configuracion,
        IRelojSistema reloj,
        ILogger<InicializadorIdentity> registro)
    {
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _usuarios = usuarios ?? throw new ArgumentNullException(nameof(usuarios));
        _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
    }

    /// <inheritdoc />
    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        await SembrarRolesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await SembrarAdministradorAsync(cancellationToken);
    }

    private async Task SembrarRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var nombre in RolesBase)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await _roles.RoleExistsAsync(nombre))
            {
                continue;
            }

            try
            {
                var resultado = await _roles.CreateAsync(new Rol(Guid.NewGuid(), nombre));

                if (!resultado.Succeeded)
                {
                    throw new InvalidOperationException($"No se pudo crear el rol '{nombre}': {Describir(resultado)}");
                }

                _registro.LogInformation("Rol base '{Rol}' creado.", nombre);
            }
            catch (DbUpdateException)
            {
                // Otra instancia pudo crear el rol entre la comprobación y la inserción.
                if (!await _roles.RoleExistsAsync(nombre))
                {
                    throw;
                }

                _registro.LogInformation("El rol base '{Rol}' ya existía.", nombre);
            }
        }
    }

    private async Task SembrarAdministradorAsync(CancellationToken cancellationToken)
    {
        var correo = _configuracion["Seed:AdminEmail"];
        var contrasena = _configuracion["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(contrasena))
        {
            _registro.LogInformation(
                "Seed del administrador omitido: configure Seed:AdminEmail y Seed:AdminPassword en User Secrets o variables de entorno.");
            return;
        }

        var administrador = await _usuarios.FindByEmailAsync(correo);

        if (administrador is null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nombre = _configuracion["Seed:AdminNombre"];
            var usuario = Usuario.Crear(
                string.IsNullOrWhiteSpace(nombre) ? NombreAdministradorPorDefecto : nombre,
                correo,
                TipoActor.Administrativo,
                _reloj.Ahora);

            var creacion = await _usuarios.CreateAsync(usuario, contrasena);

            if (creacion.Succeeded)
            {
                administrador = usuario;
                _registro.LogInformation("Administrador inicial '{Correo}' creado.", correo);
            }
            else
            {
                // Otra instancia pudo crear la cuenta entre la consulta y la inserción.
                administrador = await _usuarios.FindByEmailAsync(correo);

                if (administrador is null)
                {
                    throw new InvalidOperationException($"No se pudo crear el administrador inicial: {Describir(creacion)}");
                }

                _registro.LogInformation("El administrador inicial '{Correo}' ya existía.", correo);
            }
        }

        if (await _usuarios.IsInRoleAsync(administrador, RolAdministrativo))
        {
            return;
        }

        var asignacion = await _usuarios.AddToRoleAsync(administrador, RolAdministrativo);

        if (!asignacion.Succeeded)
        {
            throw new InvalidOperationException($"No se pudo asignar el rol '{RolAdministrativo}' al administrador: {Describir(asignacion)}");
        }
    }

    private static string Describir(IdentityResult resultado) =>
        string.Join("; ", resultado.Errors.Select(error => $"{error.Code}: {error.Description}"));
}
