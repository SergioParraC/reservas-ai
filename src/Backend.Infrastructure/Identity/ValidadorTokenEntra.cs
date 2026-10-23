using System.Security.Claims;
using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Infrastructure.Identity;

/// <summary>
/// Valida un token de Entra ID presentado en el cuerpo de la solicitud de vinculación.
/// Comparte con el esquema JwtBearer federado la construcción de parámetros de confianza.
/// </summary>
public sealed class ValidadorTokenEntra : IValidadorTokenEntra
{
    private readonly EntraIdOptions _opciones;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configuracion;
    private readonly JsonWebTokenHandler _manejador = new() { MapInboundClaims = false };

    /// <summary>Inicializa el validador con la configuración del inquilino institucional.</summary>
    public ValidadorTokenEntra(IOptions<EntraIdOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _opciones = opciones.Value;
        _configuracion = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{_opciones.Authority.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());
    }

    /// <inheritdoc />
    public async Task<Resultado<IdentidadFederada>> ValidarAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Resultado<IdentidadFederada>.Fallo(TipoFallo.Validacion, "El token de Entra ID es obligatorio.");
        }

        if (!_opciones.EstaConfigurado)
        {
            return Resultado<IdentidadFederada>.Fallo(
                TipoFallo.NoPermitido,
                "El proveedor de identidad institucional no está configurado.");
        }

        try
        {
            var configuracion = await _configuracion.GetConfigurationAsync(cancellationToken);
            var parametros = ConfiguracionTokenEntra.Crear(_opciones);
            parametros.IssuerSigningKeys = configuracion.SigningKeys;

            var validacion = await _manejador.ValidateTokenAsync(token, parametros);

            if (!validacion.IsValid || validacion.ClaimsIdentity is null)
            {
                return Resultado<IdentidadFederada>.Fallo(TipoFallo.NoAutorizado, "El token de Entra ID no es válido.");
            }

            var principal = new ClaimsPrincipal(validacion.ClaimsIdentity);
            var inquilino = principal.FindFirst(ClaimTipos.Tid)?.Value;

            if (!string.Equals(inquilino, _opciones.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                return Resultado<IdentidadFederada>.Fallo(
                    TipoFallo.NoAutorizado,
                    "El token no pertenece al inquilino institucional configurado.");
            }

            var oid = principal.FindFirst(ClaimTipos.Oid)?.Value;

            if (string.IsNullOrWhiteSpace(oid))
            {
                return Resultado<IdentidadFederada>.Fallo(
                    TipoFallo.NoAutorizado,
                    "El token no contiene el identificador institucional inmutable.");
            }

            var correo = principal.FindFirst(ClaimTipos.Email)?.Value
                ?? principal.FindFirst("preferred_username")?.Value
                ?? string.Empty;

            var nombre = principal.FindFirst(ClaimTipos.Nombre)?.Value
                ?? (string.IsNullOrWhiteSpace(correo) ? oid : correo);

            var roles = principal.FindAll(ClaimTipos.Roles).Select(claim => claim.Value).ToArray();

            return Resultado<IdentidadFederada>.Exito(new IdentidadFederada(oid, nombre, correo, roles));
        }
        catch (Exception excepcion) when (excepcion is SecurityTokenException
            or IOException
            or HttpRequestException
            or InvalidOperationException
            or ArgumentException
            or System.Net.Sockets.SocketException)
        {
            return Resultado<IdentidadFederada>.Fallo(
                TipoFallo.NoAutorizado,
                "El token de Entra ID no es válido o no pudo validarse.");
        }
    }
}
