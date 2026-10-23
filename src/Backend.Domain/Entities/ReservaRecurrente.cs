using System.Globalization;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;

namespace Backend.Domain.Entities;

/// <summary>
/// Ocurrencias generadas por una serie: las que se persisten y las omitidas por coincidir
/// completamente con una franja no operativa.
/// </summary>
public sealed record OcurrenciasGeneradas(
    IReadOnlyList<FranjaHoraria> Ocurrencias,
    IReadOnlyList<FranjaHoraria> Omitidas);

/// <summary>
/// Serie semestral de un área completa. Es la raíz del agregado que conserva lo declarado por
/// el docente (patrón y periodo) y genera las ocurrencias que se materializan en la agenda.
/// </summary>
public sealed class ReservaRecurrente
{
    /// <summary>Longitud máxima de la materia asociada a la serie.</summary>
    public const int LongitudMaximaMateria = 120;

    /// <summary>Separador de las fechas fijas persistidas.</summary>
    public const char SeparadorFechas = ',';

    private ReservaRecurrente()
    {
    }

    private ReservaRecurrente(
        Guid id,
        Guid espacioId,
        Guid docenteId,
        PatronRecurrencia patron,
        PeriodoSemestral periodo,
        string materia,
        DateTimeOffset creadaEn)
    {
        Id = id;
        EspacioId = espacioId;
        DocenteId = docenteId;
        TipoRecurrencia = patron.Tipo;
        DiaSemana = patron.DiaSemana;
        FechasFijas = SerializarFechas(patron.FechasFijas);
        HoraInicio = patron.HoraInicio;
        HoraFin = patron.HoraFin;
        PeriodoInicio = periodo.Inicio;
        PeriodoFin = periodo.Fin;
        Materia = materia;
        CreadaEn = creadaEn;
    }

    /// <summary>Identificador interno de la serie.</summary>
    public Guid Id { get; private set; }

    /// <summary>Área completa reservada por la serie.</summary>
    public Guid EspacioId { get; private set; }

    /// <summary>Docente titular de la serie.</summary>
    public Guid DocenteId { get; private set; }

    /// <summary>Cadencia declarada.</summary>
    public TipoRecurrencia TipoRecurrencia { get; private set; }

    /// <summary>Día de la semana del patrón recurrente.</summary>
    public DayOfWeek? DiaSemana { get; private set; }

    /// <summary>Fechas fijas serializadas como <c>yyyy-MM-dd</c> separadas por coma.</summary>
    public string? FechasFijas { get; private set; }

    /// <summary>Hora local de inicio de cada ocurrencia.</summary>
    public TimeOnly HoraInicio { get; private set; }

    /// <summary>Hora local de fin de cada ocurrencia.</summary>
    public TimeOnly HoraFin { get; private set; }

    /// <summary>Primer día del periodo declarado.</summary>
    public DateOnly PeriodoInicio { get; private set; }

    /// <summary>Último día del periodo declarado.</summary>
    public DateOnly PeriodoFin { get; private set; }

    /// <summary>Materia o asignatura asociada a la serie.</summary>
    public string Materia { get; private set; } = string.Empty;

    /// <summary>Momento del alta.</summary>
    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>Token de concurrencia optimista gestionado por la base de datos.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Periodo semestral declarado como valor derivado.</summary>
    public PeriodoSemestral Periodo => PeriodoSemestral.Crear(PeriodoInicio, PeriodoFin);

    /// <summary>Patrón declarado como valor derivado de las columnas persistidas.</summary>
    public PatronRecurrencia Patron => PatronRecurrencia.Crear(
        TipoRecurrencia,
        DiaSemana,
        DeserializarFechas(FechasFijas),
        HoraInicio,
        HoraFin);

    /// <summary>
    /// Crea la cabecera de la serie validando el patrón, el periodo y la materia. Las
    /// ocurrencias se generan después contra el calendario institucional.
    /// </summary>
    public static ReservaRecurrente Crear(
        Guid espacioId,
        Guid docenteId,
        PatronRecurrencia patron,
        PeriodoSemestral periodo,
        string materia,
        DateTimeOffset creadaEn)
    {
        ArgumentNullException.ThrowIfNull(patron);
        ArgumentNullException.ThrowIfNull(periodo);

        if (espacioId == Guid.Empty)
        {
            throw new DomainException(MensajesSemestre.EspacioNoEncontrado);
        }

        if (docenteId == Guid.Empty)
        {
            throw new DomainException(MensajesReserva.SinSesion);
        }

        if (string.IsNullOrWhiteSpace(materia))
        {
            throw new DomainException(MensajesSemestre.MateriaRequerida);
        }

        var materiaNormalizada = materia.Trim();

        if (materiaNormalizada.Length > LongitudMaximaMateria)
        {
            throw new DomainException(MensajesSemestre.MateriaDemasiadoLarga);
        }

        return new ReservaRecurrente(
            Guid.NewGuid(),
            espacioId,
            docenteId,
            patron,
            periodo,
            materiaNormalizada,
            creadaEn);
    }

