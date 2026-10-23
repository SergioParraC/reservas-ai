using Backend.Domain.Entities;
using Backend.Domain.ValueObjects;

namespace Backend.Domain.Services;

/// <summary>Causa por la que una franja no puede operar según el calendario institucional.</summary>
public enum MotivoCalendario
{
    /// <summary>La franja es operable dentro del periodo académico.</summary>
    Ninguno = 0,

    /// <summary>No existe un periodo académico activo.</summary>
    SinPeriodoActivo = 1,

    /// <summary>La franja queda fuera de la ventana del periodo activo.</summary>
    FueraDePeriodo = 2,

    /// <summary>La franja se cruza con una franja no operativa.</summary>
    NoOperativa = 3
}

/// <summary>Bloqueo concreto del calendario, ya expandido a una fecha y horario.</summary>
public sealed record IntervaloBloqueado(DateTimeOffset Inicio, DateTimeOffset Fin, string Motivo, bool EsFeriado)
{
    /// <summary>Franja del bloqueo con semántica semiabierta.</summary>
    public FranjaHoraria Franja => FranjaHoraria.Desde(Inicio, Fin);
}

/// <summary>
/// Resultado de evaluar una franja contra el calendario institucional. Cuando la franja cae
/// completamente dentro de un bloqueo, <see cref="CubiertaTotalmente"/> es verdadero y la
/// ocurrencia se omite en lugar de rechazarse.
/// </summary>
public sealed record EvaluacionCalendario(
    MotivoCalendario Motivo,
    bool CubiertaTotalmente,
    IntervaloBloqueado? Bloqueo);

/// <summary>
/// Calendario institucional ya resuelto para una ventana de fechas: el periodo académico activo
/// y los bloqueos no operativos expandidos. Es una función pura del estado persistido, de modo
/// que la generación de series y la consulta de disponibilidad comparten la misma regla.
/// </summary>
public sealed class CalendarioInstitucional
{
    private CalendarioInstitucional(PeriodoAcademico? periodoActivo, IReadOnlyList<IntervaloBloqueado> bloqueos)
    {
        PeriodoActivo = periodoActivo;
        Bloqueos = bloqueos;
    }

    /// <summary>Calendario sin periodo ni bloqueos; representa una institución sin configurar.</summary>
    public static CalendarioInstitucional Vacio { get; } = new(null, []);

    /// <summary>Periodo académico vigente, o <see langword="null"/> si no hay ninguno activo.</summary>
    public PeriodoAcademico? PeriodoActivo { get; }

    /// <summary>Bloqueos expandidos y ordenados por inicio.</summary>
    public IReadOnlyList<IntervaloBloqueado> Bloqueos { get; }

    /// <summary>Indica si existe un periodo académico activo.</summary>
    public bool TienePeriodoActivo => PeriodoActivo is not null;

    /// <summary>
    /// Construye el calendario expandiendo las franjas no operativas de la ventana indicada.
    /// </summary>
    /// <param name="periodoActivo">Periodo vigente o <see langword="null"/>.</param>
    /// <param name="franjas">Franjas no operativas configuradas.</param>
    /// <param name="desde">Primer día a expandir.</param>
    /// <param name="hasta">Último día a expandir, inclusive.</param>
    /// <param name="zona">Zona horaria institucional.</param>
    public static CalendarioInstitucional Crear(
        PeriodoAcademico? periodoActivo,
        IEnumerable<FranjaNoOperativa>? franjas,
        DateOnly desde,
        DateOnly hasta,
        TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        if (hasta < desde)
        {
            (desde, hasta) = (hasta, desde);
        }

        var bloqueos = new List<IntervaloBloqueado>();

        foreach (var franja in franjas ?? [])
        {
            if (!franja.Activa)
            {
                continue;
            }

            for (var fecha = desde; fecha <= hasta; fecha = fecha.AddDays(1))
            {
                if (franja.Expandir(fecha, zona) is { } intervalo)
                {
                    bloqueos.Add(new IntervaloBloqueado(
                        intervalo.Inicio,
                        intervalo.Fin,
                        franja.Motivo,
                        franja.EsFeriado));
                }
            }
        }

        return new CalendarioInstitucional(
            periodoActivo,
            bloqueos.OrderBy(bloqueo => bloqueo.Inicio).ToArray());
    }

    /// <summary>Primer instante de la ventana académica en la zona indicada.</summary>
    public DateTimeOffset InicioVentana(TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        return PeriodoActivo is null
            ? DateTimeOffset.MinValue
            : MotorDisponibilidad.InicioEnZona(PeriodoActivo.FechaInicio, TimeOnly.MinValue, zona);
    }

    /// <summary>Instante exclusivo de cierre de la ventana académica en la zona indicada.</summary>
    public DateTimeOffset FinVentanaExclusivo(TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        return PeriodoActivo is null
            ? DateTimeOffset.MaxValue
            : MotorDisponibilidad.InicioEnZona(PeriodoActivo.FechaFin.AddDays(1), TimeOnly.MinValue, zona);
    }

    /// <summary>Indica si la franja cae completamente dentro del periodo académico activo.</summary>
    public bool DentroDelPeriodo(FranjaHoraria franja, TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(franja);

        return PeriodoActivo is not null
            && franja.Inicio >= InicioVentana(zona)
            && franja.Fin <= FinVentanaExclusivo(zona);
    }

    /// <summary>
    /// Evalúa la franja en el orden calendario: periodo activo, ventana académica y bloqueos.
    /// </summary>
    public EvaluacionCalendario Evaluar(FranjaHoraria franja, TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(franja);

        if (PeriodoActivo is null)
        {
            return new EvaluacionCalendario(MotivoCalendario.SinPeriodoActivo, false, null);
        }

        if (!DentroDelPeriodo(franja, zona))
        {
            return new EvaluacionCalendario(MotivoCalendario.FueraDePeriodo, false, null);
        }

        foreach (var bloqueo in Bloqueos)
        {
            if (!bloqueo.Franja.SeSolapaCon(franja))
            {
                continue;
            }

            var cubiertaTotalmente = bloqueo.Inicio <= franja.Inicio && bloqueo.Fin >= franja.Fin;

            return new EvaluacionCalendario(MotivoCalendario.NoOperativa, cubiertaTotalmente, bloqueo);
        }

        return new EvaluacionCalendario(MotivoCalendario.Ninguno, false, null);
    }

    /// <summary>Indica si la fecha completa es no operativa (día cerrado o feriado).</summary>
    public bool DiaNoOperativo(DateOnly fecha, TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        var inicio = MotorDisponibilidad.InicioEnZona(fecha, TimeOnly.MinValue, zona);
        var fin = MotorDisponibilidad.InicioEnZona(fecha.AddDays(1), TimeOnly.MinValue, zona);

        return Bloqueos.Any(bloqueo => bloqueo.Inicio <= inicio && bloqueo.Fin >= fin);
    }
}
