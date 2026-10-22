using Backend.IntegrationTests.Infraestructura;

namespace Backend.IntegrationTests.Seguridad;

/// <summary>
/// Verifica la política CORS de la SPA: el origen configurado recibe los encabezados
/// y cualquier otro origen es rechazado.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class CorsTests
{
    private const string OrigenPermitido = "http://localhost:4200";
    private const string OrigenNoPermitido = "https://malicioso.example";

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public CorsTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElOrigenPermitidoRecibeLosEncabezadosCors()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("cors_permitido");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.SendAsync(CrearPreflight(OrigenPermitido));

        Assert.True(respuesta.Headers.TryGetValues("Access-Control-Allow-Origin", out var origenes));
        Assert.Contains(OrigenPermitido, origenes);
        Assert.True(respuesta.Headers.TryGetValues("Access-Control-Allow-Methods", out _));
    }

    [DockerFact]
    public async Task ElOrigenNoPermitidoEsRechazado()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("cors_denegado");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.SendAsync(CrearPreflight(OrigenNoPermitido));

        Assert.False(respuesta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage CrearPreflight(string origen)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        solicitud.Headers.Add("Origin", origen);
        solicitud.Headers.Add("Access-Control-Request-Method", "POST");
        solicitud.Headers.Add("Access-Control-Request-Headers", "content-type");

        return solicitud;
    }
}
