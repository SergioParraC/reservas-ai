using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace Backend.IntegrationTests.Persistencia;

/// <summary>
/// Verifica la política de migraciones al arranque: solo <c>Development</c> aplica
/// el esquema; cualquier otro entorno exige un paso explícito de despliegue.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class MigracionesAlArranqueTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public MigracionesAlArranqueTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task DevelopmentAplicaElEsquemaAlArrancar()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("arranque_dev");

        await using var fabrica = CrearFabrica(cadena, "Development");
        using var cliente = fabrica.CreateClient();

        Assert.True(await ExisteTablaAsync(cadena, "__EFMigrationsHistory"));
        Assert.True(await ExisteTablaAsync(cadena, "AspNetUsers"));
        Assert.NotNull(cliente);
    }

    [DockerFact]
    public async Task ProduccionNoAplicaMigracionesAutomaticamente()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("arranque_prod");

        await using var fabrica = CrearFabrica(cadena, "Production");
        using var cliente = fabrica.CreateClient();

        Assert.False(await ExisteTablaAsync(cadena, "__EFMigrationsHistory"));
        Assert.False(await ExisteTablaAsync(cadena, "AspNetUsers"));
        Assert.NotNull(cliente);
    }

    private static WebApplicationFactory<Program> CrearFabrica(string cadena, string entorno) =>
        FabricaApiPruebas.Crear(cadena, entorno: entorno);

    private static async Task<bool> ExisteTablaAsync(string cadena, string tabla)
    {
        try
        {
            await using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = @tabla;";
            comando.Parameters.AddWithValue("@tabla", tabla);

            var total = (int)(await comando.ExecuteScalarAsync() ?? 0);
            return total > 0;
        }
        catch (SqlException)
        {
            // La base no existe todavía: el esquema no se aplicó.
            return false;
        }
    }
}
