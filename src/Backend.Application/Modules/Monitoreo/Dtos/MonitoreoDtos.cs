using Backend.Domain.Enums;

namespace Backend.Application.Modules.Monitoreo.Dtos;

/// <summary>Sesión abierta que ocupa un puesto en el instante consultado.</summary>
public sealed record SesionEnCurso(
    Guid ReservaId,
    string PuestoIdentificador,
    string Titular,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    DateTimeOffset? FechaCheckIn);

/// <summary>
/// Desglose de ocupación de un área en el instante consultado. <c>ProximaLiberacion</c> es el
/// instante más cercano en que una plaza contada como ocupada o pendiente quedará libre; es nulo
/// cuando no hay ocupación vigente ni reservas por ingresar.
/// </summary>
public sealed record AreaOcupacion(
    Guid EspacioId,
    string Nombre,
    int Aforo,
    int PuestosActivos,
    int EnUso,
    int PendientesCheckIn,
    int SesionesSinCerrar,
    int Libres,
    decimal PorcentajeOcupacion,
    DateTimeOffset? ProximaLiberacion,
    IReadOnlyList<SesionEnCurso> EnCurso);

/// <summary>Totales institucionales del mapa de ocupación.</summary>
public sealed record TotalesOcupacion(
    int Aforo,
    int PuestosActivos,
    int EnUso,
    int PendientesCheckIn,
    int SesionesSinCerrar,
    int Libres,
    decimal PorcentajeOcupacion);

/// <summary>Respuesta del mapa: instante de generación, tolerancia aplicada, totales y áreas.</summary>
public sealed record OcupacionActual(
    DateTimeOffset GeneradoEn,
    int ToleranciaMinutos,
    TotalesOcupacion Totales,
    IReadOnlyList<AreaOcupacion> Areas);

/// <summary>Área con su aforo, incluidas las que no tienen puestos activos.</summary>
public sealed record AreaOcupable(Guid EspacioId, string Nombre, int Aforo);

/// <summary>Puesto activo que puede estar ocupado.</summary>
public sealed record PuestoOcupable(Guid EspacioId, Guid PuestoId, string Identificador);

/// <summary>
/// Reserva relevante para la foto instantánea: en uso dentro de su franja, en uso ya vencida
/// (sesión sin cerrar) o confirmada dentro de su ventana de check-in.
/// </summary>
public sealed record ReservaOcupacionActual(
    Guid ReservaId,
    Guid EspacioId,
    Guid PuestoId,
    EstadoReserva Estado,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    DateTimeOffset? FechaCheckIn,
    string Titular);

/// <summary>Filas crudas que el caso de uso agrega por área sin volver a consultar la base.</summary>
public sealed record OcupacionInstantanea(
    IReadOnlyList<AreaOcupable> Areas,
    IReadOnlyList<PuestoOcupable> Puestos,
    IReadOnlyList<ReservaOcupacionActual> Reservas);
