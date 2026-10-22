using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Presentation.Api.Problemas;

/// <summary>
/// Registro del contrato RFC 7807: personalización de todos los problemas,
/// catálogo de <c>type</c> para validaciones y manejador de excepciones no controladas.
/// </summary>
public static class ProblemasExtensions
{
    /// <summary>Registra <c>AddProblemDetails</c> con correlación y el manejador global de excepciones.</summary>
    public static IServiceCollection AddProblemasRfc7807(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(opciones =>
        {
            opciones.CustomizeProblemDetails = contexto =>
            {
                var problema = contexto.ProblemDetails;

                problema.Instance ??= contexto.HttpContext.Request.Path;
                problema.Extensions["traceId"] ??= CatalogoProblemas.Correlacion(contexto.HttpContext);

                if (problema.Status == StatusCodes.Status400BadRequest
                    && (problema is ValidationProblemDetails || string.IsNullOrWhiteSpace(problema.Type)))
                {
                    problema.Type = CatalogoProblemas.BaseTipos + "solicitud-invalida";
                    problema.Title = "Solicitud inválida";
                    problema.Detail ??= "La solicitud contiene campos inválidos.";
                }
            };
        });

        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        return services;
    }
}
