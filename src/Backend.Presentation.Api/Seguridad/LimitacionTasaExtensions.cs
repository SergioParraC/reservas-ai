using System.Text.Json;
using System.Threading.RateLimiting;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Presentation.Api.Seguridad;

/// <summary>
/// Limitación de tasa del autorregistro público con ventana deslizante por dirección remota.
/// El rechazo conserva el contrato RFC 7807 con <c>429</c>.
/// </summary>
public static class LimitacionTasaExtensions
{
    /// <summary>Nombre de la política del formulario de registro.</summary>
    public const string PoliticaRegistro = "registro";

    /// <summary>Registra la política de ventana deslizante del registro.</summary>
    public static IServiceCollection AddLimitacionTasa(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(opciones =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            opciones.OnRejected = async (contexto, cancellationToken) =>
            {
                var http = contexto.HttpContext;

                if (http.Response.HasStarted)
                {
                    return;
                }

                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var problema = CatalogoProblemas.Construir(
                    StatusCodes.Status429TooManyRequests,
                    "demasiadas-solicitudes",
                    "Demasiadas solicitudes",
                    "Se superó el límite de registros. Intente nuevamente en un minuto.",
                    http);

                await http.Response.WriteAsJsonAsync(
                    problema,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web),
                    CatalogoProblemas.TipoContenido,
                    cancellationToken);
            };

            opciones.AddPolicy(PoliticaRegistro, http =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 2,
                        QueueLimit = 0
                    }));
        });

        return services;
    }
}
