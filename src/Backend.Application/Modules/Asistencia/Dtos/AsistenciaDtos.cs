using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Application.Modules.Reservas.Dtos;

namespace Backend.Application.Modules.Asistencia.Dtos;

/// <summary>
/// Vista de una reserva con el titular, el estado vigente y el código QR del puesto reservado.
/// Es la lectura que autoriza el check-in: el QR se compara en el servidor y nunca se devuelve.
/// </summary>
public sealed record ReservaParaCheckIn(
    Guid ReservaId,
    Guid PuestoId,
    string PuestoIdentificador,
    Guid EspacioId,
    string EspacioNombre,
    Guid TitularId,
    string Titular,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    DateTimeOffset FinPlanificado,
    EstadoReserva Estado,
    DateTimeOffset? FechaCheckIn,
    DateTimeOffset? FechaCheckOut,
    string CodigoQr)
{
    /// <summary>Indica si la reserva pertenece al usuario indicado.</summary>
    public bool PerteneceA(Guid usuarioId) => TitularId == usuarioId;
}

/// <summary>Entrada del check-in: la reserva y el código leído de la placa del puesto.</summary>
public sealed record ComandoRegistrarCheckIn(Guid ReservaId, string CodigoQr);

/// <summary>Resultado del barrido de inasistencias: reservas liberadas y sus identificadores.</summary>
public sealed record ResultadoLiberacion(int Liberadas, IReadOnlyList<Guid> Reservas);

/// <summary>Mapeos del módulo de asistencia.</summary>
public static class MapeoAsistencia
{
    /// <summary>Proyecta la reserva mutada y su vista de lectura al contrato de detalle.</summary>
    public static ReservaDetalle ADetalle(Reserva reserva, ReservaParaCheckIn vista)
    {
        ArgumentNullException.ThrowIfNull(reserva);
        ArgumentNullException.ThrowIfNull(vista);

        return new ReservaDetalle(
            reserva.Id,
            reserva.PuestoId,
            vista.PuestoIdentificador,
            vista.EspacioId,
            vista.EspacioNombre,
            reserva.Inicio,
            reserva.Fin,
            reserva.FinPlanificado,
            reserva.Estado,
            reserva.CreadaEn,
            reserva.CanceladaEn,
            reserva.FechaCheckIn,
            reserva.FechaCheckOut,
            Convert.ToBase64String(reserva.RowVersion));
    }
}
