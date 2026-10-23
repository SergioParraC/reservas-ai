using System.Globalization;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.Application.Modules.Reservas;

/// <summary>
/// Alta autoconfirmada e inmediata de una reserva sobre un puesto de cómputo. La franja,
/// el estado del puesto y el cupo del titular se validan antes de persistir.
/// </summary>
public sealed class CrearReservaHandler
{
    private readonly IReservasRepository _repositorio;
    private readonly IConsultaReservas _consulta;
    private readonly OpcionesReservas _opciones;
    private readonly IRelojSistema _reloj;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos, el reloj y la identidad actual.</summary>
    public CrearReservaHandler(
        IReservasRepository repositorio,
        IConsultaReservas consulta,
        OpcionesReservas opciones,
        IRelojSistema reloj,
        IUsuarioActual usuario)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Registra la reserva confirmada si la franja y el cupo lo permiten.</summary>
    public async Task<Resultado<ReservaDetalle>> HandleAsync(
        ComandoCrearReserva comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (_usuario.UsuarioId is not { } titular)
        {
            return Resultado<ReservaDetalle>.Fallo(TipoFallo.NoAutorizado, MensajesReserva.SinSesion);
        }

        var puesto = await _consulta.ObtenerPuestoAsync(comando.PuestoId, cancellationToken);

        if (puesto is null)
        {
            return Resultado<ReservaDetalle>.Fallo(TipoFallo.NoEncontrado, MensajesReserva.PuestoNoEncontrado);
        }

        if (!puesto.AdmitePrestamoDirecto)
        {
            return Resultado<ReservaDetalle>.Fallo(TipoFallo.Conflicto, MensajesReserva.PuestoNoReservable);
        }

        var zona = _opciones.ObtenerZonaHoraria();
        var politica = _opciones.ObtenerPolitica();
        var ahora = _reloj.Ahora;

        try
        {
            var franja = FranjaHoraria.Crear(
                TimeZoneInfo.ConvertTime(comando.Inicio, zona),
                TimeZoneInfo.ConvertTime(comando.Fin, zona),
                politica,
                ahora);

            var reserva = Reserva.Confirmar(comando.PuestoId, titular, franja, ahora);

            var resultado = await _repositorio.GuardarSiDisponibleAsync(
                reserva,
                _opciones.MaxReservasActivasPorUsuario,
                cancellationToken);

            return resultado switch
            {
                ResultadoGuardadoReserva.Guardada => Resultado<ReservaDetalle>.Exito(
                    MapeoReservas.ADetalle(reserva, puesto)),
                ResultadoGuardadoReserva.FranjaOcupada => Resultado<ReservaDetalle>.Fallo(
                    TipoFallo.Conflicto,
                    MensajesReserva.FranjaNoDisponible),
                ResultadoGuardadoReserva.CupoAgotado => Resultado<ReservaDetalle>.Fallo(
                    TipoFallo.Conflicto,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        MensajesReserva.CupoAgotado,
                        _opciones.MaxReservasActivasPorUsuario)),
                _ => Resultado<ReservaDetalle>.Fallo(
                    TipoFallo.Conflicto,
                    "No fue posible registrar la reserva. Intente nuevamente.")
            };
        }
        catch (ConflictoException excepcion)
        {
            return Resultado<ReservaDetalle>.Fallo(TipoFallo.Conflicto, excepcion.Message);
        }
        catch (DomainException excepcion)
        {
            return Resultado<ReservaDetalle>.Fallo(
                TipoFallo.Validacion,
                excepcion.Message,
                MapeoReservas.Errores(MapeoReservas.CampoDe(excepcion.Message), excepcion.Message));
        }
    }
}

/// <summary>Consulta paginada de las reservas del titular autenticado.</summary>
public sealed class ConsultarMisReservasHandler
{
    private readonly IConsultaReservas _consulta;
    private readonly OpcionesReservas _opciones;
    private readonly IRelojSistema _reloj;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos, el reloj y la identidad actual.</summary>
    public ConsultarMisReservasHandler(
        IConsultaReservas consulta,
        OpcionesReservas opciones,
        IRelojSistema reloj,
        IUsuarioActual usuario)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Devuelve la agenda propia ordenada por inicio ascendente.</summary>
    public async Task<Resultado<PaginaResultado<ReservaResumen>>> HandleAsync(
        FiltroReservasPropias filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        if (_usuario.UsuarioId is not { } titular)
        {
            return Resultado<PaginaResultado<ReservaResumen>>.Fallo(
                TipoFallo.NoAutorizado,
                MensajesReserva.SinSesion);
        }

        if (filtro.Desde is { } desde && filtro.Hasta is { } hasta && desde > hasta)
        {
            return Resultado<PaginaResultado<ReservaResumen>>.Fallo(
                TipoFallo.Validacion,
                MensajesReserva.RangoInvalido,
                MapeoReservas.Errores("desde", MensajesReserva.RangoInvalido));
        }

        var normalizado = filtro with
        {
            Pagina = Paginacion.NormalizarPagina(filtro.Pagina),
            Tamano = Paginacion.NormalizarTamano(filtro.Tamano)
        };

        var pagina = await _consulta.ListarPropiasAsync(titular, normalizado, cancellationToken);
        var politica = _opciones.ObtenerPolitica();
        var ahora = _reloj.Ahora;

        var elementos = pagina.Elementos
            .Select(reserva => reserva with
            {
                Cancelable = reserva.Estado == EstadoReserva.Confirmada
                    && ahora <= reserva.Inicio - politica.AntelacionCancelacion
            })
            .ToArray();

        return Resultado<PaginaResultado<ReservaResumen>>.Exito(
            new PaginaResultado<ReservaResumen>(elementos, pagina.Total, pagina.Pagina, pagina.Tamano));
    }
}

/// <summary>Cancela una reserva propia dentro del margen permitido.</summary>
public sealed class CancelarReservaHandler
{
    private readonly IReservasRepository _repositorio;
    private readonly OpcionesReservas _opciones;
    private readonly IRelojSistema _reloj;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos, el reloj y la identidad actual.</summary>
    public CancelarReservaHandler(
        IReservasRepository repositorio,
        OpcionesReservas opciones,
        IRelojSistema reloj,
        IUsuarioActual usuario)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Cambia el estado a cancelado sin alterar el código QR del puesto.</summary>
    public async Task<Resultado> HandleAsync(Guid reservaId, CancellationToken cancellationToken = default)
    {
        if (_usuario.UsuarioId is not { } titular)
        {
            return Resultado.Fallo(TipoFallo.NoAutorizado, MensajesReserva.SinSesion);
        }

        var reserva = await _repositorio.ObtenerAsync(reservaId, cancellationToken);

        if (reserva is null)
        {
            return Resultado.Fallo(TipoFallo.NoEncontrado, MensajesReserva.ReservaNoEncontrada);
        }

        if (!reserva.PerteneceA(titular))
        {
            return Resultado.Fallo(TipoFallo.NoPermitido, MensajesReserva.ReservaAjena);
        }

        try
        {
            reserva.Cancelar(_reloj.Ahora, _opciones.ObtenerPolitica());
        }
        catch (ConflictoException excepcion)
        {
            return Resultado.Fallo(TipoFallo.Conflicto, excepcion.Message);
        }

        return await _repositorio.GuardarCambiosAsync(cancellationToken)
            ? Resultado.Exito()
            : Resultado.Fallo(TipoFallo.Conflicto, "No fue posible cancelar la reserva. Intente nuevamente.");
    }
}

