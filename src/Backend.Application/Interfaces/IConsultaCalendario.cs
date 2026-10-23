using Backend.Domain.Entities;
using Backend.Domain.Services;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de lectura del calendario institucional. Las implementaciones pueden cachear el
/// periodo activo y las franjas no operativas durante <c>Calendario:MinutosCacheCalendario</c>.
/// </summary>
public interface IConsultaCalendario
{
    /// <summary>Devuelve el periodo académico activo o <see langword="null"/> si no existe.</summary>
    Task<PeriodoAcademico?> ObtenerPeriodoActivoAsync(CancellationToken cancellationToken = default);

    /// <summary>Lista todos los periodos registrados, activos e inactivos.</summary>
    Task<IReadOnlyList<PeriodoAcademico>> ListarPeriodosAsync(CancellationToken cancellationToken = default);

    /// <summary>Lista todas las franjas no operativas vigentes.</summary>
    Task<IReadOnlyList<FranjaNoOperativa>> ListarFranjasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Construye el calendario institucional expandido para la ventana indicada.
    /// </summary>
    Task<CalendarioInstitucional> ObtenerCalendarioAsync(
        DateOnly desde,
        DateOnly hasta,
        TimeZoneInfo zona,
        CancellationToken cancellationToken = default);

    /// <summary>Purga la caché tras una mutación de periodos o franjas.</summary>
    void Invalidar();
}

/// <summary>
/// Puerto de escritura de periodos académicos: solapes, ocupación futura y activación
/// transaccional con un único periodo vigente.
/// </summary>
public interface IPeriodosAcademicosRepository
{
    /// <summary>Carga un periodo con su token de concurrencia o devuelve <see langword="null"/>.</summary>
    Task<PeriodoAcademico?> ObtenerAsync(Guid periodoId, CancellationToken cancellationToken = default);

    /// <summary>Indica si el rango se cruza con otro periodo registrado.</summary>
    Task<bool> ExisteSolapeAsync(
        DateOnly fechaInicio,
        DateOnly fechaFin,
        Guid? excluirPeriodoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si existen reservas, series o solicitudes activas fuera de la ventana indicada,
    /// de modo que acortar el periodo no invalide agendas confirmadas.
    /// </summary>
    Task<bool> ExisteOcupacionFueraDeAsync(
        DateTimeOffset inicio,
        DateTimeOffset finExclusivo,
        CancellationToken cancellationToken = default);

    /// <summary>Agrega el periodo al contexto de persistencia.</summary>
    Task AgregarAsync(PeriodoAcademico periodo, CancellationToken cancellationToken = default);

    /// <summary>Confirma los cambios pendientes; devuelve <see langword="false"/> ante conflicto.</summary>
    Task<bool> GuardarCambiosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Activa el periodo y desactiva al vigente en la misma transacción serializable.
    /// Devuelve <see langword="false"/> si el periodo no existe o hay conflicto de concurrencia.
    /// </summary>
    Task<bool> ActivarAsync(Guid periodoId, DateTimeOffset momento, CancellationToken cancellationToken = default);
}

/// <summary>Puerto de escritura de franjas no operativas globales.</summary>
public interface IFranjasNoOperativasRepository
{
    /// <summary>Carga una franja con su token de concurrencia o devuelve <see langword="null"/>.</summary>
    Task<FranjaNoOperativa?> ObtenerAsync(Guid franjaId, CancellationToken cancellationToken = default);

    /// <summary>Indica si ya existe una franja equivalente por criterio y horario.</summary>
    Task<bool> ExisteDuplicadaAsync(
        DayOfWeek? diaSemana,
        DateOnly? fechaEspecifica,
        TimeOnly horaInicio,
        TimeOnly horaFin,
        Guid? excluirFranjaId,
        CancellationToken cancellationToken = default);

    /// <summary>Agrega la franja al contexto de persistencia.</summary>
    Task AgregarAsync(FranjaNoOperativa franja, CancellationToken cancellationToken = default);

    /// <summary>Confirma los cambios pendientes; devuelve <see langword="false"/> ante conflicto.</summary>
    Task<bool> GuardarCambiosAsync(CancellationToken cancellationToken = default);
}
