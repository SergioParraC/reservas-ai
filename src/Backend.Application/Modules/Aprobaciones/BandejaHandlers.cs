using System.Globalization;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Solicitudes;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Aprobaciones;

/// <summary>
/// Bandeja única de aprobaciones filtrada por la competencia del revisor: el docente solo ve
/// áreas con aprobación docente y el administrativo ve todas.
/// </summary>
public sealed class ConsultarBandejaHandler
{
    private readonly IConsultaSolicitudes _consulta;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos y la identidad actual.</summary>
    public ConsultarBandejaHandler(IConsultaSolicitudes consulta, IUsuarioActual usuario)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Devuelve la página de solicitudes que el revisor puede resolver.</summary>
    public async Task<Resultado<PaginaResultado<SolicitudBandejaItem>>> HandleAsync(
        FiltroBandeja filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        if (_usuario.UsuarioId is null)
        {
            return Resultado<PaginaResultado<SolicitudBandejaItem>>.Fallo(
                TipoFallo.NoAutorizado,
                MensajesReserva.SinSesion);
        }

        var competencias = Competencias(_usuario.Roles);

        if (competencias.Count == 0)
        {
            return Resultado<PaginaResultado<SolicitudBandejaItem>>.Fallo(
                TipoFallo.NoPermitido,
                MensajesSolicitud.SolicitudAjena);
        }

        var normalizado = filtro with
        {
            Estado = filtro.Estado is { } estado && Enum.IsDefined(estado) ? estado : null,
            Pagina = Paginacion.NormalizarPagina(filtro.Pagina),
            Tamano = Paginacion.NormalizarTamano(filtro.Tamano)
        };

        var resultado = await _consulta.ListarBandejaAsync(normalizado, competencias, cancellationToken);

        return Resultado<PaginaResultado<SolicitudBandejaItem>>.Exito(resultado);
    }

    /// <summary>Competencias de aprobación que corresponden a los roles del portador.</summary>
    public static IReadOnlyCollection<TipoAprobacion> Competencias(IReadOnlyCollection<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        if (roles.Any(rol => string.Equals(rol, RolesAplicacion.Administrativo, StringComparison.OrdinalIgnoreCase)))
        {
            return [TipoAprobacion.Automatica, TipoAprobacion.Docente, TipoAprobacion.Administrativo];
        }

        return roles.Any(rol => string.Equals(rol, RolesAplicacion.Docente, StringComparison.OrdinalIgnoreCase))
            ? [TipoAprobacion.Docente]
            : [];
    }
}

