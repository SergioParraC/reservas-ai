using System.Globalization;
using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Identity;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.IntegrationTests.Autenticacion;

/// <summary>
/// Verifica el adaptador de Identity contra SQL Server 2022: mapeo de cuentas,
/// bloqueo por intentos fallidos, reinicio del contador y políticas de alta.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class IdentityServiceTests
{
    private const string Contrasena = "Clave.Segura.2026";
    private const string Correo = "ana@uninpahu.edu.co";

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public IdentityServiceTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task VerificarCredencialesDevuelveLaCuentaMapeadaSinDatosDeSeguridad()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("identity_servicio");
        await using var proveedor = await CrearProveedorAsync(cadena);
        var usuario = await CrearUsuarioAsync(proveedor, TipoActor.Administrativo, RolesAplicacion.Administrativo);

        await using var alcance = proveedor.CreateAsyncScope();
        var servicio = alcance.ServiceProvider.GetRequiredService<IIdentityService>();

        var resultado = await servicio.VerificarCredencialesAsync(new CredencialesLocales(Correo, Contrasena));

        Assert.True(resultado.SonValidas);
        Assert.NotNull(resultado.Cuenta);
        Assert.Equal(usuario.Id, resultado.Cuenta.Id);
        Assert.Equal(Correo, resultado.Cuenta.CorreoInstitucional);
        Assert.Equal(TipoActor.Administrativo, resultado.Cuenta.TipoActor);
        Assert.True(resultado.Cuenta.Activo);
        Assert.False(resultado.Cuenta.Bloqueada);
    }

    [DockerFact]
    public async Task ElUmbralDeIntentosBloqueaLaCuentaYElAccesoExitosoReiniciaElContador()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("identity_bloqueo");
        await using var proveedor = await CrearProveedorAsync(cadena, maxIntentosFallidos: 3);
        await CrearUsuarioAsync(proveedor, TipoActor.Administrativo, RolesAplicacion.Administrativo);

        await using var alcance = proveedor.CreateAsyncScope();
        var servicio = alcance.ServiceProvider.GetRequiredService<IIdentityService>();
        var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        for (var intento = 0; intento < 3; intento++)
        {
            var fallo = await servicio.VerificarCredencialesAsync(new CredencialesLocales(Correo, "Clave.Incorrecta.1"));
            Assert.Equal(EstadoCredenciales.CredencialesInvalidas, fallo.Estado);
        }

        var bloqueada = await servicio.VerificarCredencialesAsync(new CredencialesLocales(Correo, Contrasena));
        Assert.Equal(EstadoCredenciales.CuentaBloqueada, bloqueada.Estado);

        // Desbloqueo administrativo: el soporte limpia la fecha de bloqueo.
        var cuenta = await usuarios.FindByEmailAsync(Correo);
        Assert.NotNull(cuenta);
        await usuarios.SetLockoutEndDateAsync(cuenta, null);

        var exitosa = await servicio.VerificarCredencialesAsync(new CredencialesLocales(Correo, Contrasena));
        Assert.True(exitosa.SonValidas);

        var recargada = await usuarios.FindByEmailAsync(Correo);
        Assert.NotNull(recargada);
        Assert.Equal(0, await usuarios.GetAccessFailedCountAsync(recargada));
    }

    [DockerFact]
    public async Task ElAlmacenRechazaUnCorreoDuplicadoYUnaContrasenaDebil()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("identity_politicas");
        await using var proveedor = await CrearProveedorAsync(cadena);
        await CrearUsuarioAsync(proveedor, TipoActor.Administrativo, RolesAplicacion.Administrativo);

        await using var alcance = proveedor.CreateAsyncScope();
        var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        var duplicado = Usuario.Crear("Otra Ana", Correo, TipoActor.Docente, DateTimeOffset.UtcNow);
        var resultadoDuplicado = await usuarios.CreateAsync(duplicado, Contrasena);

        Assert.False(resultadoDuplicado.Succeeded);

        var debil = Usuario.Crear("Clave Débil", "debil@uninpahu.edu.co", TipoActor.Estudiante, DateTimeOffset.UtcNow);
        var resultadoDebil = await usuarios.CreateAsync(debil, "clave");

        Assert.False(resultadoDebil.Succeeded);
    }

    private static async Task<Usuario> CrearUsuarioAsync(ServiceProvider proveedor, TipoActor tipoActor, string rol)
    {
        await using var alcance = proveedor.CreateAsyncScope();
        var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        var usuario = Usuario.Crear("Ana Administrativa", Correo, tipoActor, DateTimeOffset.UtcNow);
        var creacion = await usuarios.CreateAsync(usuario, Contrasena);

        Assert.True(creacion.Succeeded, string.Join("; ", creacion.Errors.Select(error => $"{error.Code}: {error.Description}")));

        var asignacion = await usuarios.AddToRoleAsync(usuario, rol);
        Assert.True(asignacion.Succeeded);

        return usuario;
    }

    private static async Task<ServiceProvider> CrearProveedorAsync(string cadena, int maxIntentosFallidos = 5)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = cadena,
                ["Identity:Lockout:MaxIntentosFallidos"] = maxIntentosFallidos.ToString(CultureInfo.InvariantCulture),
                ["Jwt:Key"] = "clave-de-pruebas-suficientemente-larga-para-hmac-256",
                ["Jwt:Issuer"] = "https://api.reservas-espacios.edu",
                ["Jwt:Audience"] = "reservas-espacios-spa"
            })
            .Build();

        var servicios = new ServiceCollection();
        servicios.AddSingleton<IConfiguration>(configuracion);
        servicios.AddLogging(registro => registro.SetMinimumLevel(LogLevel.Warning));
        servicios.AddInfrastructure(configuracion);

        var proveedor = servicios.BuildServiceProvider();

        await using var alcance = proveedor.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await contexto.Database.MigrateAsync();
        await alcance.ServiceProvider.GetRequiredService<IInicializadorIdentity>().InicializarAsync();

        return proveedor;
    }
}
