using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Backend.IntegrationTests.Persistencia;

/// <summary>
/// Verifica que la migración inicial crea el esquema de identidad completo
/// (tablas, índice único filtrado y columnas de auditoría) sobre SQL Server 2022.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class EsquemaIdentityTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public EsquemaIdentityTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task LaMigracionInicialCreaTablasIndiceFiltradoYColumnasDeAuditoria()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("identidad");
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(cadena).Options;

        await using (var contexto = new ApplicationDbContext(opciones))
        {
            await contexto.Database.MigrateAsync();

            var aplicadas = await contexto.Database.GetAppliedMigrationsAsync();
            Assert.Contains(aplicadas, migracion => migracion.EndsWith("InitialCreate", StringComparison.Ordinal));
        }

        var tablas = await LeerAsync(cadena, "SELECT name FROM sys.tables;");
        Assert.Contains("AspNetUsers", tablas);
        Assert.Contains("AspNetRoles", tablas);
        Assert.Contains("AspNetUserRoles", tablas);
        Assert.Contains("__EFMigrationsHistory", tablas);

        var columnas = await LeerAsync(
            cadena,
            "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers');");

        Assert.Contains("NombreCompleto", columnas);
        Assert.Contains("TipoActor", columnas);
        Assert.Contains("IdentificadorFederado", columnas);
        Assert.Contains("Activo", columnas);
        Assert.Contains("CreadoEn", columnas);
        Assert.Contains("UltimoAccesoEn", columnas);

        var tipoDeTipoActor = await LeerAsync(
            cadena,
            "SELECT TYPE_NAME(user_type_id) FROM sys.columns WHERE object_id = OBJECT_ID('AspNetUsers') AND name = 'TipoActor';");
        Assert.Equal("int", Assert.Single(tipoDeTipoActor));

        var indices = await LeerAsync(
            cadena,
            $"SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('AspNetUsers') AND name = '{UsuarioConfiguration.NombreIndiceIdentificadorFederado}' AND has_filter = 1 AND is_unique = 1;");
        Assert.Single(indices);
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
