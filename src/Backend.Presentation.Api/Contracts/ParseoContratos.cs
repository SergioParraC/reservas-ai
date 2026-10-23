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
}
