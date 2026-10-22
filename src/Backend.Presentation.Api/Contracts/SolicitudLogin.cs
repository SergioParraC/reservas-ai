using System.ComponentModel.DataAnnotations;

namespace Backend.Presentation.Api.Contracts;

/// <summary>Cuerpo del inicio de sesión con credenciales locales.</summary>
public sealed class SolicitudLogin
{
    /// <summary>Correo institucional del usuario.</summary>
    [Required(ErrorMessage = "El correo institucional es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo institucional no tiene un formato válido.")]
    public string CorreoInstitucional { get; init; } = string.Empty;

    /// <summary>Contraseña local.</summary>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Contrasena { get; init; } = string.Empty;
}