/// <summary>
/// Cambio de estado de una solicitud con revalidación del calendario al aprobar, ocupación del
/// área y notificación encolada en la misma transacción de la decisión.
/// </summary>
public sealed class CambiarEstadoSolicitudHandler
{
    private readonly IConsultaSolicitudes _consulta;
    private readonly ISolicitudesRepository _repositorio;
    private readonly IConsultaCalendario _calendario;
    private readonly OpcionesSolicitudes _opciones;
    private readonly OpcionesReservas _reservas;
    private readonly IRelojSistema _reloj;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos, la política y la identidad actual.</summary>
    public CambiarEstadoSolicitudHandler(
        IConsultaSolicitudes consulta,
        ISolicitudesRepository repositorio,
        IConsultaCalendario calendario,
        OpcionesSolicitudes opciones,
        OpcionesReservas reservas,
        IRelojSistema reloj,
        IUsuarioActual usuario)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _calendario = calendario ?? throw new ArgumentNullException(nameof(calendario));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reservas = reservas ?? throw new ArgumentNullException(nameof(reservas));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Aplica la transición solicitada y confirma decisión, ocupación y correo juntos.</summary>
    public async Task<Resultado<SolicitudDetalle>> HandleAsync(
        ComandoCambiarEstado comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (_usuario.UsuarioId is not { } revisor)
        {
            return Resultado<SolicitudDetalle>.Fallo(TipoFallo.NoAutorizado, MensajesReserva.SinSesion);
        }

        if (comando.Estado is not (EstadoSolicitud.EnRevision or EstadoSolicitud.Aprobado or EstadoSolicitud.Rechazado))
        {
            return Resultado<SolicitudDetalle>.Fallo(
                TipoFallo.Validacion,
                MensajesSolicitud.TransicionInvalida,
                MapeoSolicitudes.Errores("estado", MensajesSolicitud.TransicionInvalida));
        }

        var detalle = await _consulta.ObtenerAsync(comando.SolicitudId, cancellationToken);

        if (detalle is null)
        {
            return Resultado<SolicitudDetalle>.Fallo(TipoFallo.NoEncontrado, MensajesSolicitud.SolicitudNoEncontrada);
        }

        if (detalle.SolicitanteId == revisor
            || !ConsultarBandejaHandler.Competencias(_usuario.Roles).Contains(detalle.TipoAprobacion))
        {
            return Resultado<SolicitudDetalle>.Fallo(TipoFallo.NoPermitido, MensajesSolicitud.SolicitudAjena);
        }

        var solicitud = await _repositorio.ObtenerAsync(comando.SolicitudId, cancellationToken);

        if (solicitud is null)
        {
            return Resultado<SolicitudDetalle>.Fallo(TipoFallo.NoEncontrado, MensajesSolicitud.SolicitudNoEncontrada);
        }

        var ahora = _reloj.Ahora;
        ReservaEspacio? ocupacion = null;

        try
        {
            switch (comando.Estado)
            {
                case EstadoSolicitud.EnRevision:
                    solicitud.TomarEnRevision(revisor, ahora);
                    break;

                case EstadoSolicitud.Aprobado:
                    if (await MotivoCalendarioAsync(solicitud, cancellationToken) is { } motivo)
                    {
                        return Resultado<SolicitudDetalle>.Fallo(TipoFallo.Conflicto, motivo);
                    }

                    solicitud.Aprobar(revisor, ahora);
                    ocupacion = ReservaEspacio.Crear(
                        solicitud.EspacioId,
                        solicitud.SolicitanteId,
                        OrigenOcupacion.Solicitud,
                        solicitud.Id,
                        solicitud.Franja,
                        ahora);
                    break;

                default:
                    solicitud.Rechazar(revisor, comando.Motivo, _opciones.LongitudMinimaMotivoRechazo, ahora);
                    break;
            }
        }
        catch (ConflictoException excepcion)
        {
            return Resultado<SolicitudDetalle>.Fallo(TipoFallo.Conflicto, excepcion.Message);
        }
        catch (DomainException excepcion)
        {
            return Resultado<SolicitudDetalle>.Fallo(
                TipoFallo.Validacion,
                excepcion.Message,
                MapeoSolicitudes.Errores(MapeoSolicitudes.CampoDe(excepcion.Message), excepcion.Message));
        }

        var correo = ComponerCorreo(detalle, solicitud, ahora);

        var persistencia = await _repositorio.GuardarDecisionAsync(
            solicitud,
            ocupacion,
            correo,
            cancellationToken);

        return persistencia switch
        {
            ResultadoDecisionSolicitud.Guardada => Resultado<SolicitudDetalle>.Exito(
                detalle with
                {
                    Estado = solicitud.Estado,
                    RevisadaEn = solicitud.RevisadaEn,
                    RevisorId = solicitud.RevisorId,
                    MotivoRechazo = solicitud.MotivoRechazo
                }),
            ResultadoDecisionSolicitud.FranjaNoDisponible => Resultado<SolicitudDetalle>.Fallo(
                TipoFallo.Conflicto,
                MensajesSolicitud.FranjaNoDisponible),
            _ => Resultado<SolicitudDetalle>.Fallo(
                TipoFallo.Conflicto,
                "La solicitud fue modificada por otra sesión. Recargue los datos e intente nuevamente.")
        };
    }

    private async Task<string?> MotivoCalendarioAsync(
        SolicitudPrestamo solicitud,
        CancellationToken cancellationToken)
    {
        var zona = _reservas.ObtenerZonaHoraria();
        var inicio = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(solicitud.Inicio, zona).DateTime);
        var fin = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(solicitud.Fin, zona).DateTime);

        var calendario = await _calendario.ObtenerCalendarioAsync(inicio, fin, zona, cancellationToken);

        return ValidacionesSolicitud.MensajeCalendario(solicitud.Franja, calendario, zona);
    }

    private static CorreoPendiente? ComponerCorreo(
        SolicitudDetalle detalle,
        SolicitudPrestamo solicitud,
        DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(detalle.SolicitanteCorreo))
        {
            return null;
        }

        return CorreoPendiente.Crear(
            detalle.SolicitanteCorreo,
            PlantillaCorreoSolicitud.Asunto(solicitud.Estado),
            PlantillaCorreoSolicitud.Componer(detalle, solicitud.Estado, solicitud.MotivoRechazo),
            solicitud.Id,
            ahora);
    }
}
