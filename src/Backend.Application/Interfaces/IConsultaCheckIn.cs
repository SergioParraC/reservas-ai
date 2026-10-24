using Backend.Application.Modules.Asistencia.Dtos;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de lectura del check-in: devuelve la reserva con su titular, su estado y el código QR
/// estático del puesto reservado, de modo que la comparación ocurra siempre en el servidor.
/// </summary>
public interface IConsultaCheckIn
{
    /// <summary>Vista de la reserva o <see langword="null"/> si no existe.</summary>
    Task<ReservaParaCheckIn?> ObtenerAsync(Guid reservaId, CancellationToken cancellationToken = default);
}
