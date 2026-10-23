using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Calendario.Dtos;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;

namespace Backend.Application.Modules.Calendario;

/// <summary>Actualiza un periodo académico validando solapes y ocupación fuera de la nueva ventana.</summary>
public sealed class ActualizarPeriodoHandler
{
    private readonly IPeriodosAcademicosRepository _repositorio;
    private readonly IConsultaCalendario _consulta;
    private readonly OpcionesReservas _opciones;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos y el reloj.</summary>
    public ActualizarPeriodoHandler(
        IPeriodosAcademicosRepository repositorio,
        IConsultaCalendario consulta,
        OpcionesReservas opciones,
        IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Edita el periodo si la nueva ventana conserva toda la ocupación confirmada.</summary>
    public async Task<Resultado<PeriodoAcademicoDetalle>> HandleAsync(
        Guid periodoId,
        ComandoActualizarPeriodo comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var periodo = await _repositorio.ObtenerAsync(periodoId, cancellationToken);

        if (periodo is null)
        {
            return Resultado<PeriodoAcademicoDetalle>.Fallo(
                TipoFallo.NoEncontrado,
                MensajesCalendario.PeriodoNoEncontrado);
        }

        if (!string.IsNullOrWhiteSpace(comando.RowVersion)
            && !string.Equals(comando.RowVersion, Convert.ToBase64String(periodo.RowVersion), StringComparison.Ordinal))
        {
            return AyudanteCalendario.ConflictoConcurrencia<PeriodoAcademicoDetalle>();
        }

        try
        {
            periodo.Actualizar(comando.Nombre, comando.FechaInicio, comando.FechaFin, _reloj.Ahora);

            if (await _repositorio.ExisteSolapeAsync(
                    periodo.FechaInicio,
                    periodo.FechaFin,
                    periodo.Id,
                    cancellationToken))
            {
                return Resultado<PeriodoAcademicoDetalle>.Fallo(
                    TipoFallo.Conflicto,
                    MensajesCalendario.PeriodoSolapado);
            }

            var zona = _opciones.ObtenerZonaHoraria();
            var inicio = MotorDisponibilidad.InicioEnZona(periodo.FechaInicio, TimeOnly.MinValue, zona);
            var fin = MotorDisponibilidad.InicioEnZona(periodo.FechaFin.AddDays(1), TimeOnly.MinValue, zona);

            if (await _repositorio.ExisteOcupacionFueraDeAsync(inicio, fin, cancellationToken))
            {
                return Resultado<PeriodoAcademicoDetalle>.Fallo(
                    TipoFallo.Conflicto,
                    MensajesCalendario.PeriodoConOcupacion);
            }

            if (!await _repositorio.GuardarCambiosAsync(cancellationToken))
            {
                return AyudanteCalendario.ConflictoConcurrencia<PeriodoAcademicoDetalle>();
            }

            _consulta.Invalidar();

            return Resultado<PeriodoAcademicoDetalle>.Exito(MapeoCalendario.ADetalle(periodo));
        }
        catch (DomainException excepcion)
        {
            return Resultado<PeriodoAcademicoDetalle>.Fallo(
                TipoFallo.Validacion,
                excepcion.Message,
                MapeoCalendario.Errores(AyudanteCalendario.CampoDe(excepcion.Message), excepcion.Message));
        }
    }
}
