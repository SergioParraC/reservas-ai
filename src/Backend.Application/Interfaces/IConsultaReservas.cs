using Backend.Application.Common;
using Backend.Application.Modules.Reservas.Dtos;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de lectura de reservas: puesto reservable, ocupación de un puesto y agenda del titular,
/// siempre con proyecciones explícitas sin cargar agregados completos.
/// </summary>
public interface IConsultaReservas
{
    /// <summary>Obtiene los datos del puesto y su área necesarios para validar una reserva.</summary>
    Task<PuestoReservable?> ObtenerPuestoAsync(Guid puestoId, CancellationToken cancellationToken = default);

    /// <summary>Franjas ocupadas por reservas vigentes del puesto dentro del rango indicado.</summary>
    Task<IReadOnlyList<IntervaloOcupado>> ObtenerOcupadasAsync(
        Guid puestoId,
        DateTimeOffset desde,
        DateTimeOffset hasta,
        CancellationToken cancellationToken = default);

    /// <summary>Lista paginada de las reservas del titular, ordenada por inicio ascendente.</summary>
    Task<PaginaResultado<ReservaResumen>> ListarPropiasAsync(
        Guid usuarioId,
        FiltroReservasPropias filtro,
        CancellationToken cancellationToken = default);
}
