using Backend.Domain.Exceptions;

namespace Backend.Domain.ValueObjects;

/// <summary>
/// Ventana semestral declarada para una serie: primer y último día, ambos inclusive. Acota la
/// generación de ocurrencias y el máximo de semanas admitido por configuración.
/// </summary>
public sealed record PeriodoSemestral
{
    private PeriodoSemestral(DateOnly inicio, DateOnly fin)
    {
        Inicio = inicio;
        Fin = fin;
    }

    /// <summary>Primer día del periodo, inclusive.</summary>
    public DateOnly Inicio { get; }

    /// <summary>Último día del periodo, inclusive.</summary>
    public DateOnly Fin { get; }

    /// <summary>Cantidad de días que cubre el periodo.</summary>
    public int Dias => Fin.DayNumber - Inicio.DayNumber + 1;

    /// <summary>Cantidad de semanas (redondeada hacia arriba) que cubre el periodo.</summary>
    public int Semanas => (int)Math.Ceiling(Dias / 7d);

    /// <summary>Crea el periodo validando que el rango no sea inverso.</summary>
    public static PeriodoSemestral Crear(DateOnly inicio, DateOnly fin)
    {
        if (fin < inicio)
        {
            throw new DomainException(MensajesSemestre.PeriodoInvalido);
        }

        return new PeriodoSemestral(inicio, fin);
    }

    /// <summary>Indica si la fecha cae dentro de la ventana.</summary>
    public bool Contiene(DateOnly fecha) => fecha >= Inicio && fecha <= Fin;

    /// <summary>Indica si la ventana completa cae dentro del periodo académico indicado.</summary>
    public bool EstaContenidoEn(DateOnly inicioAcademico, DateOnly finAcademico) =>
        Inicio >= inicioAcademico && Fin <= finAcademico;

    /// <inheritdoc />
    public override string ToString() => $"{Inicio:yyyy-MM-dd} - {Fin:yyyy-MM-dd}";
}
