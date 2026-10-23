using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Calendario.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Calendario;

/// <summary>Clasificación de mensajes y utilidades compartidas de los casos de uso del calendario.</summary>
internal static class AyudanteCalendario
{
    /// <summary>Determina el campo del cuerpo al que corresponde un mensaje de dominio.</summary>
    public static string CampoDe(string mensaje) => mensaje switch
    {
        MensajesCalendario.PeriodoNombreRequerido or MensajesCalendario.PeriodoNombreDemasiadoLargo => "nombre",
        MensajesCalendario.PeriodoFechasInvertidas => "fechaFin",
        MensajesCalendario.FranjaDiaInvalido or MensajesCalendario.FranjaCriteriosExcluyentes
            or MensajesCalendario.FranjaCriterioRequerido => "diaSemana",
        MensajesCalendario.FranjaHorasInvalidas => "horaFin",
        MensajesCalendario.FranjaMotivoRequerido or MensajesCalendario.FranjaMotivoDemasiadoLargo => "motivo",
        _ => "periodo"
    };

    /// <summary>Fallo estándar de concurrencia del calendario.</summary>
    public static Resultado<T> ConflictoConcurrencia<T>() => Resultado<T>.Fallo(
        TipoFallo.Conflicto,
        "El calendario fue modificado por otra sesión. Recargue los datos e intente nuevamente.");
}

/// <summary>
/// Alta de periodos académicos: se registran inactivos y se rechazan los rangos solapados.
/// </summary>
public sealed class CrearPeriodoHandler
{
    private readonly IPeriodosAcademicosRepository _repositorio;
    private readonly IConsultaCalendario _consulta;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos y el reloj.</summary>
    public CrearPeriodoHandler(
        IPeriodosAcademicosRepository repositorio,
        IConsultaCalendario consulta,
        IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Registra el periodo inactivo si no se cruza con otro registrado.</summary>
    public async Task<Resultado<PeriodoAcademicoDetalle>> HandleAsync(
        ComandoCrearPeriodo comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        try
        {
            var periodo = PeriodoAcademico.Crear(
                comando.Nombre,
                comando.FechaInicio,
                comando.FechaFin,
                _reloj.Ahora);

            if (await _repositorio.ExisteSolapeAsync(
                    periodo.FechaInicio,
                    periodo.FechaFin,
                    null,
                    cancellationToken))
            {
                return Resultado<PeriodoAcademicoDetalle>.Fallo(
                    TipoFallo.Conflicto,
                    MensajesCalendario.PeriodoSolapado);
            }

            await _repositorio.AgregarAsync(periodo, cancellationToken);

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

/// <summary>Activa un periodo y desactiva al vigente en la misma transacción.</summary>
public sealed class ActivarPeriodoHandler
{
    private readonly IPeriodosAcademicosRepository _repositorio;
    private readonly IConsultaCalendario _consulta;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos y el reloj.</summary>
    public ActivarPeriodoHandler(
        IPeriodosAcademicosRepository repositorio,
        IConsultaCalendario consulta,
        IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Marca el periodo como vigente y purga la caché del calendario.</summary>
    public async Task<Resultado<PeriodoAcademicoDetalle>> HandleAsync(
        Guid periodoId,
        CancellationToken cancellationToken = default)
    {
        var activado = await _repositorio.ActivarAsync(periodoId, _reloj.Ahora, cancellationToken);
        var periodo = await _repositorio.ObtenerAsync(periodoId, cancellationToken);

        if (periodo is null)
        {
            return Resultado<PeriodoAcademicoDetalle>.Fallo(
                TipoFallo.NoEncontrado,
                MensajesCalendario.PeriodoNoEncontrado);
        }

        if (!activado)
        {
            return AyudanteCalendario.ConflictoConcurrencia<PeriodoAcademicoDetalle>();
        }

        _consulta.Invalidar();

        return Resultado<PeriodoAcademicoDetalle>.Exito(MapeoCalendario.ADetalle(periodo));
    }
}
