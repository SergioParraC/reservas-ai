using System.Globalization;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;

namespace Backend.Domain.Entities;

/// <summary>
/// Franja en la que la institución no opera. Aplica a todo el campus y se define por un día
/// de la semana recurrente o por una fecha específica, nunca por ambos criterios a la vez.
/// </summary>
public sealed class FranjaNoOperativa
{
    /// <summary>Longitud máxima del motivo de la franja.</summary>
    public const int LongitudMaximaMotivo = 120;

    /// <summary>Hora final que representa el cierre del día completo.</summary>
    public static readonly TimeOnly FinDeDia = new(23, 59);

    private FranjaNoOperativa()
    {
    }

    private FranjaNoOperativa(
        Guid id,
        DayOfWeek? diaSemana,
        DateOnly? fechaEspecifica,
        TimeOnly horaInicio,
        TimeOnly horaFin,
        bool esFeriado,
        string motivo,
        DateTimeOffset momento)
    {
        Id = id;
        DiaSemana = diaSemana;
        FechaEspecifica = fechaEspecifica;
        HoraInicio = horaInicio;
        HoraFin = horaFin;
        EsFeriado = esFeriado;
        Motivo = motivo;
        Activa = true;
        CreadoEn = momento;
    }

    /// <summary>Identificador interno de la franja.</summary>
    public Guid Id { get; private set; }

    /// <summary>Día de la semana recurrente; excluyente con <see cref="FechaEspecifica"/>.</summary>
    public DayOfWeek? DiaSemana { get; private set; }

    /// <summary>Fecha concreta de la franja; excluyente con <see cref="DiaSemana"/>.</summary>
    public DateOnly? FechaEspecifica { get; private set; }

    /// <summary>Hora de inicio de la no operación, inclusive.</summary>
    public TimeOnly HoraInicio { get; private set; }

    /// <summary>Hora de fin de la no operación, exclusiva.</summary>
    public TimeOnly HoraFin { get; private set; }

    /// <summary>Indica si la franja corresponde a un feriado institucional.</summary>
    public bool EsFeriado { get; private set; }

    /// <summary>Motivo visible de la no operación.</summary>
    public string Motivo { get; private set; } = string.Empty;

    /// <summary>Indica si la franja sigue vigente; la baja es lógica.</summary>
    public bool Activa { get; private set; }

    /// <summary>Momento del alta.</summary>
    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Token de concurrencia optimista gestionado por la base de datos.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Indica si la franja cubre el día completo.</summary>
    public bool EsDiaCompleto => HoraInicio == TimeOnly.MinValue && HoraFin >= FinDeDia;

    /// <summary>
    /// Crea una franja no operativa exigiendo exactamente un criterio de aplicación
    /// (día de la semana o fecha) y un horario coherente.
    /// </summary>
    public static FranjaNoOperativa Crear(
        DayOfWeek? diaSemana,
        DateOnly? fechaEspecifica,
        TimeOnly horaInicio,
        TimeOnly horaFin,
        bool esFeriado,
        string motivo,
        DateTimeOffset momento)
    {
        ValidarCriterio(diaSemana, fechaEspecifica);
        ValidarHorario(horaInicio, horaFin);

        return new FranjaNoOperativa(
            Guid.NewGuid(),
            diaSemana,
            fechaEspecifica,
            horaInicio,
            horaFin,
            esFeriado,
            NormalizarMotivo(motivo),
            momento);
    }

    /// <summary>Actualiza los criterios, el horario, la marca de feriado y el motivo.</summary>
    public void Actualizar(
        DayOfWeek? diaSemana,
        DateOnly? fechaEspecifica,
        TimeOnly horaInicio,
        TimeOnly horaFin,
        bool esFeriado,
        string motivo)
    {
        ValidarCriterio(diaSemana, fechaEspecifica);
        ValidarHorario(horaInicio, horaFin);

        DiaSemana = diaSemana;
        FechaEspecifica = fechaEspecifica;
        HoraInicio = horaInicio;
        HoraFin = horaFin;
        EsFeriado = esFeriado;
        Motivo = NormalizarMotivo(motivo);
    }

    /// <summary>Da de baja lógica la franja; deja de aplicarse a las reservas siguientes.</summary>
    public void Desactivar() => Activa = false;

    /// <summary>Indica si la franja aplica a la fecha indicada.</summary>
    public bool AplicaA(DateOnly fecha) =>
        Activa && (FechaEspecifica == fecha || (DiaSemana is { } dia && dia == fecha.DayOfWeek));

    /// <summary>Identifica una franja equivalente por criterio y horario.</summary>
    public bool EsEquivalenteA(DayOfWeek? diaSemana, DateOnly? fechaEspecifica, TimeOnly horaInicio, TimeOnly horaFin) =>
        DiaSemana == diaSemana
        && FechaEspecifica == fechaEspecifica
        && HoraInicio == horaInicio
        && HoraFin == horaFin;

    /// <summary>
    /// Expande la franja a un intervalo concreto para la fecha indicada, tratando el cierre
    /// de <c>00:00</c> a <c>23:59</c> como el día completo. Devuelve <see langword="null"/>
    /// cuando la franja no aplica a esa fecha.
    /// </summary>
    public FranjaHoraria? Expandir(DateOnly fecha, TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        if (!AplicaA(fecha))
        {
            return null;
        }

        var inicio = MotorDisponibilidad.InicioEnZona(fecha, HoraInicio, zona);

        var fin = EsDiaCompleto
            ? MotorDisponibilidad.InicioEnZona(fecha.AddDays(1), TimeOnly.MinValue, zona)
            : MotorDisponibilidad.InicioEnZona(fecha, HoraFin, zona);

        return FranjaHoraria.Desde(inicio, fin);
    }

    private static void ValidarCriterio(DayOfWeek? diaSemana, DateOnly? fechaEspecifica)
    {
        if (diaSemana is { } dia && !Enum.IsDefined(dia))
        {
            throw new DomainException(MensajesCalendario.FranjaDiaInvalido);
        }

        if (diaSemana is not null && fechaEspecifica is not null)
        {
            throw new DomainException(MensajesCalendario.FranjaCriteriosExcluyentes);
        }

        if (diaSemana is null && fechaEspecifica is null)
        {
            throw new DomainException(MensajesCalendario.FranjaCriterioRequerido);
        }
    }

    private static void ValidarHorario(TimeOnly horaInicio, TimeOnly horaFin)
    {
        if (horaFin <= horaInicio)
        {
            throw new DomainException(MensajesCalendario.FranjaHorasInvalidas);
        }
    }

    private static string NormalizarMotivo(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new DomainException(MensajesCalendario.FranjaMotivoRequerido);
        }

        var normalizado = motivo.Trim();

        if (normalizado.Length > LongitudMaximaMotivo)
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesCalendario.FranjaMotivoDemasiadoLargo));
        }

        return normalizado;
    }
}
