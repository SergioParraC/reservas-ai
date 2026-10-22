using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Presentation.Api.Seguridad;

/// <summary>
/// Política CORS para la SPA: orígenes explícitos configurados por entorno,
/// sin credenciales (la autenticación viaja en el encabezado <c>Authorization</c>).
/// </summary>
public static class CorsExtensions
{
    /// <summary>Nombre de la política CORS de la SPA.</summary>
    public const string PoliticaSpa = "Spa";

    /// <summary>Nombre de la sección de configuración con los orígenes permitidos.</summary>
    public const string Seccion = "Cors";

    /// <summary>Registra la política CORS con los orígenes declarados en <c>Cors:OrigenesPermitidos</c>.</summary>
    public static IServiceCollection AddCorsSpa(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var origenes = configuration.GetSection($"{Seccion}:OrigenesPermitidos").Get<string[]>() ?? [];

        services.AddCors(opciones => opciones.AddPolicy(PoliticaSpa, politica =>
        {
            if (origenes.Length == 0)
            {
                politica.SetIsOriginAllowed(_ => false);
                return;
            }

            politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod();
        }));

        return services;
    }
}
