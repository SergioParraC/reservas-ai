using Backend.Domain.Enums;
using Backend.Domain.ValueObjects;

namespace Backend.Application.Modules.Reservas.Dtos;

/// <summary>Intervalo ocupado de la agenda del área con su procedencia.</summary>
public sealed record IntervaloOcupacion(DateTimeOffset Inicio, DateTimeOffset Fin)
{
    /// <summary>Franja del intervalo con semántica semiabierta.</summary>
    public FranjaHoraria Franja => FranjaHoraria.Desde(Inicio, Fin);
}

/// <summary>Ocurrencia de una serie: la generada o la omitida por el calendario.</summary>
public sealed record OcurrenciaSerie(DateTimeOffset Inicio, DateTimeOffset Fin, bool Omitida, string? Motivo);

/// <summary>Detalle de una serie recién creada con sus ocurrencias.</summary>
public sealed record SerieDetalle(
    Guid Id,
    Guid EspacioId,
    string EspacioNombre,
    string Materia,
    TipoRecurrencia TipoRecurrencia,
    DayOfWeek? DiaSemana,
    IReadOnlyList<DateOnly> FechasFijas,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    DateTimeOffset CreadaEn,
    int TotalOcurrencias,
    IReadOnlyList<OcurrenciaSerie> Ocurrencias);

/// <summary>Resumen de una serie para el listado del docente.</summary>
public sealed record SerieResumen(
    Guid Id,
    Guid EspacioId,
    string EspacioNombre,
    string Materia,
    TipoRecurrencia TipoRecurrencia,
    DayOfWeek? DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    int TotalOcurrencias,
    DateTimeOffset CreadaEn);

/// <summary>Datos de entrada para radicar una serie semestral.</summary>
public sealed record ComandoCrearSerie(
    Guid EspacioId,
    TipoRecurrencia TipoRecurrencia,
    DayOfWeek? DiaSemana,
    IReadOnlyList<DateOnly>? FechasFijas,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    string Materia);
