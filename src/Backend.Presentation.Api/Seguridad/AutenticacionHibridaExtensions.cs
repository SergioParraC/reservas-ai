using System.Security.Claims;
using System.Text;
using Backend.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Presentation.Api.Seguridad;

/// <summary>
/// Esquema de autenticación híbrido: un <c>PolicyScheme</c> enruta cada solicitud al
/// esquema local (JWT propio) o al federado (Entra ID) según el emisor del token.
/// </summary>
public static class AutenticacionHibridaExtensions
{
    /// <summary>Nombre del esquema de autenticación local.</summary>
    public const string EsquemaLocal = "LocalJwt";

    /// <summary>Nombre del esquema de autenticación federado.</summary>
    public const string EsquemaEntraId = EventosJwtBearer.EsquemaEntraId;

    /// <summary>Nombre del esquema de resolución híbrida.</summary>
    public const string EsquemaHibrido = "Hibrida";

    /// <summary>
    /// Registra los esquemas local y federado y la política que los selecciona.
    /// La firma se valida siempre con el handler elegido.
    /// </summary>
    public static IServiceCollection AddAutenticacionHibrida(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var jwt = configuration.GetSection(JwtOptions.Seccion).Get<JwtOptions>() ?? new JwtOptions();
        var entra = configuration.GetSection(EntraIdOptions.Seccion).Get<EntraIdOptions>() ?? new EntraIdOptions();

        jwt.Validar();
        entra.Validar();

        services.Configure<EntraIdOptions>(configuration.GetSection(EntraIdOptions.Seccion));

        services.AddAuthentication(opciones =>
            {
                opciones.DefaultScheme = EsquemaHibrido;
                opciones.DefaultChallengeScheme = EsquemaHibrido;
                opciones.DefaultForbidScheme = EsquemaHibrido;
            })
            .AddPolicyScheme(EsquemaHibrido, "Resolución de esquema híbrido", opciones =>
            {
                opciones.ForwardDefaultSelector = contexto =>
                {
                    if (!entra.EstaConfigurado)
                    {
                        return EsquemaLocal;
                    }

                    var autorizacion = contexto.Request.Headers.Authorization.ToString();

                    if (!autorizacion.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return EsquemaLocal;
                    }

                    var emisor = EventosJwtBearer.LeerEmisor(autorizacion["Bearer ".Length..].Trim());

                    return emisor is not null && emisor.Contains(entra.TenantId, StringComparison.OrdinalIgnoreCase)
                        ? EsquemaEntraId
                        : EsquemaLocal;
                };
            })
            .AddJwtBearer(EsquemaLocal, opciones => ConfigurarLocal(opciones, jwt))
            .AddJwtBearer(EsquemaEntraId, opciones => ConfigurarEntraId(opciones, entra));

        services.AddAuthorization();

        return services;
    }

    private static void ConfigurarLocal(JwtBearerOptions opciones, JwtOptions jwt)
    {
        opciones.MapInboundClaims = false;
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTipos.Nombre,
            RoleClaimType = ClaimTipos.Roles
        };
        opciones.Events = EventosJwtBearer.Crear(EsquemaLocal);
    }

    private static void ConfigurarEntraId(JwtBearerOptions opciones, EntraIdOptions entra)
    {
        opciones.Authority = entra.Authority;
        opciones.Audience = entra.AudienciaEfectiva;
        opciones.MapInboundClaims = false;
        opciones.TokenValidationParameters = ConfiguracionTokenEntra.Crear(entra, validarEmisor: false);

        var eventos = EventosJwtBearer.Crear(EsquemaEntraId);
        eventos.OnTokenValidated = contexto =>
        {
            var inquilino = contexto.Principal?.FindFirst(ClaimTipos.Tid)?.Value;

            if (!string.Equals(inquilino, entra.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                contexto.Fail(new SecurityTokenInvalidIssuerException(
                    "El token no pertenece al inquilino institucional configurado."));

                return Task.CompletedTask;
            }

            contexto.Principal?.Identities.FirstOrDefault()?.AddClaim(
                new Claim(ClaimTipos.Esquema, EsquemaEntraId));

            return Task.CompletedTask;
        };

        opciones.Events = eventos;
    }
}
