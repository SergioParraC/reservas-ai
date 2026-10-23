using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Entities;

namespace Backend.Application.Interfaces;

/// <summary>Desenlace de un intento de persistir una reserva.</summary>
public enum ResultadoGuardadoReserva
{
    /// <summary>La reserva se persistió y la franja quedó bloqueada.</summary>
    Guardada = 0,

    /// <summary>Otra reserva vigente ocupa la franja solicitada.</summary>
    FranjaOcupada = 1,

    /// <summary>El titular ya alcanzó su cupo de reservas activas.</summary>
    CupoAgotado = 2,

    /// <summary>Conflicto de persistencia o de concurrencia optimista.</summary>
    Conflicto = 3
}

/// <summary>
/// Puerto de escritura de reservas: alta condicionada a disponibilidad y cupo dentro de una
/// transacción, más las transiciones de estado con concurrencia optimista.
/// </summary>
public interface IReservasRepository
{
    /// <summary>Carga una reserva con su token de concurrencia o devuelve <see langword="null"/>.</summary>
    Task<Reserva?> ObtenerAsync(Guid reservaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste la reserva solo si la franja sigue libre y el titular no superó el cupo.
    /// La verificación y la escritura ocurren en la misma transacción serializable.
    /// </summary>
    Task<ResultadoGuardadoReserva> GuardarSiDisponibleAsync(
        Reserva reserva,
        int maximoReservasActivas,
        CancellationToken cancellationToken = default);

    /// <summary>Confirma un cambio de estado; devuelve <see langword="false"/> ante conflicto.</summary>
    Task<bool> GuardarCambiosAsync(CancellationToken cancellationToken = default);
}
