using Backend.Application.Interfaces;

namespace Backend.Application.Autenticacion;

/// <summary>
/// Caso de uso que resuelve el perfil del portador autenticado a partir de su identificador interno.
/// Devuelve solo datos de negocio; nunca contraseñas, hashes ni sellos de seguridad.
/// </summary>
public sealed class ObtenerPerfilHandler
{
    private readonly IIdentityService _identidad;

    /// <summary>Inicializa el caso de uso con sus puertos.</summary>
    public ObtenerPerfilHandler(IIdentityService identidad)
    {
        _identidad = identidad ?? throw new ArgumentNullException(nameof(identidad));
    }

    /// <summary>Obtiene el perfil del usuario o <see langword="null"/> si no existe o está deshabilitado.</summary>
    public async Task<PerfilUsuario?> HandleAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var cuenta = await _identidad.BuscarPorIdAsync(usuarioId, cancellationToken);

        if (cuenta is null || !cuenta.Activo)
        {
            return null;
        }

        var roles = await _identidad.ObtenerRolesAsync(cuenta.Id, cancellationToken);

        return new PerfilUsuario(cuenta.Id, cuenta.NombreCompleto, cuenta.CorreoInstitucional, cuenta.TipoActor, roles);
    }
}
