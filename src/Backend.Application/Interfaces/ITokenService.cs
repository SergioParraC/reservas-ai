using Backend.Application.Autenticacion;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de emisión de tokens de acceso. La firma y la forma del token son detalles
/// de infraestructura; la capa de aplicación solo conoce el resultado emitido.
/// </summary>
public interface ITokenService
{
    /// <summary>Emite un token de acceso para el perfil indicado.</summary>
    TokenEmitido Emitir(PerfilUsuario perfil, DateTimeOffset emitidoEn);
}
