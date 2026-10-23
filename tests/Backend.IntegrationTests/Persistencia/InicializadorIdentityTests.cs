using Backend.Domain.Enums;
using Backend.Infrastructure;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Identity;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.IntegrationTests.Persistencia;

/// <summary>
/// Verifica que la siembra de identidad es idempotente: ejecutarla dos veces
/// no duplica roles ni el administrador inicial.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class InicializadorIdentityTests
{
    private const string CorreoAdministrador = "admin@uninpahu.edu.co";
    private const string ContrasenaAdministrador = "Clave.Segura.2026";

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public InicializadorIdentityTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task EjecutarLaSemillaDosVecesNoDuplicaRolesNiAdministrador()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("semilla");
        await using var proveedor = CrearProveedor(cadena);

        await using (var alcance = proveedor.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await contexto.Database.MigrateAsync();
        }

        await using (var alcance = proveedor.CreateAsyncScope())
        {
            var inicializador = alcance.ServiceProvider.GetRequiredService<IInicializadorIdentity>();
            await inicializador.InicializarAsync();
            await inicializador.InicializarAsync();
        }

        await using (var alcance = proveedor.CreateAsyncScope())
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var roles = await contexto.Roles.Select(rol => rol.Name).ToListAsync();
            Assert.Equivalent(InicializadorIdentity.RolesBase, roles);

            var administrador = await contexto.Users.SingleAsync();
            Assert.Equal(CorreoAdministrador, administrador.Email);
            Assert.Equal(TipoActor.Administrativo, administrador.TipoActor);
            Assert.True(administrador.Activo);

            var rolesDelAdministrador = await (
                    from usuarioRol in contexto.UserRoles
                    join rol in contexto.Roles on usuarioRol.RoleId equals rol.Id
                    where usuarioRol.UserId == administrador.Id
                    select rol.Name)
                .ToListAsync();

            Assert.Equal(InicializadorIdentity.RolAdministrativo, Assert.Single(rolesDelAdministrador));
        }
    }

    private static ServiceProvider CrearProveedor(string cadena)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = cadena,
                ["Seed:AdminEmail"] = CorreoAdministrador,
                ["Seed:AdminPassword"] = ContrasenaAdministrador,
                ["Seed:AdminNombre"] = "Administrador de Pruebas"
            })
            .Build();

        var servicios = new ServiceCollection();
        servicios.AddSingleton<IConfiguration>(configuracion);
        servicios.AddLogging(registro => registro.SetMinimumLevel(LogLevel.Warning));
        servicios.AddInfrastructure(configuracion);

        return servicios.BuildServiceProvider();
    }
}
