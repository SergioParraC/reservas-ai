using Backend.Domain.Entities;

namespace Backend.Application.Modules.Calendario.Dtos;

/// <summary>Periodo académico expuesto en las respuestas administrativas.</summary>
public sealed record PeriodoAcademicoDetalle(
    Guid Id,
    string Nombre,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    bool EstadoActivo,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn,
    string RowVersion);

/// <summary>Franja no operativa expuesta en el calendario y en la administración.</summary>
public sealed record FranjaNoOperativaDetalle(
    Guid Id,
    DayOfWeek? DiaSemana,
    DateOnly? FechaEspecifica,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    bool EsFeriado,
    string Motivo,
    bool Activa,
    DateTimeOffset CreadoEn,
    string RowVersion);

/// <summary>Calendario vigente para que la SPA anticipe las restricciones antes de enviar.</summary>
public sealed record CalendarioVigenteDetalle(
    bool TienePeriodoActivo,
    PeriodoAcademicoDetalle? PeriodoActivo,
    IReadOnlyList<FranjaNoOperativaDetalle> Franjas);

/// <summary>Datos de entrada para registrar un periodo académico.</summary>
public sealed record ComandoCrearPeriodo(string Nombre, DateOnly FechaInicio, DateOnly FechaFin);

/// <summary>Datos de entrada para editar un periodo académico.</summary>
public sealed record ComandoActualizarPeriodo(
    string Nombre,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? RowVersion);

/// <summary>Datos de entrada para registrar una franja no operativa.</summary>
public sealed record ComandoCrearFranjaNoOperativa(
    DayOfWeek? DiaSemana,
    DateOnly? FechaEspecifica,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    bool EsFeriado,
    string Motivo);

/// <summary>Datos de entrada para editar una franja no operativa.</summary>
public sealed record ComandoActualizarFranjaNoOperativa(
    DayOfWeek? DiaSemana,
    DateOnly? FechaEspecifica,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    bool EsFeriado,
    string Motivo,
    string? RowVersion);

/// <summary>Mapeos y clasificación de mensajes del módulo de calendario.</summary>
public static class MapeoCalendario
{
    /// <summary>Proyecta un periodo académico a su contrato.</summary>
    public static PeriodoAcademicoDetalle ADetalle(PeriodoAcademico periodo)
    {
        ArgumentNullException.ThrowIfNull(periodo);

        return new PeriodoAcademicoDetalle(
            periodo.Id,
            periodo.Nombre,
            periodo.FechaInicio,
            periodo.FechaFin,
            periodo.EstadoActivo,
            periodo.CreadoEn,
            periodo.ActualizadoEn,
            Convert.ToBase64String(periodo.RowVersion));
    }

    /// <summary>Proyecta una franja no operativa a su contrato.</summary>
    public static FranjaNoOperativaDetalle ADetalle(FranjaNoOperativa franja)
    {
        ArgumentNullException.ThrowIfNull(franja);

        return new FranjaNoOperativaDetalle(
            franja.Id,
            franja.DiaSemana,
            franja.FechaEspecifica,
            franja.HoraInicio,
            franja.HoraFin,
            franja.EsFeriado,
            franja.Motivo,
            franja.Activa,
            franja.CreadoEn,
            Convert.ToBase64String(franja.RowVersion));
    }

    /// <summary>Construye el diccionario de errores por campo del contrato RFC 7807.</summary>
    public static IDictionary<string, string[]> Errores(string campo, string mensaje) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal) { [campo] = [mensaje] };
}
