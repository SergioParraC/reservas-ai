using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;

namespace Backend.IntegrationTests.Salud;

/// <summary>
/// Verifica el endpoint <c>/health</c>: 200 con la base disponible y 503 cuando no lo está.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class SaludTests
{
    private const string CadenaInaccesible =
        "Server=127.0.0.1,1;Database=salud;User Id=sa;Password=Clave.Invalida.1;Connect Timeout=2;TrustServerCertificate=True";

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public SaludTests(SqlServerContainerFixture sql) => _sql = sql;

    [Fact]
    public async Task ElEndpointSaludDevuelve503SinBaseDisponible()
    {
        await using var fabrica = FabricaApiPruebas.Crear(CadenaInaccesible, entorno: "Production");
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
    }

    [DockerFact]
    public async Task ElEndpointSaludDevuelve200ConBaseDisponible()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("salud");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Healthy", await respuesta.Content.ReadAsStringAsync());
    }
}
