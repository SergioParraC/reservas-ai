using System.Globalization;
using Backend.Domain.Exceptions;

namespace Backend.Domain.ValueObjects;

/// <summary>
/// Intervalo horario inmutable con semántica semiabierta <c>[Inicio, Fin)</c>: dos franjas
/// contiguas no se solapan y una reserva que empieza justo al terminar otra es válida.
/// </summary>
public sealed record FranjaHoraria
{
    private FranjaHoraria(DateTimeOffset inicio, DateTimeOffset fin)
    {
        Inicio = inicio;
        Fin = fin;
    }

    /// <summary>Instante de inicio, inclusive.</summary>
    public DateTimeOffset Inicio { get; }

    /// <summary>Instante de fin, exclusivo.</summary>
    public DateTimeOffset Fin { get; }

    /// <summary>Duración efectiva de la franja.</summary>
    public TimeSpan Duracion => Fin - Inicio;

    /// <summary>
    /// Crea una franja nueva validando duración, alineación, jornada y horizonte contra la política.
    /// </summary>
    /// <param name="inicio">Inicio de la franja en la zona horaria institucional.</param>
    /// <param name="fin">Fin de la franja en la misma zona.</param>
    /// <param name="politica">Política vigente de reservas.</param>
    /// <param name="ahora">Instante actual del servidor.</param>
    public static FranjaHoraria Crear(
        DateTimeOffset inicio,
        DateTimeOffset fin,
        PoliticaReserva politica,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(politica);

        if (inicio.Offset != fin.Offset)
        {
            throw new DomainException(MensajesReserva.DesplazamientoInvalido);
        }

        if (fin <= inicio)
        {
            throw new DomainException(MensajesReserva.FranjaInvalida);
        }

        var duracion = fin - inicio;

        if (duracion < politica.DuracionMinima || duracion > politica.DuracionMaxima)
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesReserva.DuracionFueraDeRango,
                (int)politica.DuracionMinima.TotalMinutes,
                (int)politica.DuracionMaxima.TotalMinutes));
        }

        if (politica.IntervaloMinutos < 1 || inicio.TimeOfDay.TotalMinutes % politica.IntervaloMinutos != 0)
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesReserva.InicioNoAlineado,
                politica.IntervaloMinutos));
        }

        if (!politica.DentroDeJornada(TimeOnly.FromTimeSpan(inicio.TimeOfDay))
            || !politica.DentroDeJornada(TimeOnly.FromTimeSpan(fin.TimeOfDay)))
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesReserva.FranjaFueraDeJornada,
                politica.HoraApertura.ToString("HH\\:mm", CultureInfo.InvariantCulture),
                politica.HoraCierre.ToString("HH\\:mm", CultureInfo.InvariantCulture)));
        }

        if (inicio < ahora + politica.AntelacionMinima)
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesReserva.InicioFueraDeAntelacion,
                (int)politica.AntelacionMinima.TotalMinutes));
        }

        if (inicio > ahora + politica.AntelacionMaxima)
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesReserva.InicioFueraDeHorizonte,
                (int)politica.AntelacionMaxima.TotalDays));
        }

        return new FranjaHoraria(inicio, fin);
    }

    /// <summary>Rehidrata una franja ya persistida o calculada, sin aplicar la política.</summary>
    public static FranjaHoraria Desde(DateTimeOffset inicio, DateTimeOffset fin)
    {
        if (fin <= inicio)
        {
            throw new DomainException(MensajesReserva.FranjaInvalida);
        }

        return new FranjaHoraria(inicio, fin);
    }

    /// <summary>Indica si la franja se cruza con otra, excluyendo el contacto en los extremos.</summary>
    public bool SeSolapaCon(FranjaHoraria otra)
    {
        ArgumentNullException.ThrowIfNull(otra);

        return Inicio < otra.Fin && otra.Inicio < Fin;
    }

    /// <summary>Indica si el instante cae dentro de la franja.</summary>
    public bool Contiene(DateTimeOffset momento) => momento >= Inicio && momento < Fin;

    /// <summary>Desplaza la franja completa conservando su duración.</summary>
    public FranjaHoraria Desplazar(TimeSpan delta) => new(Inicio + delta, Fin + delta);

    /// <inheritdoc />
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0:O} - {1:O}", Inicio, Fin);
}
