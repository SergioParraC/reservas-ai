using Backend.Domain.Exceptions;

namespace Backend.Presentation.Api.Problemas;

/// <summary>
/// Traduce los mensajes del calendario institucional a los <c>type</c> estables del contrato
/// RFC 7807 y aporta sus títulos legibles.
/// </summary>
public static class ProblemasCalendario
{
    /// <summary>Slug del periodo que se cruza con otro registrado.</summary>
    public const string PeriodoSolapado = "periodo-solapado";

    /// <summary>Slug del periodo que no puede modificarse por tener ocupación confirmada.</summary>
    public const string PeriodoConOcupacion = "periodo-con-ocupacion";

    /// <summary>Slug de la franja no operativa duplicada.</summary>
    public const string FranjaDuplicada = "franja-duplicada";

    /// <summary>Slug de la operación sin periodo académico activo.</summary>
    public const string SinPeriodoActivo = "sin-periodo-activo";

    /// <summary>Slug de la franja fuera del periodo académico.</summary>
    public const string FueraDePeriodoAcademico = "fuera-de-periodo-academico";

    /// <summary>Slug de la franja no operativa.</summary>
    public const string FranjaNoOperativa = "franja-no-operativa";

    /// <summary>Devuelve el slug de conflicto del calendario o <see langword="null"/>.</summary>
    public static string? SlugConflicto(string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return null;
        }

        return mensaje switch
        {
            MensajesCalendario.PeriodoSolapado => PeriodoSolapado,
            MensajesCalendario.PeriodoConOcupacion => PeriodoConOcupacion,
            MensajesCalendario.FranjaDuplicada => FranjaDuplicada,
            MensajesCalendario.SinPeriodoActivo => SinPeriodoActivo,
            MensajesCalendario.FueraDePeriodoAcademico => FueraDePeriodoAcademico,
            _ => mensaje.StartsWith(Prefijo(MensajesCalendario.FranjaNoOperativaConFecha), StringComparison.Ordinal)
                || string.Equals(mensaje, MensajesCalendario.FranjaNoOperativa, StringComparison.Ordinal)
                ? FranjaNoOperativa
                : null
        };
    }

    /// <summary>Título legible del problema a partir de su slug.</summary>
    public static string? Titulo(string slug) => slug switch
    {
        PeriodoSolapado => "Periodo solapado",
        PeriodoConOcupacion => "Periodo con ocupación",
        FranjaDuplicada => "Franja no operativa duplicada",
        SinPeriodoActivo => "Sin periodo académico activo",
        FueraDePeriodoAcademico => "Fuera del periodo académico",
        FranjaNoOperativa => "Franja no operativa",
        _ => null
    };

    private static string Prefijo(string plantilla) =>
        plantilla[..plantilla.IndexOf('{', StringComparison.Ordinal)];
}