    /// <summary>Indica si la serie pertenece al docente indicado.</summary>
    public bool PerteneceA(Guid docenteId) => DocenteId == docenteId;

    /// <summary>
    /// Expande el patrón sobre el periodo y evalúa cada ocurrencia contra el calendario:
    /// las cubiertas completamente por un bloqueo se omiten; las parciales se rechazan.
    /// </summary>
    /// <param name="calendario">Calendario institucional ya expandido.</param>
    /// <param name="zona">Zona horaria institucional.</param>
    /// <param name="semanasMaximas">Máximo de semanas admitido por serie.</param>
    /// <param name="ahora">Instante actual del servidor.</param>
    /// <param name="antelacionMinima">Antelación mínima de la primera ocurrencia.</param>
    public OcurrenciasGeneradas GenerarOcurrencias(
        CalendarioInstitucional calendario,
        TimeZoneInfo zona,
        int semanasMaximas,
        DateTimeOffset ahora,
        TimeSpan antelacionMinima)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        ArgumentNullException.ThrowIfNull(zona);

        var periodo = Periodo;

        ValidarSemanas(periodo, semanasMaximas);

        var fechas = Patron.GenerarFechas(periodo);
        var ocurrencias = new List<FranjaHoraria>();
        var omitidas = new List<FranjaHoraria>();

        foreach (var fecha in fechas)
        {
            var franja = FranjaHoraria.Desde(
                MotorDisponibilidad.InicioEnZona(fecha, HoraInicio, zona),
                MotorDisponibilidad.InicioEnZona(fecha, HoraFin, zona));

            var evaluacion = calendario.Evaluar(franja, zona);

            switch (evaluacion.Motivo)
            {
                case MotivoCalendario.SinPeriodoActivo:
                    throw new ConflictoException(MensajesCalendario.SinPeriodoActivo);
                case MotivoCalendario.FueraDePeriodo:
                    throw new ConflictoException(MensajesCalendario.FueraDePeriodoAcademico);
                case MotivoCalendario.NoOperativa when evaluacion.CubiertaTotalmente:
                    omitidas.Add(franja);
                    continue;
                case MotivoCalendario.NoOperativa:
                    throw new ConflictoException(string.Format(
                        CultureInfo.InvariantCulture,
                        MensajesCalendario.FranjaNoOperativaConFecha,
                        fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        evaluacion.Bloqueo?.Motivo ?? string.Empty));
                default:
                    ocurrencias.Add(franja);
                    break;
            }
        }

        if (ocurrencias.Count == 0)
        {
            throw new DomainException(MensajesSemestre.SerieSinOcurrencias);
        }

        if (ocurrencias[0].Inicio < ahora + antelacionMinima)
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesSemestre.AntelacionMinima,
                (int)antelacionMinima.TotalDays));
        }

        return new OcurrenciasGeneradas(ocurrencias, omitidas);
    }

    /// <summary>Valida que el periodo no supere el máximo de semanas configurado.</summary>
    public static void ValidarSemanas(PeriodoSemestral periodo, int semanasMaximas)
    {
        ArgumentNullException.ThrowIfNull(periodo);

        if (semanasMaximas < 1 || periodo.Semanas > semanasMaximas)
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesSemestre.PeriodoExcedido,
                semanasMaximas));
        }
    }

    private static string? SerializarFechas(IReadOnlyList<DateOnly> fechas) =>
        fechas.Count == 0
            ? null
            : string.Join(SeparadorFechas, fechas.Select(fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

    private static IReadOnlyList<DateOnly> DeserializarFechas(string? fechas) =>
        string.IsNullOrWhiteSpace(fechas)
            ? []
            : fechas
                .Split(SeparadorFechas, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(fecha => DateOnly.ParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToArray();
}
