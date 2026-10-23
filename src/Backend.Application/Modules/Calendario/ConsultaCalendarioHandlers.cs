using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Calendario.Dtos;

namespace Backend.Application.Modules.Calendario;

/// <summary>Lista los periodos académicos registrados, activos e inactivos.</summary>
public sealed class ListarPeriodosHandler
{
    private readonly IConsultaCalendario _consulta;

    /// <summary>Inicializa el caso de uso con el puerto de lectura del calendario.</summary>
    public ListarPeriodosHandler(IConsultaCalendario consulta)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    /// <summary>Devuelve los periodos ordenados de la fecha de inicio más reciente a la más antigua.</summary>
    public async Task<Resultado<IReadOnlyList<PeriodoAcademicoDetalle>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var periodos = await _consulta.ListarPeriodosAsync(cancellationToken);

        return Resultado<IReadOnlyList<PeriodoAcademicoDetalle>>.Exito(
            periodos
                .OrderByDescending(periodo => periodo.FechaInicio)
                .Select(MapeoCalendario.ADetalle)
                .ToArray());
    }
}

/// <summary>Consulta el calendario vigente para que la SPA anticipe las restricciones.</summary>
public sealed class ConsultarCalendarioVigenteHandler
{
    private readonly IConsultaCalendario _consulta;

    /// <summary>Inicializa el caso de uso con el puerto de lectura del calendario.</summary>
    public ConsultarCalendarioVigenteHandler(IConsultaCalendario consulta)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    /// <summary>Devuelve el periodo activo y las franjas no operativas ordenadas.</summary>
    public async Task<Resultado<CalendarioVigenteDetalle>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var periodo = await _consulta.ObtenerPeriodoActivoAsync(cancellationToken);
        var franjas = await _consulta.ListarFranjasAsync(cancellationToken);

        return Resultado<CalendarioVigenteDetalle>.Exito(new CalendarioVigenteDetalle(
            periodo is not null,
            periodo is null ? null : MapeoCalendario.ADetalle(periodo),
            franjas
                .OrderBy(franja => franja.DiaSemana)
                .ThenBy(franja => franja.FechaEspecifica)
                .ThenBy(franja => franja.HoraInicio)
                .Select(MapeoCalendario.ADetalle)
                .ToArray()));
    }
}

/// <summary>Lista las franjas no operativas vigentes del calendario global.</summary>
public sealed class ListarFranjasHandler
{
    private readonly IConsultaCalendario _consulta;

    /// <summary>Inicializa el caso de uso con el puerto de lectura del calendario.</summary>
    public ListarFranjasHandler(IConsultaCalendario consulta)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    /// <summary>Devuelve las franjas activas ordenadas por día y hora.</summary>
    public async Task<Resultado<IReadOnlyList<FranjaNoOperativaDetalle>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var franjas = await _consulta.ListarFranjasAsync(cancellationToken);

        return Resultado<IReadOnlyList<FranjaNoOperativaDetalle>>.Exito(
            franjas
                .OrderBy(franja => franja.DiaSemana)
                .ThenBy(franja => franja.FechaEspecifica)
                .ThenBy(franja => franja.HoraInicio)
                .Select(MapeoCalendario.ADetalle)
                .ToArray());
    }
}
