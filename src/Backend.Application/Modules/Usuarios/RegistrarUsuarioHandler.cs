using System.Net.Mail;
using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Usuarios.Dtos;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Usuarios;

/// <summary>
/// Autorregistro público de cuentas. El rol inicial lo decide el almacén de identidad dentro
/// de una transacción serializable: la primera cuenta es <c>Administrativo</c> y el resto
/// nace como <c>Estudiante</c>. La respuesta nunca emite token de acceso.
/// </summary>
public sealed class RegistrarUsuarioHandler
{
    private readonly IIdentityService _identidad;
    private readonly OpcionesIdentidad _opciones;

    /// <summary>Inicializa el caso de uso con el almacén de identidad y la política de registro.</summary>
    public RegistrarUsuarioHandler(IIdentityService identidad, OpcionesIdentidad opciones)
    {
        _identidad = identidad ?? throw new ArgumentNullException(nameof(identidad));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
    }

    /// <summary>Registra la cuenta y devuelve su rol inicial.</summary>
    public async Task<Resultado<ResultadoRegistroUsuario>> HandleAsync(
        RegistroCuentaLocal solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!_opciones.PermitirAutorregistro)
        {
            return Resultado<ResultadoRegistroUsuario>.Fallo(
                TipoFallo.NoPermitido,
                "El autorregistro de cuentas está deshabilitado.");
        }

        var nombre = solicitud.NombreCompleto?.Trim() ?? string.Empty;
        var correo = solicitud.CorreoInstitucional?.Trim() ?? string.Empty;
        var contrasena = solicitud.Contrasena ?? string.Empty;

        if (nombre.Length == 0)
        {
            return Validacion("nombreCompleto", MensajesIdentidad.NombreCompletoRequerido);
        }

        if (nombre.Length > 200)
        {
            return Validacion("nombreCompleto", "El nombre completo no puede superar los 200 caracteres.");
        }

        if (!MailAddress.TryCreate(correo, out _))
        {
            return Validacion("correoInstitucional", "El correo institucional no tiene un formato válido.");
        }

        var dominios = _opciones.ObtenerDominios();

        if (dominios.Count > 0 && !PerteneceAlDominio(correo, dominios))
        {
            return Validacion(
                "correoInstitucional",
                $"El correo debe pertenecer al dominio institucional ({string.Join(", ", dominios)}).");
        }

        if (contrasena.Length == 0)
        {
            return Validacion("contrasena", "La contraseña es obligatoria.");
        }

        if (await _identidad.BuscarPorCorreoAsync(correo, cancellationToken) is not null)
        {
            return ConflictoCorreo();
        }

        var resultado = await _identidad.RegistrarCuentaLocalAsync(
            new RegistroCuentaLocal(nombre, correo, contrasena),
            cancellationToken);

        if (resultado.CorreoDuplicado)
        {
            return ConflictoCorreo();
        }

        if (!resultado.Exitoso || resultado.Cuenta is null || resultado.Rol is null)
        {
            var detalle = resultado.Errores.Count == 0
                ? "No fue posible registrar la cuenta."
                : string.Join(" ", resultado.Errores);

            return Validacion("contrasena", detalle);
        }

        return Resultado<ResultadoRegistroUsuario>.Exito(new ResultadoRegistroUsuario(
            resultado.Cuenta.Id,
            resultado.Cuenta.NombreCompleto,
            resultado.Cuenta.CorreoInstitucional,
            resultado.Rol));
    }

    private static bool PerteneceAlDominio(string correo, IReadOnlyList<string> dominios)
    {
        var separador = correo.LastIndexOf('@');

        if (separador < 0)
        {
            return false;
        }

        var dominio = correo[(separador + 1)..].Trim().Trim('.');

        return dominios.Any(candidato =>
            string.Equals(NormalizarDominio(candidato), dominio, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Acepta el dominio con o sin el prefijo <c>@</c> y con puntos decorativos, para que
    /// configurar <c>@uninpahu.edu.co</c> o <c>uninpahu.edu.co</c> sea equivalente.
    /// </summary>
    private static string NormalizarDominio(string? dominio) =>
        (dominio ?? string.Empty).Trim().TrimStart('@').Trim('.');

    private static Resultado<ResultadoRegistroUsuario> Validacion(string campo, string mensaje) =>
        Resultado<ResultadoRegistroUsuario>.Fallo(
            TipoFallo.Validacion,
            mensaje,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [campo] = [mensaje] });

    private static Resultado<ResultadoRegistroUsuario> ConflictoCorreo() =>
        Resultado<ResultadoRegistroUsuario>.Fallo(
            TipoFallo.Conflicto,
            MensajesIdentidad.CorreoYaRegistrado,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["correoInstitucional"] = [MensajesIdentidad.CorreoYaRegistrado] });
}
