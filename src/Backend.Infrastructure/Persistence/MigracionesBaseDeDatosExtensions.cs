using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Persistence;

/// <summary>
/// Aplicación del esquema y siembra de identidad. Las migraciones se invocan
/// únicamente en <c>Development</c>; en otros entornos son un paso explícito de
/// despliegue. La siembra del administrador es idempotente y corre en cualquier entorno.
/// </summary>
public static class MigracionesBaseDeDatosExtensions
{
    /// <summary>Aplica las migraciones pendientes y siembra los datos base de identidad.</summary>
    public static async Task AplicarMigracionesAsync(this IServiceProvider proveedor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        using (var alcance = proveedor.CreateScope())
        {
            var registro = alcance.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Backend.Infrastructure.Persistence.Migraciones");

            try
            {
                var contexto = alcance.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await contexto.Database.MigrateAsync(cancellationToken);
            }
            catch (Exception excepcion)
            {
                registro.LogCritical(excepcion, "No se pudo aplicar el esquema de identidad.");
                throw;
            }
        }

        await proveedor.SembrarIdentidadAsync(cancellationToken);
    }

    /// <summary>
    /// Siembra idempotente de roles y administrador inicial. Se invoca al arrancar en
    /// cualquier entorno: si el usuario ya existe no hace nada.
    /// </summary>
    public static async Task SembrarIdentidadAsync(this IServiceProvider proveedor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        using var alcance = proveedor.CreateScope();
        var registro = alcance.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Backend.Infrastructure.Persistence.Semilla");

        try
        {
            var inicializador = alcance.ServiceProvider.GetRequiredService<IInicializadorIdentity>();
            await inicializador.InicializarAsync(cancellationToken);

            registro.LogInformation("Datos base de identidad verificados.");
        }
        catch (Exception excepcion)
        {
            registro.LogCritical(excepcion, "No se pudieron sembrar los datos base de identidad.");
            throw;
        }
    }
}

