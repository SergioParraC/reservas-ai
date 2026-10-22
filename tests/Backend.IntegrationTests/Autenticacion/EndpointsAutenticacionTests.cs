using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Application.Common;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence.Identity;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.IntegrationTests.Autenticacion;

/// <summary>
/// Pruebas de los endpoints de autenticación local: emisión de token, contrato de error,
/// perfil del portador y autorización por rol administrativo.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class EndpointsAutenticacionTests
{
    private const string ClaveJwt = "clave-de-pruebas-suficientemente-larga-para-hmac-256";
    private const string Emisor = "https://api.reservas-espacios.edu";
    private const string Audiencia = "reservas-espacios-spa";
    private const string CorreoAdmin = "admin@universidad.edu";
    private const string Contrasena = "Clave.Segura.2026";

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public EndpointsAutenticacionTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElLoginEmiteTokenYDevuelveProblemaRfc7807AnteCredencialesInvalidas()
    {
        await using var fabrica = await CrearFabricaAsync();
        using var cliente = fabrica.CreateClient();

        var valido = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = CorreoAdmin, contrasena = Contrasena });

        Assert.Equal(HttpStatusCode.OK, valido.StatusCode);
        var token = await valido.Content.ReadFromJsonAsync<RespuestaToken>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        Assert.Equal("Bearer", token.TipoToken);
        Assert.Contains(RolesAplicacion.Administrativo, token.Roles);

        var incorrecta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = CorreoAdmin, contrasena = "Clave.Incorrecta.1" });

        Assert.Equal(HttpStatusCode.Unauthorized, incorrecta.StatusCode);
        Assert.Equal("application/problem+json", incorrecta.Content.Headers.ContentType?.MediaType);

        var inexistente = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = "nadie@universidad.edu", contrasena = "Clave.Incorrecta.1" });

        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);

        var problemaIncorrecta = await incorrecta.Content.ReadFromJsonAsync<Problema>();
        var problemaInexistente = await inexistente.Content.ReadFromJsonAsync<Problema>();

        Assert.NotNull(problemaIncorrecta);
        Assert.NotNull(problemaInexistente);
        Assert.Equal(problemaIncorrecta.Type, problemaInexistente.Type);
        Assert.Equal(problemaIncorrecta.Title, problemaInexistente.Title);
        Assert.Equal(401, problemaIncorrecta.Status);
        Assert.EndsWith("/credenciales-invalidas", problemaIncorrecta.Type, StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task ElPerfilSeResuelveConElTokenVigenteYSeRechazaSinToken()
    {
        await using var fabrica = await CrearFabricaAsync();
        using var cliente = fabrica.CreateClient();

        var sinToken = await cliente.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);

        var token = await IniciarSesionAsync(cliente, CorreoAdmin, Contrasena);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var respuesta = await cliente.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var perfil = await respuesta.Content.ReadFromJsonAsync<PerfilRespuesta>();
        Assert.NotNull(perfil);
        Assert.Equal(CorreoAdmin, perfil.CorreoInstitucional);
        Assert.Equal("Administradora de Pruebas", perfil.NombreCompleto);
        Assert.Equal(TipoActor.Administrativo, perfil.TipoActor);
        Assert.Contains(RolesAplicacion.Administrativo, perfil.Roles);
    }

    [DockerFact]
    public async Task ElEndpointAdministrativoExigeElRolAdministrativo()
    {
        await using var fabrica = await CrearFabricaAsync();
        using var cliente = fabrica.CreateClient();

        var sinToken = await cliente.GetAsync("/api/administracion/verificacion");
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);

        var tokenAdmin = await IniciarSesionAsync(cliente, CorreoAdmin, Contrasena);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAdmin.Token);

        var permitido = await cliente.GetAsync("/api/administracion/verificacion");
        Assert.Equal(HttpStatusCode.OK, permitido.StatusCode);

        var correoEstudiante = "estudiante@universidad.edu";
        await CrearUsuarioAsync(fabrica, correoEstudiante);
        var tokenEstudiante = await IniciarSesionAsync(cliente, correoEstudiante, Contrasena);

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenEstudiante.Token);
        var denegado = await cliente.GetAsync("/api/administracion/verificacion");

        Assert.Equal(HttpStatusCode.Forbidden, denegado.StatusCode);
    }

    [DockerFact]
    public async Task ElBloqueoTrasIntentosFallidosResponde423ConTipoCuentaBloqueada()
    {
        await using var fabrica = await CrearFabricaAsync(maxIntentosFallidos: 3);
        using var cliente = fabrica.CreateClient();

        for (var intento = 0; intento < 3; intento++)
        {
            var fallo = await cliente.PostAsJsonAsync(
                "/api/auth/login",
                new { correoInstitucional = CorreoAdmin, contrasena = "Clave.Incorrecta.1" });

            Assert.Equal(HttpStatusCode.Unauthorized, fallo.StatusCode);
        }

        var bloqueado = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = CorreoAdmin, contrasena = Contrasena });

        Assert.Equal(HttpStatusCode.Locked, bloqueado.StatusCode);

        var problema = await bloqueado.Content.ReadFromJsonAsync<Problema>();
        Assert.NotNull(problema);
        Assert.EndsWith("/cuenta-bloqueada", problema.Type, StringComparison.Ordinal);
    }

    private async Task<WebApplicationFactory<Program>> CrearFabricaAsync(int maxIntentosFallidos = 5)
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("api_auth");

        return FabricaApiPruebas.Crear(cadena, sobrescribir: new Dictionary<string, string?>
        {
            ["Identity:Lockout:MaxIntentosFallidos"] = maxIntentosFallidos.ToString(CultureInfo.InvariantCulture)
        });
    }

    private static async Task<RespuestaToken> IniciarSesionAsync(HttpClient cliente, string correo, string contrasena)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = correo, contrasena });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var token = await respuesta.Content.ReadFromJsonAsync<RespuestaToken>();
        Assert.NotNull(token);

        return token;
    }

    private static async Task CrearUsuarioAsync(WebApplicationFactory<Program> fabrica, string correo)
    {
        using var alcance = fabrica.Services.CreateScope();
        var usuarios = alcance.ServiceProvider.GetRequiredService<UserManager<Usuario>>();

        var usuario = Usuario.Crear("Estudiante de Pruebas", correo, TipoActor.Estudiante, DateTimeOffset.UtcNow);
        var creacion = await usuarios.CreateAsync(usuario, Contrasena);

        Assert.True(creacion.Succeeded, string.Join("; ", creacion.Errors.Select(error => error.Description)));

        var asignacion = await usuarios.AddToRoleAsync(usuario, RolesAplicacion.Estudiante);
        Assert.True(asignacion.Succeeded);
    }

    private sealed record RespuestaToken(string Token, string TipoToken, DateTimeOffset ExpiraEn, IReadOnlyCollection<string> Roles);

    private sealed record PerfilRespuesta(
        Guid Id,
        string NombreCompleto,
        string CorreoInstitucional,
        TipoActor TipoActor,
        IReadOnlyCollection<string> Roles);

    private sealed record Problema(string Type, string Title, int Status, string? Detail);
}
