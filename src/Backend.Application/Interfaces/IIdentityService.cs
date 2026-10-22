using Backend.Application.Autenticacion;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de gestión de identidad. La implementación (Infrastructure) es la única que conoce
/// <c>UserManager&lt;Usuario&gt;</c> y <c>RoleManager&lt;Rol&gt;</c>; la capa de aplicación
/// solo ve <see cref="CuentaUsuario"/> y <see cref="ResultadoCredenciales"/>.
/// </summary>
public interface IIdentityService
{
    /// <summary>Localiza una cuenta por correo institucional o devuelve <see langword="null"/>.</summary>
    Task<CuentaUsuario?> BuscarPorCorreoAsync(string correoInstitucional, CancellationToken cancellationToken = default);

    /// <summary>Localiza una cuenta por su identificador interno o devuelve <see langword="null"/>.</summary>
    Task<CuentaUsuario?> BuscarPorIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Localiza una cuenta por su identificador federado (<c>oid</c>) o devuelve <see langword="null"/>.</summary>
    Task<CuentaUsuario?> BuscarPorIdentificadorFederadoAsync(string identificadorFederado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un perfil federado con el rol académico indicado. Nunca asigna roles administrativos.
    /// </summary>
    Task<CuentaUsuario?> CrearPerfilFederadoAsync(AltaPerfilFederado alta, string rol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica las credenciales locales aplicando el control de bloqueo del almacén de identidad.
    /// La implementación debe consumir un tiempo comparable cuando el correo no existe,
    /// para no revelar la existencia de la cuenta por tiempo de respuesta.
    /// </summary>
    Task<ResultadoCredenciales> VerificarCredencialesAsync(CredencialesLocales credenciales, CancellationToken cancellationToken = default);

    /// <summary>Obtiene los roles concedidos a la cuenta.</summary>
    Task<IReadOnlyList<string>> ObtenerRolesAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Actualiza la marca de tiempo del último acceso autorizado.</summary>
    Task ActualizarUltimoAccesoAsync(Guid usuarioId, DateTimeOffset momento, CancellationToken cancellationToken = default);
}
