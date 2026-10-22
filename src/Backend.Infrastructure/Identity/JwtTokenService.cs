using System.Text;
using Backend.Application.Autenticacion;
using Backend.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Infrastructure.Identity;

/// <summary>
/// Emisor de los access tokens locales: JWT firmado con HMAC-SHA256, expiración absoluta
/// y sin datos sensibles del almacén de identidad.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _opciones;
    private readonly JsonWebTokenHandler _manejador = new() { SetDefaultTimesOnTokenCreation = false };
    private readonly SigningCredentials _credenciales;

    /// <summary>Inicializa el emisor con la configuración validada de JWT.</summary>
    public JwtTokenService(IOptions<JwtOptions> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _opciones = opciones.Value;
        _opciones.Validar();

        _credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Key)),
            SecurityAlgorithms.HmacSha256);
    }

    /// <inheritdoc />
    public TokenEmitido Emitir(PerfilUsuario perfil, DateTimeOffset emitidoEn)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        var expiraEn = emitidoEn.AddMinutes(_opciones.ExpiraMinutos);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opciones.Issuer,
            Audience = _opciones.Audience,
            IssuedAt = emitidoEn.UtcDateTime,
            Expires = expiraEn.UtcDateTime,
            SigningCredentials = _credenciales,
            Claims = new Dictionary<string, object>
            {
                [ClaimTipos.Sub] = perfil.Id.ToString(),
                [ClaimTipos.Jti] = Guid.NewGuid().ToString(),
                [ClaimTipos.Email] = perfil.CorreoInstitucional,
                [ClaimTipos.Nombre] = perfil.NombreCompleto,
                [ClaimTipos.Roles] = perfil.Roles.ToArray()
            }
        };

        var token = _manejador.CreateToken(descriptor);

        return TokenEmitido.Bearer(token, expiraEn, perfil.Roles.ToArray());
    }
}
