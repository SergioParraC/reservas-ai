using Microsoft.IdentityModel.Tokens;

namespace Backend.Infrastructure.Identity;

/// <summary>
/// Construcción compartida de los parámetros de validación del token de Entra ID.
/// La usan el esquema JwtBearer federado y el validador de la vinculación, de modo que
/// no existan dos políticas de confianza distintas.
/// </summary>
public static class ConfiguracionTokenEntra
{
    /// <summary>
    /// Crea los parámetros con el inquilino y la audiencia institucional.
    /// El esquema JwtBearer obtiene el emisor de los metadatos y valida el inquilino en
    /// <c>OnTokenValidated</c>; el validador de vinculación lo exige explícitamente.
    /// </summary>
    public static TokenValidationParameters Crear(EntraIdOptions opciones, bool validarEmisor = true)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        return new TokenValidationParameters
        {
            ValidateIssuer = validarEmisor,
            ValidIssuer = validarEmisor ? opciones.Authority : null,
            ValidateAudience = true,
            ValidAudience = opciones.AudienciaEfectiva,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTipos.Nombre,
            RoleClaimType = ClaimTipos.Roles
        };
    }
}
