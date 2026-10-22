using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Backend.Presentation.Api.Problemas;

/// <summary>
/// Traduce cualquier excepción no controlada a un problema RFC 7807 genérico,
/// sin exponer trazas ni detalles internos.
/// </summary>
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _servicio;
    private readonly ILogger<ProblemDetailsExceptionHandler> _registro;

    /// <summary>Inicializa el manejador con el servicio de problemas y el registro.</summary>
    public ProblemDetailsExceptionHandler(IProblemDetailsService servicio, ILogger<ProblemDetailsExceptionHandler> registro)
    {
        _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var correlacion = CatalogoProblemas.Correlacion(httpContext);

        if (EsFalloDeProveedorExterno(exception))
        {
            _registro.LogWarning(
                exception,
                "Proveedor externo no disponible en {Metodo} {Ruta}. traceId={TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                correlacion);

            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            var problemaProveedor = CatalogoProblemas.Construir(
                StatusCodes.Status503ServiceUnavailable,
                "proveedor-identidad-no-disponible",
                "Proveedor de identidad no disponible",
                "El proveedor de identidad no está disponible en este momento. Intente nuevamente más tarde.",
                httpContext);

            return await _servicio.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemaProveedor
            });
        }

        _registro.LogError(
            exception,
            "Excepción no controlada en {Metodo} {Ruta}. traceId={TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            correlacion);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problema = CatalogoProblemas.Construir(
            StatusCodes.Status500InternalServerError,
            "error-interno",
            "Error interno",
            "Ocurrió un error inesperado. Contacte al área de soporte si el problema persiste.",
            httpContext);

        return await _servicio.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema
        });
    }

    private static bool EsFalloDeProveedorExterno(Exception excepcion)
    {
        for (var actual = excepcion; actual is not null; actual = actual.InnerException)
        {
            if (actual is HttpRequestException or IOException or System.Net.Sockets.SocketException)
            {
                return true;
            }

            // Identificadores de IdentityModel para metadatos/JWKS inaccesibles.
            if (actual is InvalidOperationException
                && actual.Message.Contains("IDX208", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
