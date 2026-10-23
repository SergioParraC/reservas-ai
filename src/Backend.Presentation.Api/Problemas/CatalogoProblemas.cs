using System.Diagnostics;
using Backend.Application.Common;
using Backend.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Problemas;

/// <summary>
/// Catálogo de problemas RFC 7807 de la API: <c>type</c> estables, título y estado
/// por condición, más el identificador de correlación de la solicitud.
/// </summary>
public static class CatalogoProblemas
{
    /// <summary>Base de las URIs de tipo de problema.</summary>
    public const string BaseTipos = "https://api.reservas-espacios.edu/problems/";

    /// <summary>Tipo de contenido del contrato.</summary>
    public const string TipoContenido = "application/problem+json";

    /// <summary>Traduce una clase de error de autenticación al problema HTTP correspondiente.</summary>
    public static ObjectResult DesdeError(ErrorAutenticacion error, HttpContext contexto) => error switch
    {
        ErrorAutenticacion.CredencialesInvalidas or ErrorAutenticacion.CuentaInactiva => Crear(
            StatusCodes.Status401Unauthorized, "credenciales-invalidas", "Credenciales inválidas",
            MensajesIdentidad.CredencialesInvalidas, contexto),
        ErrorAutenticacion.CuentaBloqueada => Crear(
            StatusCodes.Status423Locked, "cuenta-bloqueada", "Cuenta bloqueada",
            MensajesIdentidad.CuentaBloqueada, contexto),
        ErrorAutenticacion.SesionExpirada => Crear(
            StatusCodes.Status401Unauthorized, "sesion-expirada", "Sesión expirada",
            "La sesión expiró. Vuelva a iniciar sesión.", contexto),
        ErrorAutenticacion.SesionRevocada => Crear(
            StatusCodes.Status401Unauthorized, "sesion-revocada", "Sesión revocada",
            "La sesión fue revocada. Vuelva a iniciar sesión.", contexto),
        ErrorAutenticacion.TokenInvalido => Crear(
            StatusCodes.Status401Unauthorized, "token-invalido", "Token inválido",
            "El token presentado no es válido.", contexto),
        ErrorAutenticacion.SinVinculacion => Crear(
            StatusCodes.Status403Forbidden, "vinculacion-requerida", "Vinculación requerida",
            MensajesIdentidad.SinVinculacion, contexto),
        ErrorAutenticacion.PerfilInexistente or ErrorAutenticacion.SinRolHabilitado => Crear(
            StatusCodes.Status403Forbidden, "permisos-insuficientes", "Permisos insuficientes",
            MensajesIdentidad.SinHabilitacionInstitucional, contexto),
        ErrorAutenticacion.ProveedorIdentidadNoDisponible => Crear(
            StatusCodes.Status503ServiceUnavailable, "proveedor-identidad-no-disponible", "Proveedor de identidad no disponible",
            "El proveedor de identidad no está disponible en este momento. Intente nuevamente más tarde.", contexto),
        _ => Crear(
            StatusCodes.Status500InternalServerError, "error-interno", "Error interno",
            "Ocurrió un error inesperado.", contexto)
    };

    /// <summary>Traduce el fallo de un caso de uso al código HTTP del contrato.</summary>
    public static ObjectResult DesdeResultado(Resultado resultado, HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(contexto);

        return CrearDesdeFallo(resultado.TipoFallo, resultado.Mensaje, resultado.Errores, contexto);
    }

    /// <summary>Traduce el fallo de un caso de uso con valor al código HTTP del contrato.</summary>
    public static ObjectResult DesdeResultado<T>(Resultado<T> resultado, HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(contexto);

        return CrearDesdeFallo(resultado.TipoFallo, resultado.Mensaje, resultado.Errores, contexto);
    }

    private static ObjectResult CrearDesdeFallo(
        TipoFallo tipoFallo,
        string? mensaje,
        IReadOnlyDictionary<string, string[]> errores,
        HttpContext contexto)
    {
        var (estado, slug, titulo) = tipoFallo switch
        {
            TipoFallo.Validacion => (StatusCodes.Status400BadRequest, "solicitud-invalida", "Solicitud inválida"),
            TipoFallo.Conflicto => (StatusCodes.Status409Conflict, "conflicto", "Conflicto con el estado actual"),
            TipoFallo.NoEncontrado => (StatusCodes.Status404NotFound, "recurso-no-encontrado", "Recurso no encontrado"),
            TipoFallo.NoPermitido => (StatusCodes.Status403Forbidden, "operacion-no-permitida", "Operación no permitida"),
            TipoFallo.NoAutorizado => (StatusCodes.Status401Unauthorized, "token-invalido", "Token inválido"),
            _ => (StatusCodes.Status500InternalServerError, "error-interno", "Error interno")
        };

        var detalle = string.IsNullOrWhiteSpace(mensaje) ? titulo : mensaje;
        var diccionario = errores.Count == 0
            ? null
            : errores.ToDictionary(par => par.Key, par => par.Value, StringComparer.Ordinal);

        return Crear(estado, slug, titulo, detalle, contexto, diccionario);
    }

    /// <summary>Construye el cuerpo del problema con correlación y errores por campo opcionales.</summary>
    public static ProblemDetails Construir(
        int estado,
        string slug,
        string titulo,
        string detalle,
        HttpContext contexto,
        IDictionary<string, string[]>? errores = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var problema = new ProblemDetails
        {
            Type = BaseTipos + slug,
            Title = titulo,
            Status = estado,
            Detail = detalle,
            Instance = contexto.Request.Path
        };

        problema.Extensions["traceId"] = Correlacion(contexto);

        if (errores is not null)
        {
            problema.Extensions["errors"] = errores;
        }

        return problema;
    }

    /// <summary>Construye la respuesta de problema con el tipo de contenido del contrato.</summary>
    public static ObjectResult Crear(
        int estado,
        string slug,
        string titulo,
        string detalle,
        HttpContext contexto,
        IDictionary<string, string[]>? errores = null)
    {
        var respuesta = new ObjectResult(Construir(estado, slug, titulo, detalle, contexto, errores))
        {
            StatusCode = estado
        };

        respuesta.ContentTypes.Add(TipoContenido);

        return respuesta;
    }

    /// <summary>Identificador de correlación del evento (traza distribuida o identificador de la solicitud).</summary>
    public static string Correlacion(HttpContext contexto) =>
        Activity.Current?.TraceId.ToString() ?? contexto.TraceIdentifier;
}
