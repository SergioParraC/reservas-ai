using Backend.Application.Autenticacion;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de resolución del perfil institucional a partir de una identidad federada de Entra ID.
/// Implementado en Infrastructure con acceso al almacén de identidad.
/// </summary>
public interface IEntraIdProfileResolver
{
    /// <summary>
    /// Resuelve el perfil local por el <c>oid</c> vinculado. No aprovisiona cuentas:
    /// una identidad sin vínculo debe registrarse y vincularse desde la configuración del usuario.
    /// </summary>
    Task<ResultadoPerfilFederado> ResolverAsync(IdentidadFederada identidad, CancellationToken cancellationToken = default);
}
