using Backend.Application.Common;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;

namespace Backend.Application.Interfaces;

/// <summary>Desenlace de un intento de persistir una serie completa.</summary>
public enum ResultadoGuardadoSerie
{
    /// <summary>La serie y todas sus ocurrencias se persistieron.</summary>
    Guardada = 0,

    /// <summary>Al menos una ocurrencia choca con la agenda vigente del área.</summary>
    Colision = 1,

    /// <summary>Conflicto de persistencia o de concurrencia optimista.</summary>
    Conflicto = 2
}

/// <summary>Resultado explícito del alta de una serie, con las colisiones detectadas al confirmar.</summary>
public sealed record ResultadoPersistenciaSerie(
    ResultadoGuardadoSerie Estado,
    IReadOnlyList<ColisionFranja> Colisiones);

/// <summary>
/// Puerto de escritura y lectura de series semestrales. El alta verifica la agenda y escribe
/// la cabecera y todas las ocurrencias dentro de una única transacción serializable.
/// </summary>
public interface IReservasRecurrentesRepository
{
    /// <summary>
    /// Persiste la serie y sus ocurrencias solo si la agenda del área sigue libre.
    /// </summary>
    Task<ResultadoPersistenciaSerie> GuardarSerieAsync(
        ReservaRecurrente serie,
        IReadOnlyList<FranjaHoraria> ocurrencias,
        CancellationToken cancellationToken = default);

    /// <summary>Carga una serie con sus ocurrencias para las pruebas y la consulta interna.</summary>
    Task<ReservaRecurrente?> ObtenerAsync(Guid serieId, CancellationToken cancellationToken = default);

    /// <summary>Lista las series del docente, de la más reciente a la más antigua.</summary>
    Task<PaginaResultado<SerieResumen>> ListarPropiasAsync(
        Guid docenteId,
        int pagina,
        int tamano,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Puerto de lectura de la agenda única del área: ocurrencias de series, solicitudes aprobadas
/// y reservas puntuales de sus puestos.
/// </summary>
public interface IConsultaOcupacionEspacio
{
    /// <summary>Intervalos activos del área que se cruzan con la ventana indicada.</summary>
    Task<IReadOnlyList<IntervaloOcupacion>> ObtenerAsync(
        Guid espacioId,
        DateTimeOffset desde,
        DateTimeOffset hasta,
        CancellationToken cancellationToken = default);

    /// <summary>Indica si el área tiene ocupación activa a partir del instante indicado.</summary>
    Task<bool> HayOcupacionFuturaAsync(
        Guid espacioId,
        DateTimeOffset desde,
        CancellationToken cancellationToken = default);
}
