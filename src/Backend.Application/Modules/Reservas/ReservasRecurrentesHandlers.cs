using System.Globalization;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;

namespace Backend.Application.Modules.Reservas;

/// <summary>
/// Alta de una serie semestral de área completa. El calendario académico se evalúa antes que la
/// agenda; la serie se confirma como un todo y se rechaza íntegra si una ocurrencia colisiona.
/// </summary>
public sealed class CrearSerieHandler
{
    private readonly IConsultaEspacios _espacios;
    private readonly IConsultaCalendario _calendario;
    private readonly IConsultaOcupacionEspacio _ocupacion;
    private readonly IReservasRecurrentesRepository _repositorio;
    private readonly OpcionesSemestre _semestre;
    private readonly OpcionesReservas _reservas;
    private readonly IRelojSistema _reloj;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos, la política y la identidad actual.</summary>
    public CrearSerieHandler(
        IConsultaEspacios espacios,
        IConsultaCalendario calendario,
        IConsultaOcupacionEspacio ocupacion,
        IReservasRecurrentesRepository repositorio,
        OpcionesSemestre semestre,
        OpcionesReservas reservas,
        IRelojSistema reloj,
        IUsuarioActual usuario)
    {
        _espacios = espacios ?? throw new ArgumentNullException(nameof(espacios));
        _calendario = calendario ?? throw new ArgumentNullException(nameof(calendario));
        _ocupacion = ocupacion ?? throw new ArgumentNullException(nameof(ocupacion));
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _semestre = semestre ?? throw new ArgumentNullException(nameof(semestre));
        _reservas = reservas ?? throw new ArgumentNullException(nameof(reservas));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Radica la serie completa y devuelve sus ocurrencias confirmadas.</summary>
    public async Task<Resultado<SerieDetalle>> HandleAsync(
        ComandoCrearSerie comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (_usuario.UsuarioId is not { } docente)
        {
            return Resultado<SerieDetalle>.Fallo(TipoFallo.NoAutorizado, MensajesReserva.SinSesion);
        }

        var espacio = await _espacios.ObtenerParaReservaAsync(comando.EspacioId, cancellationToken);

        if (espacio is null)
        {
            return Resultado<SerieDetalle>.Fallo(TipoFallo.NoEncontrado, MensajesSemestre.EspacioNoEncontrado);
        }

        if (espacio.Estado != EstadoUnidad.Activo)
        {
            return Resultado<SerieDetalle>.Fallo(TipoFallo.Conflicto, MensajesSemestre.EspacioNoDisponible);
        }

        if (espacio.TipoModalidadPrestamo != TipoModalidadPrestamo.AreaCompleta)
        {
            return Resultado<SerieDetalle>.Fallo(TipoFallo.Conflicto, MensajesSemestre.ModalidadIncompatible);
        }

        try
        {
            var periodo = PeriodoSemestral.Crear(comando.PeriodoInicio, comando.PeriodoFin);

            // El tope de semanas se valida sin leer el calendario ni la agenda.
            ReservaRecurrente.ValidarSemanas(periodo, _semestre.SemanasMaximasPorSerie);

            var patron = PatronRecurrencia.Crear(
                comando.TipoRecurrencia,
                comando.DiaSemana,
                comando.FechasFijas,
                comando.HoraInicio,
                comando.HoraFin);

            var ahora = _reloj.Ahora;
            var zona = _reservas.ObtenerZonaHoraria();

            var serie = ReservaRecurrente.Crear(
                espacio.Id,
                docente,
                patron,
                periodo,
                comando.Materia,
                ahora);

            var calendario = await _calendario.ObtenerCalendarioAsync(
                periodo.Inicio,
                periodo.Fin,
                zona,
                cancellationToken);

            var generadas = serie.GenerarOcurrencias(
                calendario,
                zona,
                _semestre.SemanasMaximasPorSerie,
                ahora,
                _semestre.ObtenerAntelacion());

            var ocupadas = await _ocupacion.ObtenerAsync(
                espacio.Id,
                generadas.Ocurrencias[0].Inicio,
                generadas.Ocurrencias[^1].Fin,
                cancellationToken);

            var colisiones = MotorDisponibilidad.DetectarColisiones(
                generadas.Ocurrencias,
                ocupadas.Select(intervalo => intervalo.Franja).ToArray());

            if (colisiones.Count > 0)
            {
                return ConflictoColision(colisiones.Count);
            }

            var persistencia = await _repositorio.GuardarSerieAsync(
                serie,
                generadas.Ocurrencias,
                cancellationToken);

            return persistencia.Estado switch
            {
                ResultadoGuardadoSerie.Guardada => Resultado<SerieDetalle>.Exito(
                    MapeoSeries.ADetalle(serie, espacio.Nombre, generadas)),
                ResultadoGuardadoSerie.Colision => ConflictoColision(persistencia.Colisiones.Count),
                _ => Resultado<SerieDetalle>.Fallo(
                    TipoFallo.Conflicto,
                    "No fue posible confirmar la serie. Intente nuevamente.")
            };
        }
        catch (ConflictoException excepcion)
        {
            return Resultado<SerieDetalle>.Fallo(TipoFallo.Conflicto, excepcion.Message);
        }
        catch (DomainException excepcion)
        {
            return Resultado<SerieDetalle>.Fallo(
                TipoFallo.Validacion,
                excepcion.Message,
                MapeoSeries.Errores(MapeoSeries.CampoDe(excepcion.Message), excepcion.Message));
        }
    }

    private static Resultado<SerieDetalle> ConflictoColision(int colisiones) =>
        Resultado<SerieDetalle>.Fallo(
            TipoFallo.Conflicto,
            string.Format(CultureInfo.InvariantCulture, MensajesSemestre.ColisionSerie, colisiones));
}

/// <summary>Consulta las series propias del docente autenticado.</summary>
public sealed class ConsultarSeriesHandler
{
    private readonly IReservasRecurrentesRepository _repositorio;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos y la identidad actual.</summary>
    public ConsultarSeriesHandler(IReservasRecurrentesRepository repositorio, IUsuarioActual usuario)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Devuelve la página de series del docente en orden descendente por registro.</summary>
    public async Task<Resultado<PaginaResultado<SerieResumen>>> HandleAsync(
        int pagina,
        int tamano,
        CancellationToken cancellationToken = default)
    {
        if (_usuario.UsuarioId is not { } docente)
        {
            return Resultado<PaginaResultado<SerieResumen>>.Fallo(
                TipoFallo.NoAutorizado,
                MensajesReserva.SinSesion);
        }

        var resultado = await _repositorio.ListarPropiasAsync(
            docente,
            Paginacion.NormalizarPagina(pagina),
            Paginacion.NormalizarTamano(tamano),
            cancellationToken);

        return Resultado<PaginaResultado<SerieResumen>>.Exito(resultado);
    }
}