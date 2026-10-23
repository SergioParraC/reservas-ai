using Backend.Infrastructure.Persistence;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Backend.IntegrationTests.Persistencia;

/// <summary>
/// Verifica que la migración <c>AreasYPuestos</c> crea las tablas del catálogo, el token
/// de concurrencia y los cuatro índices únicos exigidos por el diseño.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class EsquemaEspaciosTests
{
    private static readonly string[] IndicesUnicos =
    [
        "IX_Espacios_CodigoQR",
        "IX_Puestos_CodigoQR",
        "IX_Espacios_Nombre_Ubicacion",
        "IX_Puestos_EspacioId_Identificador"
    ];

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public EsquemaEspaciosTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task LaMigracionCreaIndicesUnicosYRowVersion()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("esquema_espacios");
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(cadena).Options;

        await using (var contexto = new ApplicationDbContext(opciones))
        {
            await contexto.Database.MigrateAsync();

            var aplicadas = await contexto.Database.GetAppliedMigrationsAsync();
            Assert.Contains(aplicadas, migracion => migracion.EndsWith("AreasYPuestos", StringComparison.Ordinal));
        }

        var tablas = await LeerAsync(cadena, "SELECT name FROM sys.tables;");
        Assert.Contains("Espacios", tablas);
        Assert.Contains("Puestos", tablas);

        var indices = await LeerAsync(
            cadena,
            @"SELECT i.name
              FROM sys.indexes i
              WHERE i.object_id IN (OBJECT_ID('Espacios'), OBJECT_ID('Puestos'))
                AND i.is_unique = 1
                AND i.name IS NOT NULL;");

        foreach (var indice in IndicesUnicos)
        {
            Assert.Contains(indice, indices);
        }

        var rowVersion = await LeerAsync(
            cadena,
            @"SELECT TYPE_NAME(c.user_type_id)
              FROM sys.columns c
              WHERE c.object_id = OBJECT_ID('Espacios') AND c.name = 'RowVersion';");

        Assert.Contains(Assert.Single(rowVersion), new[] { "rowversion", "timestamp" });

        var columnasPuesto = await LeerAsync(
            cadena,
            @"SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('Puestos');");

        Assert.Contains("Identificador", columnasPuesto);
        Assert.Contains("CodigoQR", columnasPuesto);
        Assert.Contains("Estado", columnasPuesto);
    }

    private static async Task<List<string>> LeerAsync(string cadena, string consulta)
    {
        var valores = new List<string>();

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = consulta;
        await using var lector = await comando.ExecuteReaderAsync();

        while (await lector.ReadAsync())
        {
            valores.Add(lector.GetString(0));
        }

        return valores;
    }
}
