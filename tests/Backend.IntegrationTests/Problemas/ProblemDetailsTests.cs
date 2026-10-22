using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.IntegrationTests.Infraestructura;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.IntegrationTests.Problemas;

/// <summary>
/// Verifica el contrato RFC 7807: miembros y `type` estables, validación por campo,
/// rechazos de autenticación con `WWW-Authenticate` y ausencia de enumeración de usuarios.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class ProblemDetailsTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public ProblemDetailsTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElLoginInvalidoDevuelveElContratoCompletoConCorrelacion()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("problemas_login");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = FabricaApiPruebas.CorreoAdmin, contrasena = "Clave.Incorrecta.1" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var raiz = problema.RootElement;

        Assert.Equal(401, raiz.GetProperty("status").GetInt32());
        Assert.StartsWith(CatalogoProblemas.BaseTipos, raiz.GetProperty("type").GetString());
        Assert.EndsWith("/credenciales-invalidas", raiz.GetProperty("type").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(raiz.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(raiz.GetProperty("detail").GetString()));
        Assert.Equal("/api/auth/login", raiz.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(raiz.GetProperty("traceId").GetString()));
    }

    [DockerFact]
    public async Task ElCuerpoIncompletoDevuelve400ConErroresPorCampo()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("problemas_validacion");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = FabricaApiPruebas.CorreoAdmin });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var raiz = problema.RootElement;

        Assert.EndsWith("/solicitud-invalida", raiz.GetProperty("type").GetString(), StringComparison.Ordinal);
        Assert.True(raiz.TryGetProperty("errors", out var errores));
        Assert.True(errores.TryGetProperty("Contrasena", out _) || errores.TryGetProperty("contrasena", out _));

        var correoInvalido = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = "no-es-un-correo", contrasena = "Clave.Segura.2026" });

        Assert.Equal(HttpStatusCode.BadRequest, correoInvalido.StatusCode);
    }

    [DockerFact]
    public async Task LaAusenciaDeTokenDevuelveTokenAusenteConWwwAuthenticate()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("problemas_sin_token");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.True(respuesta.Headers.WwwAuthenticate.Count > 0);

        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.EndsWith("/token-ausente", problema.RootElement.GetProperty("type").GetString(), StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task LasCredencialesInvalidasNoRevelanSiElCorreoExiste()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("problemas_enumeracion");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var existente = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = FabricaApiPruebas.CorreoAdmin, contrasena = "Clave.Incorrecta.1" });

        var inexistente = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { correoInstitucional = "nadie@universidad.edu", contrasena = "Clave.Incorrecta.1" });

        Assert.Equal(HttpStatusCode.Unauthorized, existente.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);

        // El identificador de correlación es único por solicitud; el resto del cuerpo debe coincidir.
        var cuerpoExistente = SinCorrelacion(await existente.Content.ReadAsStringAsync());
        var cuerpoInexistente = SinCorrelacion(await inexistente.Content.ReadAsStringAsync());

        Assert.Equal(cuerpoExistente, cuerpoInexistente);
    }

    [Fact]
    public async Task ElManejadorDeExcepcionesDevuelve500GenericoSinTrazas()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddProblemasRfc7807();

        await using var proveedor = servicios.BuildServiceProvider();
        var manejador = proveedor
            .GetServices<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>()
            .OfType<ProblemDetailsExceptionHandler>()
            .Single();

        var contexto = new DefaultHttpContext();
        contexto.Request.Path = "/api/prueba";
        contexto.Response.Body = new MemoryStream();

        var controlado = await manejador.TryHandleAsync(
            contexto,
            new InvalidOperationException("Error interno de prueba"),
            CancellationToken.None);

        Assert.True(controlado);
        Assert.Equal(StatusCodes.Status500InternalServerError, contexto.Response.StatusCode);

        contexto.Response.Body.Position = 0;
        var cuerpo = await new StreamReader(contexto.Response.Body).ReadToEndAsync();

        Assert.Contains("/error-interno", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", cuerpo, StringComparison.Ordinal);
    }

    private static string SinCorrelacion(string cuerpo)
    {
        using var documento = JsonDocument.Parse(cuerpo);
        var propiedades = documento.RootElement
            .EnumerateObject()
            .Where(propiedad => !string.Equals(propiedad.Name, "traceId", StringComparison.OrdinalIgnoreCase))
            .OrderBy(propiedad => propiedad.Name, StringComparer.Ordinal)
            .Select(propiedad => $"{propiedad.Name}={propiedad.Value}");

        return string.Join('|', propiedades);
    }
}
