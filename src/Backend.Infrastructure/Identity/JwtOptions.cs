namespace Backend.Infrastructure.Identity;

/// <summary>
/// Configuración de emisión y validación de los tokens locales.
/// La clave de firma nunca se versiona: se inyecta por User Secrets o variables de entorno.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Jwt";

    /// <summary>Vigencia por defecto del access token local, en minutos.</summary>
    public const int ExpiraMinutosPorDefecto = 60;

    /// <summary>Longitud mínima exigida para la clave HMAC (256 bits).</summary>
    public const int LongitudMinimaClave = 32;

    /// <summary>Emisor propio de los tokens locales.</summary>
    public string Issuer { get; set; } = "https://api.reservas-espacios.edu";

    /// <summary>Audiencia esperada (la SPA institucional).</summary>
    public string Audience { get; set; } = "reservas-espacios-spa";

    /// <summary>Vigencia absoluta del access token, en minutos.</summary>
    public int ExpiraMinutos { get; set; } = ExpiraMinutosPorDefecto;

    /// <summary>Clave simétrica de firma; se inyecta fuera del repositorio.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Valida que la configuración permita emitir y validar tokens.</summary>
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("Falta la configuración 'Jwt:Issuer'.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Falta la configuración 'Jwt:Audience'.");
        }

        if (ExpiraMinutos <= 0)
        {
            throw new InvalidOperationException("La configuración 'Jwt:ExpiraMinutos' debe ser mayor que cero.");
        }

        if (string.IsNullOrWhiteSpace(Key) || Key.Length < LongitudMinimaClave)
        {
            throw new InvalidOperationException(
                $"La configuración 'Jwt:Key' es obligatoria y debe tener al menos {LongitudMinimaClave} caracteres. " +
                "Configúrela con 'dotnet user-secrets' en desarrollo o con una variable de entorno en contenedor.");
        }
    }
}
