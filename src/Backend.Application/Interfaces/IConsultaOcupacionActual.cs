using Backend.Application.Modules.Monitoreo.Dtos;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de lectura del mapa de ocupación: devuelve, para el instante y la tolerancia indicados,
/// las áreas con su aforo, sus puestos activos y las reservas relevantes (en uso, sesión sin
/// cerrar o pendiente de check-in) para que la agregación viva en la capa de aplicación.
/// </summary>
public interface IConsultaOcupacionActual
{
    /// <summary>Foto instantánea de la ocupación del campus.</summary>
    Task<OcupacionInstantanea> ObtenerAsync(
        DateTimeOffset ahora,
        TimeSpan tolerancia,
        CancellationToken cancellationToken = default);
}
