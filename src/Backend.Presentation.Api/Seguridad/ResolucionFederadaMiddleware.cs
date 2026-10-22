using System.Security.Claims;
using Backend.Application.Autenticacion;
using Backend.Application.Interfaces;
using Backend.Infrastructure.Identity;
using Backend.Presentation.Api.Problemas;
using Microsoft.Extensions.Logging;

namespace Backend.Presentation.Api.Seguridad;

/// <summary>
/// Resuelve el perfil institucional de las identidades federadas antes de la autorización:
/// reemplaza el principal por uno con el identificador interno y los roles efectivos.
/// </summary>
public sealed class ResolucionFederadaMiddleware
{
    private readonly RequestDelegate _siguiente;
    private readonly ILogger<ResolucionFederadaMiddleware> _registro;

    /// <summary>Inicializa el middleware con el siguiente delegado y el registro.</summary>
    public ResolucionFederadaMiddleware(RequestDelegate siguiente, ILogger<ResolucionFederadaMiddleware> registro)
    {
        _siguiente = siguiente ?? throw new ArgumentNullException(nameof(siguiente));
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
    }

    /// <summary>Ejecuta la resolución del perfil federado cuando corresponde.</summary>
    public async Task InvokeAsync(HttpContext contexto, IEntraIdProfileResolver resolutor)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(resolutor);

        if (!EsFederado(contexto.User))
        {
            await _siguiente(contexto);
            return;
        }

        var identidad = LeerIdentidad(contexto.User);

        if (identidad is null)
        {
            _registro.LogWarning("Acceso federado rechazado: token sin identificador institucional.");
            await EscribirProblemaAsync(contexto, 403, "permisos-insuficientes", "Permisos insuficientes");
            return;
        }

        var resultado = await resolutor.ResolverAsync(identidad, contexto.RequestAborted);

        if (resultado.Perfil is not { } perfil)
        {
            _registro.LogWarning(
                "Acceso federado rechazado: {Error} oid={Oid}",
                resultado.Error,
                identidad.Oid);

            await EscribirProblemaAsync(contexto, 403, "permisos-insuficientes", "Permisos insuficientes");
            return;
        }

        contexto.User = ConstruirPrincipal(perfil, AutenticacionHibridaExtensions.EsquemaEntraId);

        await _siguiente(contexto);
    }

    private static bool EsFederado(ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true
        && principal.HasClaim(ClaimTipos.Esquema, AutenticacionHibridaExtensions.EsquemaEntraId);

    private static IdentidadFederada? LeerIdentidad(ClaimsPrincipal principal)
    {
        var oid = principal.FindFirstValue(ClaimTipos.Oid);

        if (string.IsNullOrWhiteSpace(oid))
        {
            return null;
        }

        var nombre = principal.FindFirstValue(ClaimTipos.Nombre)
            ?? principal.FindFirstValue("preferred_username")
            ?? oid;

        var correo = principal.FindFirstValue(ClaimTipos.Email)
            ?? principal.FindFirstValue("preferred_username")
            ?? string.Empty;

        var roles = principal.FindAll(ClaimTipos.Roles).Select(claim => claim.Value).ToArray();

        return new IdentidadFederada(oid, nombre, correo, roles);
    }

    private static ClaimsPrincipal ConstruirPrincipal(PerfilUsuario perfil, string esquema)
    {
        var claims = new List<Claim>
        {
            new(ClaimTipos.Sub, perfil.Id.ToString()),
            new(ClaimTipos.Email, perfil.CorreoInstitucional),
            new(ClaimTipos.Nombre, perfil.NombreCompleto),
            new(ClaimTipos.Esquema, esquema)
        };

        claims.AddRange(perfil.Roles.Select(rol => new Claim(ClaimTipos.Roles, rol)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, esquema, ClaimTipos.Nombre, ClaimTipos.Roles));
    }

    private static async Task EscribirProblemaAsync(HttpContext contexto, int estado, string slug, string titulo)
    {
        if (contexto.Response.HasStarted)
        {
            return;
        }

        var problema = CatalogoProblemas.Construir(
            estado,
            slug,
            titulo,
            Backend.Domain.Exceptions.MensajesIdentidad.SinHabilitacionInstitucional,
            contexto);

        contexto.Response.StatusCode = estado;
        await contexto.Response.WriteAsJsonAsync(
            problema,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web),
            CatalogoProblemas.TipoContenido);
    }
}
