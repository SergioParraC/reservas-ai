using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Backend.IntegrationTests.Infraestructura;

/// <summary>Colección que comparte un único contenedor de SQL Server 2022.</summary>
[CollectionDefinition(nameof(SqlServerCollection))]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>
{
}

/// <summary>
/// Contenedor compartido de SQL Server 2022. El arranque es diferido: solo se inicia
/// cuando una prueba realmente lo usa, por lo que omitir las pruebas no requiere Docker.
/// Cada prueba recibe una base de datos vacía, de forma que el esquema se crea
/// aplicando la migración real (nunca con <c>EnsureCreated</c>).
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private const string ImagenSqlServer = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly SemaphoreSlim _candado = new(1, 1);
    private MsSqlContainer? _contenedor;

    /// <inheritdoc />
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_contenedor is not null)
        {
            await _contenedor.DisposeAsync();
        }

        _candado.Dispose();
    }

    /// <summary>Crea una base de datos vacía en el contenedor y devuelve su cadena de conexión.</summary>
    public async Task<string> CrearBaseDeDatosAsync(string prefijo, CancellationToken cancellationToken = default)
    {
        var contenedor = await AsegurarContenedorAsync();
        var nombre = $"{prefijo}_{Guid.NewGuid():N}";

        var cadenaMaestra = new SqlConnectionStringBuilder(contenedor.GetConnectionString())
        {
            InitialCatalog = "master"
        }.ConnectionString;

        await using (var conexion = new SqlConnection(cadenaMaestra))
        {
            await conexion.OpenAsync(cancellationToken);
            await using var comando = conexion.CreateCommand();
            comando.CommandText = $"CREATE DATABASE [{nombre}];";
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        return new SqlConnectionStringBuilder(contenedor.GetConnectionString())
        {
            InitialCatalog = nombre
        }.ConnectionString;
    }

    private async Task<MsSqlContainer> AsegurarContenedorAsync()
    {
        if (_contenedor is not null)
        {
            return _contenedor;
        }

        await _candado.WaitAsync();
        try
        {
            if (_contenedor is not null)
            {
                return _contenedor;
            }

            var contenedor = new MsSqlBuilder(ImagenSqlServer).Build();
            await contenedor.StartAsync();
            _contenedor = contenedor;

            return contenedor;
        }
        finally
        {
            _candado.Release();
        }
    }
}
