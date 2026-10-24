using Backend.Domain.Enums;

namespace Backend.Application.Modules.Reservas.Dtos;

/// <summary>Datos del puesto y su área necesarios para validar el préstamo directo.</summary>
public sealed record PuestoReservable(
    Guid PuestoId,
    string Identificador,
    EstadoUnidad EstadoPuesto,
    Guid EspacioId,
    string EspacioNombre,
    EstadoUnidad EstadoEspacio,
    TipoAprobacion Aprobacion,
    TipoModalidadPrestamo TipoModalidadPrestamo = TipoModalidadPrestamo.PuestoIndividual)
{
    /// <summary>Indica si el puesto admite reserva autoconfirmada inmediata.</summary>
    public bool AdmitePrestamoDirecto =>
        EstadoPuesto == EstadoUnidad.Activo
        && EstadoEspacio == EstadoUnidad.Activo
        && Aprobacion == TipoAprobacion.Automatica
        && TipoModalidadPrestamo != TipoModalidadPrestamo.AreaCompleta;

    /// <summary>Indica si la modalidad del área exige reservar el área completa.</summary>
    public bool ModalidadIncompatible => TipoModalidadPrestamo == TipoModalidadPrestamo.AreaCompleta;
}

/// <summary>Intervalo ocupado por una reserva vigente.</summary>
public sealed record IntervaloOcupado(DateTimeOffset Inicio, DateTimeOffset Fin);

/// <summary>Franja de la jornada con su disponibilidad y el motivo cuando está bloqueada.</summary>
public sealed record FranjaDisponible(
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    bool Disponible,
    string? Motivo);

/// <summary>Disponibilidad de un puesto para una fecha y jornada determinadas.</summary>
public sealed record DisponibilidadPuesto(
    Guid PuestoId,
    string Identificador,
    Guid EspacioId,
    string EspacioNombre,
    string ZonaHoraria,
    IReadOnlyList<FranjaDisponible> Franjas);

/// <summary>Disponibilidad de un área completa para una fecha y jornada determinadas.</summary>
public sealed record DisponibilidadEspacio(
    Guid EspacioId,
    string EspacioNombre,
    string ZonaHoraria,
    IReadOnlyList<FranjaDisponible> Franjas);

/// <summary>Detalle de una reserva confirmada o cancelada.</summary>
public sealed record ReservaDetalle(
    Guid Id,
    Guid PuestoId,
    string PuestoIdentificador,
    Guid EspacioId,
    string EspacioNombre,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    DateTimeOffset FinPlanificado,
    EstadoReserva Estado,
    DateTimeOffset CreadaEn,
    DateTimeOffset? CanceladaEn,
    DateTimeOffset? FechaCheckIn,
    DateTimeOffset? FechaCheckOut,
    string RowVersion);

/// <summary>Resumen de una reserva del titular para el listado paginado.</summary>
public sealed record ReservaResumen(
    Guid Id,
    Guid PuestoId,
    string PuestoIdentificador,
    string EspacioNombre,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    DateTimeOffset FinPlanificado,
    EstadoReserva Estado,
    DateTimeOffset CreadaEn,
    DateTimeOffset? CanceladaEn,
    DateTimeOffset? FechaCheckIn,
    DateTimeOffset? FechaCheckOut,
    bool Cancelable);

/// <summary>Datos de entrada para reservar una franja de un puesto.</summary>
public sealed record ComandoCrearReserva(Guid PuestoId, DateTimeOffset Inicio, DateTimeOffset Fin);

/// <summary>Filtros de la consulta de disponibilidad.</summary>
public sealed record FiltroDisponibilidad(
    DateOnly Fecha,
    TimeOnly? Desde,
    TimeOnly? Hasta,
    int? DuracionMinutos);

/// <summary>Filtros de la agenda propia del titular.</summary>
public sealed record FiltroReservasPropias(
    DateTimeOffset? Desde,
    DateTimeOffset? Hasta,
    EstadoReserva? Estado,
    int Pagina,
    int Tamano);
