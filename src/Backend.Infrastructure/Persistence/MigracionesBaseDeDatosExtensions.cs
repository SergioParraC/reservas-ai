using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Persistence;

/// <summary>
/// Aplicación del esquema al arranque. Se invoca únicamente en <c>Development</c>;
/// en otros entornos las migraciones son un paso explícito de despliegue.
/// </summary>
public static class MigracionesBaseDeDatosExtensions
{
    /// <summary>Aplica las migraciones pendientes y siembra los datos base de identidad.</summary>
    public static async Task AplicarMigracionesAsync(this IServiceProvider proveedor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        using var alcance = proveedor.CreateScope();
        var registro = alcance.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Backend.Infrastructure.Persistence.Migraciones");

        try
        {
            var contexto = alcance.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await contexto.Database.MigrateAsync(cancellationToken);

            var inicializador = alcance.ServiceProvider.GetRequiredService<IInicializadorIdentity>();
            await inicializador.InicializarAsync(cancellationToken);

            registro.LogInformation("Esquema de identidad actualizado y datos base sembrados.");
        }
        catch (Exception excepcion)
        {
            registro.LogCritical(excepcion, "No se pudo aplicar el esquema de identidad.");
            throw;
        }
    }
}
