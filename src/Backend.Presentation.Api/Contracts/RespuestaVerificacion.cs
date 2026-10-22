namespace Backend.Presentation.Api.Contracts;

/// <summary>Respuesta del endpoint administrativo de verificación.</summary>
/// <param name="Mensaje">Mensaje de confirmación del acceso.</param>
/// <param name="CorreoInstitucional">Correo del portador autorizado.</param>
public sealed record RespuestaVerificacion(string Mensaje, string? CorreoInstitucional);
