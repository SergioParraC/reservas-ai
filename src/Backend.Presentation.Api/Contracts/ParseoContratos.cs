namespace Backend.Presentation.Api.Contracts;

/// <summary>Conversión tolerante de los nombres de enum que envía la SPA.</summary>
public static class ParseoContratos
{
    /// <summary>Convierte un nombre de enum sin distinguir mayúsculas y exige que esté definido.</summary>
    public static bool TryParseEnum<TEnum>(string? texto, out TEnum valor)
        where TEnum : struct, Enum
    {
        valor = default;

        return !string.IsNullOrWhiteSpace(texto)
            && Enum.TryParse(texto.Trim(), ignoreCase: true, out valor)
            && Enum.IsDefined(valor);
    }

    /// <summary>Convierte una hora local en formato <c>HH:mm</c>.</summary>
    public static bool TryParseHora(string? texto, out TimeOnly hora) =>
        TimeOnly.TryParse(
            texto,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out hora);

    /// <summary>Convierte una fecha en formato <c>yyyy-MM-dd</c>.</summary>
    public static bool TryParseFecha(string? texto, out DateOnly fecha) =>
        DateOnly.TryParseExact(
            texto,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out fecha);
}

