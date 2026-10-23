using Backend.Application.Autenticacion;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de gestión de identidad. La implementación (Infrastructure) es la única que conoce
/// <c>UserManager&lt;Usuario&gt;</c> y <c>RoleManager&lt;Rol&gt;</c>; la capa de aplicación
/// solo ve los contratos de <see cref="Backend.Application.Autenticacion"/>.
/// </summary>
public interface IIdentityService
{
    /// <summary>Localiza una cuenta por correo institucional o devuelve <see langword="null"/>.</summary>
    Task<CuentaUsuario?> BuscarPorCorreoAsync(string correoInstitucional, CancellationToken cancellationToken = default);

    /// <summary>Localiza una cuenta por su identificador interno o devuelve <see langword="null"/>.</summary>
    Task<CuentaUsuario?> BuscarPorIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Localiza una cuenta por su identificador federado (<c>oid</c>) o devuelve <see langword="null"/>.</summary>
    Task<CuentaUsuario?> BuscarPorIdentificadorFederadoAsync(string identificadorFederado, CancellationToken cancellationToken = default);

    /// <summary>Cuenta las cuentas registradas en la plataforma.</summary>
    Task<int> ContarCuentasAsync(CancellationToken cancellationToken = default);

    /// <summary>Cuenta los administradores activos del sistema.</summary>
    Task<int> ContarAdministradoresActivosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una cuenta local dentro de una transacción serializable y le asigna el rol
    /// <c>Administrativo</c> si es la primera cuenta del sistema, o <c>Estudiante</c> en otro caso.
    /// </summary>
    Task<ResultadoCreacionCuenta> RegistrarCuentaLocalAsync(RegistroCuentaLocal registro, CancellationToken cancellationToken = default);

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

    /// <summary>Reemplaza el rol vigente de la cuenta y sincroniza su tipo de actor.</summary>
    Task<ResultadoOperacionIdentidad> CambiarRolAsync(Guid usuarioId, string rol, CancellationToken cancellationToken = default);

    /// <summary>Asocia el identificador federado a la cuenta; es idempotente para el mismo valor.</summary>
    Task<ResultadoOperacionIdentidad> VincularIdentidadAsync(Guid usuarioId, string identificadorFederado, CancellationToken cancellationToken = default);

    /// <summary>Retira el identificador federado de la cuenta sin tocar su contraseña local.</summary>
    Task<ResultadoOperacionIdentidad> DesvincularIdentidadAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
