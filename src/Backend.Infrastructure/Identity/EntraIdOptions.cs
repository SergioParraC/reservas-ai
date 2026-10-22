namespace Backend.Infrastructure.Identity;

/// <summary>
/// Configuración del inquilino institucional de Microsoft Entra ID (single-tenant).
/// </summary>
public sealed class EntraIdOptions
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "EntraId";

    /// <summary>Identificador del inquilino institucional.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Identificador del registro de aplicación que expone la API.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Audiencia esperada; si está vacía se usa <see cref="ClientId"/>.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Autoridad explícita para metadatos simulados (pruebas de integración).
    /// Si está vacía se deriva del <see cref="TenantId"/>.
    /// </summary>
    public string AuthorityOverride { get; set; } = string.Empty;

    /// <summary>Autoridad OIDC del inquilino (metadatos y JWKS).</summary>
    public string Authority => string.IsNullOrWhiteSpace(AuthorityOverride)
        ? $"https://login.microsoftonline.com/{TenantId}/v2.0"
        : AuthorityOverride.Trim();

    /// <summary>Audiencia efectiva del token federado.</summary>
    public string AudienciaEfectiva => string.IsNullOrWhiteSpace(Audience) ? ClientId : Audience;

    /// <summary>Indica si el inquilino está configurado para aceptar identidades federadas.</summary>
    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>Valida la coherencia de la configuración cuando el inquilino está definido.</summary>
    public void Validar()
    {
        if (!EstaConfigurado)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(AudienciaEfectiva))
        {
            throw new InvalidOperationException("Falta la configuración 'EntraId:Audience' o 'EntraId:ClientId'.");
        }
    }
}
