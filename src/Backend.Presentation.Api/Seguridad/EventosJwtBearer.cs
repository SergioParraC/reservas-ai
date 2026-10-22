using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Backend.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Presentation.Api.Seguridad;

/// <summary>
/// Eventos de los esquemas JWT: materializan los rechazos de autenticación y autorización
/// como problemas RFC 7807 y registran los intentos federados para auditoría.
/// </summary>
public static class EventosJwtBearer
{
    /// <summary>Nombre del esquema federado de Entra ID.</summary>
    public const string EsquemaEntraId = "EntraId";

    /// <summary>Crea los eventos del esquema indicado.</summary>
    public static JwtBearerEvents Crear(string esquema) => new()
    {
        OnChallenge = contexto => EscribirChallengeAsync(contexto, esquema),
        OnForbidden = contexto => EscribirProblemaAsync(
            contexto.HttpContext,
            StatusCodes.Status403Forbidden,
            "permisos-insuficientes",
            "Permisos insuficientes",
            "No tiene permisos para realizar esta operación."),
        OnAuthenticationFailed = contexto =>
        {
            contexto.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Backend.Presentation.Api.Seguridad.JwtBearer")
                .LogWarning(
                    "Intento de autenticación rechazado en el esquema {Esquema}: {Motivo}",
                    esquema,
                    contexto.Exception.GetType().Name);

            return Task.CompletedTask;
        }
    };

    private static async Task EscribirChallengeAsync(JwtBearerChallengeContext contexto, string esquema)
    {
        contexto.HandleResponse();

        var http = contexto.HttpContext;

        if (http.Response.HasStarted)
        {
            return;
        }

        var sinCredenciales = string.IsNullOrWhiteSpace(http.Request.Headers.Authorization);
        var fallo = contexto.AuthenticateFailure;

        if (sinCredenciales || fallo is null)
        {
            http.Response.Headers.WWWAuthenticate = "Bearer";
            await EscribirProblemaAsync(
                http,
                StatusCodes.Status401Unauthorized,
                "token-ausente",
                "Token ausente",
                "La solicitud requiere autenticación.").ConfigureAwait(false);

            return;
        }

        // Metadatos/JWKS inaccesibles: no es una credencial inválida del usuario.
        if (string.Equals(esquema, EsquemaEntraId, StringComparison.Ordinal) && EsFalloDeProveedor(fallo))
        {
            await EscribirProblemaAsync(
                http,
                StatusCodes.Status503ServiceUnavailable,
                "proveedor-identidad-no-disponible",
                "Proveedor de identidad no disponible",
                "El proveedor de identidad no está disponible en este momento. Intente nuevamente más tarde.").ConfigureAwait(false);

            return;
        }

        if (fallo is SecurityTokenExpiredException)
        {
            http.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
            await EscribirProblemaAsync(
                http,
                StatusCodes.Status401Unauthorized,
                "sesion-expirada",
                "Sesión expirada",
                "La sesión expiró. Vuelva a iniciar sesión.").ConfigureAwait(false);

            return;
        }

        if (fallo is SecurityTokenException)
        {
            http.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
            await EscribirProblemaAsync(
                http,
                StatusCodes.Status401Unauthorized,
                "token-invalido",
                "Token inválido",
                "El token presentado no es válido.").ConfigureAwait(false);

            return;
        }

        if (string.Equals(esquema, EsquemaEntraId, StringComparison.Ordinal))
        {
            // Fallo de metadatos/JWKS: no es una credencial inválida del usuario.
            await EscribirProblemaAsync(
                http,
                StatusCodes.Status503ServiceUnavailable,
                "proveedor-identidad-no-disponible",
                "Proveedor de identidad no disponible",
                "El proveedor de identidad no está disponible en este momento. Intente nuevamente más tarde.").ConfigureAwait(false);

            return;
        }

        await EscribirProblemaAsync(
            http,
            StatusCodes.Status401Unauthorized,
            "token-invalido",
            "Token inválido",
            "El token presentado no es válido.").ConfigureAwait(false);
    }

    private static bool EsFalloDeProveedor(Exception? fallo)
    {
        for (var actual = fallo; actual is not null; actual = actual.InnerException)
        {
            if (actual is HttpRequestException or IOException or System.Net.Sockets.SocketException)
            {
                return true;
            }

            if (actual is SecurityTokenSignatureKeyNotFoundException)
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

    private static async Task EscribirProblemaAsync(HttpContext http, int estado, string slug, string titulo, string detalle)
    {
        if (http.Response.HasStarted)
        {
            return;
        }

        var problema = Problemas.CatalogoProblemas.Construir(estado, slug, titulo, detalle, http);

        http.Response.StatusCode = estado;
        await http.Response.WriteAsJsonAsync(
            problema,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            Problemas.CatalogoProblemas.TipoContenido).ConfigureAwait(false);
    }

    /// <summary>Lee el emisor de un JWT sin validar la firma; solo se usa para enrutar el esquema.</summary>
    public static string? LeerEmisor(string token)
    {
        try
        {
            var segmentos = token.Split('.');

            if (segmentos.Length < 2)
            {
                return null;
            }

            var payload = Base64UrlEncoder.Decode(segmentos[1]);

            using var documento = JsonDocument.Parse(payload);

            return documento.RootElement.TryGetProperty(JwtRegisteredClaimNames.Iss, out var emisor)
                ? emisor.GetString()
                : null;
        }
        catch (Exception excepcion) when (excepcion is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
