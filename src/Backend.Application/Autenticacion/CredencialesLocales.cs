namespace Backend.Application.Autenticacion;

/// <summary>Credenciales locales enviadas al endpoint de acceso.</summary>
public sealed record CredencialesLocales(string CorreoInstitucional, string Contrasena);
