using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Backend.Application.Common;
using Backend.IntegrationTests.Infraestructura;
using Backend.Presentation.Api.Problemas;
using Backend.Presentation.Api.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Backend.IntegrationTests.Autenticacion;

/// <summary>
/// Verifica la autenticación híbrida: enrutamiento por emisor, validación federada,
/// aprovisionamiento acotado a roles académicos, rechazos RFC 7807 y auditoría.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class HybridAuthTests
{
    private static readonly RSA RsaEntra = RSA.Create(2048);
    private static readonly RsaSecurityKey ClaveEntra = new(RsaEntra) { KeyId = "clave-entra-pruebas" };

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public HybridAuthTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElTokenFederadoVinculadoResuelveElPerfilConElRolLocal()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("hibrida_docente");
        var capturador = new CapturadorRegistros();
        await using var fabrica = FabricaApiPruebas.Crear(
            cadena,
            constructor =>
            {
                ConfigurarEntraSimulado(constructor);
                constructor.ConfigureTestServices(servicios => servicios.AddSingleton<ILoggerProvider>(capturador));
            },
            ConfiguracionEntra());

        var oid = Guid.NewGuid().ToString();
        await CrearCuentaVinculadaAsync(fabrica, oid, RolesAplicacion.Docente, "docente@uninpahu.edu.co");

        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", EmitirTokenEntra(oid, "Docente de Pruebas", "docente@uninpahu.edu.co", ["Docente"]));

        var primera = await cliente.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);

        var segunda = await cliente.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);

        var perfil = await segunda.Content.ReadFromJsonAsync<PerfilRespuesta>();
        Assert.NotNull(perfil);
        Assert.Equal("docente@uninpahu.edu.co", perfil.CorreoInstitucional);
        Assert.Contains(RolesAplicacion.Docente, perfil.Roles);

        using var alcance = fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<Backend.Infrastructure.Persistence.ApplicationDbContext>();
        Assert.Equal(1, await contexto.Users.CountAsync(usuario => usuario.IdentificadorFederado == oid));

        Assert.Contains(
            capturador.Mensajes,
            mensaje => mensaje.Contains("Acceso federado autorizado", StringComparison.Ordinal) && mensaje.Contains(oid, StringComparison.Ordinal));
    }

    [DockerFact]
    public async Task ElTokenFederadoSinVinculacionDevuelve403YNoAprovisiona()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("hibrida_sin_vinculo");
        await using var fabrica = FabricaApiPruebas.Crear(cadena, ConfigurarEntraSimulado, ConfiguracionEntra());

        var oid = Guid.NewGuid().ToString();
        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            EmitirTokenEntra(oid, "Persona Externa", "externa@uninpahu.edu.co", ["Docente"]));

        var respuesta = await cliente.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<Problema>();
        Assert.NotNull(problema);
        Assert.EndsWith("/vinculacion-requerida", problema.Type, StringComparison.Ordinal);

        using var alcance = fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<Backend.Infrastructure.Persistence.ApplicationDbContext>();
        Assert.Equal(0, await contexto.Users.CountAsync(usuario => usuario.IdentificadorFederado == oid));
    }

    [DockerFact]
    public async Task ElRolDelTokenNoElevaElRolAsignadoEnLaPlataforma()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("hibrida_rol_local");
        await using var fabrica = FabricaApiPruebas.Crear(cadena, ConfigurarEntraSimulado, ConfiguracionEntra());

        var oid = Guid.NewGuid().ToString();
        await CrearCuentaVinculadaAsync(fabrica, oid, RolesAplicacion.Estudiante, "estudiante@uninpahu.edu.co");

        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            EmitirTokenEntra(oid, "Estudiante de Pruebas", "estudiante@uninpahu.edu.co", ["Administrativo"]));

        var respuesta = await cliente.GetAsync("/api/administracion/verificacion");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [DockerFact]
    public async Task ElTokenFederadoDeOtroInquilinoEsRechazado()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("hibrida_otro_tid");
        await using var fabrica = FabricaApiPruebas.Crear(cadena, ConfigurarEntraSimulado, ConfiguracionEntra());
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            EmitirTokenEntra(
                Guid.NewGuid().ToString(),
                "Docente Ajeno",
                "ajeno@uninpahu.edu.co",
                ["Docente"],
                tenantId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

        var respuesta = await cliente.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [DockerFact]
    public async Task ElLoginLocalSigueOperativoCuandoElProveedorFederadoNoResponde()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("hibrida_caida");
        await using var fabrica = FabricaApiPruebas.Crear(
            cadena,
            sobrescribir: new Dictionary<string, string?>
            {
                ["EntraId:TenantId"] = FabricaApiPruebas.TenantIdPrueba,
                ["EntraId:ClientId"] = FabricaApiPruebas.ClienteIdPrueba,
                ["EntraId:AuthorityOverride"] = "https://127.0.0.1:9/v2.0"
            });
        using var cliente = fabrica.CreateClient();

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            EmitirTokenEntra(Guid.NewGuid().ToString(), "Docente Caído", "caido@uninpahu.edu.co", ["Docente"]));

        var federada = await cliente.GetAsync("/api/auth/me");
        var cuerpo = await federada.Content.ReadAsStringAsync();
        Assert.True(federada.StatusCode == HttpStatusCode.ServiceUnavailable, $"DIAG2 {federada.StatusCode} {cuerpo}");

        var problema = await federada.Content.ReadFromJsonAsync<Problema>();
        Assert.NotNull(problema);
        Assert.EndsWith("/proveedor-identidad-no-disponible", problema.Type, StringComparison.Ordinal);

        // El login local no depende del proveedor federado.
        var login = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = FabricaApiPruebas.CorreoAdmin, contrasena = FabricaApiPruebas.ContrasenaAdmin });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static async Task CrearCuentaVinculadaAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> fabrica,
        string oid,
        string rol,
        string correo)
    {
        using var alcance = fabrica.Services.CreateScope();
        var usuarios = alcance.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Backend.Infrastructure.Persistence.Identity.Usuario>>();

        var tipoActor = string.Equals(rol, RolesAplicacion.Docente, StringComparison.Ordinal)
            ? Backend.Domain.Enums.TipoActor.Docente
            : Backend.Domain.Enums.TipoActor.Estudiante;

        var usuario = Backend.Infrastructure.Persistence.Identity.Usuario.Crear(
            "Cuenta Vinculada",
            correo,
            tipoActor,
            DateTimeOffset.UtcNow);

        usuario.VincularIdentidadFederada(oid);

        var creacion = await usuarios.CreateAsync(usuario, FabricaApiPruebas.ContrasenaAdmin);
        Assert.True(creacion.Succeeded, string.Join("; ", creacion.Errors.Select(error => error.Description)));

        var asignacion = await usuarios.AddToRoleAsync(usuario, rol);
        Assert.True(asignacion.Succeeded);
    }

    private static Dictionary<string, string?> ConfiguracionEntra() => new()
    {
        ["EntraId:TenantId"] = FabricaApiPruebas.TenantIdPrueba,
        ["EntraId:ClientId"] = FabricaApiPruebas.ClienteIdPrueba
    };

    private static void ConfigurarEntraSimulado(Microsoft.AspNetCore.Hosting.IWebHostBuilder constructor)
    {
        constructor.ConfigureTestServices(servicios =>
            servicios.PostConfigure<JwtBearerOptions>(AutenticacionHibridaExtensions.EsquemaEntraId, opciones =>
            {
                opciones.Authority = string.Empty;
                opciones.MetadataAddress = string.Empty;
                opciones.ConfigurationManager = null;
                opciones.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = FabricaApiPruebas.EmisorEntra,
                    ValidateAudience = true,
                    ValidAudience = FabricaApiPruebas.ClienteIdPrueba,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = ClaveEntra,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name",
                    RoleClaimType = "roles"
                };
            }));
    }

    private static string EmitirTokenEntra(
        string oid,
        string nombre,
        string correo,
        string[] roles,
        string? tenantId = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = FabricaApiPruebas.EmisorEntra,
            Audience = FabricaApiPruebas.ClienteIdPrueba,
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(20),
            SigningCredentials = new SigningCredentials(ClaveEntra, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["tid"] = tenantId ?? FabricaApiPruebas.TenantIdPrueba,
                ["oid"] = oid,
                ["name"] = nombre,
                ["email"] = correo,
                ["roles"] = roles
            }
        };

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    private sealed record PerfilRespuesta(Guid Id, string NombreCompleto, string CorreoInstitucional, int TipoActor, IReadOnlyCollection<string> Roles);

    private sealed record Problema(string Type, string Title, int Status, string? Detail);

    private sealed class CapturadorRegistros : ILoggerProvider
    {
        public ConcurrentQueue<string> Mensajes { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Registro(categoryName, Mensajes);

        public void Dispose()
        {
        }

        private sealed class Registro(string categoria, ConcurrentQueue<string> destino) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                destino.Enqueue($"{categoria}: {formatter(state, exception)}");
        }
    }
}
