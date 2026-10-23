using System.Globalization;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;

namespace Backend.Domain.ValueObjects;

/// <summary>
/// Patrón periódico de una serie semestral. Genera las fechas candidatas del periodo sin
/// conocer la agenda ni la base de datos, de modo que la vista previa y validación del backend
/// comparten el mismo algoritmo.
/// </summary>
public sealed record PatronRecurrencia
{
    private PatronRecurrencia(
        TipoRecurrencia tipo,
        DayOfWeek? diaSemana,
        IReadOnlyList<DateOnly> fechasFijas,
        TimeOnly horaInicio,
        TimeOnly horaFin)
    {
        Tipo = tipo;
        DiaSemana = diaSemana;
        FechasFijas = fechasFijas;
        HoraInicio = horaInicio;
        HoraFin = horaFin;
    }

    /// <summary>Cadencia del patrón.</summary>
    public TipoRecurrencia Tipo { get; }

    /// <summary>Día de la semana del patrón semanal o bisemanal.</summary>
    public DayOfWeek? DiaSemana { get; }

    /// <summary>Fechas explícitas del patrón de fechas fijas, ordenadas y sin duplicados.</summary>
    public IReadOnlyList<DateOnly> FechasFijas { get; }

    /// <summary>Hora local de inicio de cada ocurrencia.</summary>
    public TimeOnly HoraInicio { get; }

    /// <summary>Hora local de fin de cada ocurrencia.</summary>
    public TimeOnly HoraFin { get; }

    /// <summary>
    /// Crea el patrón validando la coherencia entre el tipo de recurrencia, el día de la semana
    /// y las fechas fijas.
    /// </summary>
    public static PatronRecurrencia Crear(
        TipoRecurrencia tipo,
        DayOfWeek? diaSemana,
        IEnumerable<DateOnly>? fechasFijas,
        TimeOnly horaInicio,
        TimeOnly horaFin)
    {
        if (!Enum.IsDefined(tipo))
        {
            throw new DomainException(MensajesSemestre.TipoRecurrenciaInvalido);
        }

        if (horaFin <= horaInicio)
        {
            throw new DomainException(MensajesSemestre.FranjaPatronInvalida);
        }

        if (diaSemana is { } dia && !Enum.IsDefined(dia))
        {
            throw new DomainException(MensajesSemestre.TipoRecurrenciaInvalido);
        }

        var fechas = (fechasFijas ?? [])
            .Distinct()
            .OrderBy(fecha => fecha)
            .ToArray();

        return tipo switch
        {
            TipoRecurrencia.Semanal or TipoRecurrencia.Bisemanal => new PatronRecurrencia(
                tipo,
                diaSemana ?? throw new DomainException(MensajesSemestre.DiaSemanaRequerido),
                [],
                horaInicio,
                horaFin),
            TipoRecurrencia.FechasFijas when diaSemana is not null => throw new DomainException(
                MensajesSemestre.FechasFijasSinDia),
            TipoRecurrencia.FechasFijas when fechas.Length == 0 => throw new DomainException(
                MensajesSemestre.FechasFijasRequeridas),
            _ => new PatronRecurrencia(tipo, null, fechas, horaInicio, horaFin)
        };
    }

    /// <summary>
    /// Genera las fechas candidatas dentro del periodo. Las fechas fijas fuera del periodo se
    /// rechazan; las recurrentes se anclan a la primera coincidencia del día de la semana.
    /// </summary>
    public IReadOnlyList<DateOnly> GenerarFechas(PeriodoSemestral periodo)
    {
        ArgumentNullException.ThrowIfNull(periodo);

        if (Tipo == TipoRecurrencia.FechasFijas)
        {
            foreach (var fecha in FechasFijas)
            {
                if (!periodo.Contiene(fecha))
                {
                    throw new DomainException(string.Format(
                        CultureInfo.InvariantCulture,
                        MensajesSemestre.FechaFueraDePeriodo,
                        fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                }
            }

            return FechasFijas;
        }

        var paso = Tipo == TipoRecurrencia.Bisemanal ? 14 : 7;
        var ancla = periodo.Inicio;

        while (ancla.DayOfWeek != DiaSemana)
        {
            ancla = ancla.AddDays(1);

            if (ancla > periodo.Fin)
            {
                return [];
            }
        }

        var fechasGeneradas = new List<DateOnly>();

        for (var fecha = ancla; fecha <= periodo.Fin; fecha = fecha.AddDays(paso))
        {
            fechasGeneradas.Add(fecha);
        }

        return fechasGeneradas;
    }

    /// <summary>Cantidad máxima de ocurrencias posibles antes de evaluar el calendario.</summary>
    public int FechasMaximas(PeriodoSemestral periodo)
    {
        ArgumentNullException.ThrowIfNull(periodo);

        return GenerarFechas(periodo).Count;
    }
}
