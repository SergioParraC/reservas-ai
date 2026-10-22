using Backend.Application.Autenticacion;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de resolución del perfil institucional a partir de una identidad federada de Entra ID.
/// Implementado en Infrastructure con acceso al almacén de identidad.
/// </summary>
public interface IEntraIdProfileResolver
{
    /// <summary>
    /// Resuelve el perfil local por el <c>oid</c> del token, o lo aprovisiona si el rol
    /// institucional es académico (<c>Docente</c> o <c>Estudiante</c>).
    /// </summary>
    Task<ResultadoPerfilFederado> ResolverAsync(IdentidadFederada identidad, CancellationToken cancellationToken = default);
}
