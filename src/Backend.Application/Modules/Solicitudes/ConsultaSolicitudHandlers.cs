using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Solicitudes;

/// <summary>Consulta paginada del historial de solicitudes del solicitante autenticado.</summary>
public sealed class ConsultarSolicitudesHandler
{
    private readonly IConsultaSolicitudes _consulta;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos y la identidad actual.</summary>
    public ConsultarSolicitudesHandler(IConsultaSolicitudes consulta, IUsuarioActual usuario)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Devuelve las solicitudes del usuario de la más reciente a la más antigua.</summary>
    public async Task<Resultado<PaginaResultado<SolicitudResumen>>> HandleAsync(
        EstadoSolicitud? estado,
        int pagina,
        int tamano,
        CancellationToken cancellationToken = default)
    {
        if (_usuario.UsuarioId is not { } solicitante)
        {
            return Resultado<PaginaResultado<SolicitudResumen>>.Fallo(
                TipoFallo.NoAutorizado,
                MensajesReserva.SinSesion);
        }

        var filtro = new FiltroSolicitudesPropias(
            estado is { } valor && Enum.IsDefined(valor) ? valor : null,
            Paginacion.NormalizarPagina(pagina),
            Paginacion.NormalizarTamano(tamano));

        var resultado = await _consulta.ListarPropiasAsync(solicitante, filtro, cancellationToken);

        return Resultado<PaginaResultado<SolicitudResumen>>.Exito(resultado);
    }
}

/// <summary>
/// Descarga autenticada del pre-informe. Solo el autor y los revisores competentes del área
/// acceden al documento; el archivo nunca se sirve desde el almacén estático.
/// </summary>
public sealed class DescargarPreinformeHandler
{
    private readonly IConsultaSolicitudes _consulta;
    private readonly IAlmacenDocumentos _documentos;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos y la identidad actual.</summary>
    public DescargarPreinformeHandler(
        IConsultaSolicitudes consulta,
        IAlmacenDocumentos documentos,
        IUsuarioActual usuario)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _documentos = documentos ?? throw new ArgumentNullException(nameof(documentos));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Devuelve el flujo del PDF cuando el usuario tiene competencia sobre la solicitud.</summary>
    public async Task<Resultado<PreinformeDescarga>> HandleAsync(
        Guid solicitudId,
        CancellationToken cancellationToken = default)
    {
        if (_usuario.UsuarioId is not { } usuarioId)
        {
            return Resultado<PreinformeDescarga>.Fallo(TipoFallo.NoAutorizado, MensajesReserva.SinSesion);
        }

        var solicitud = await _consulta.ObtenerAsync(solicitudId, cancellationToken);

        if (solicitud is null)
        {
            return Resultado<PreinformeDescarga>.Fallo(TipoFallo.NoEncontrado, MensajesSolicitud.SolicitudNoEncontrada);
        }

        if (!PuedeDescargar(solicitud, usuarioId))
        {
            return Resultado<PreinformeDescarga>.Fallo(TipoFallo.NoPermitido, MensajesSolicitud.SolicitudAjena);
        }

        var archivo = await _documentos.AbrirAsync(solicitud.ArchivoRuta, cancellationToken);

        if (!archivo.Exitoso || archivo.Valor is null)
        {
            return Resultado<PreinformeDescarga>.Fallo(
                TipoFallo.NoEncontrado,
                MensajesSolicitud.ArchivoNoEncontrado);
        }

        return Resultado<PreinformeDescarga>.Exito(new PreinformeDescarga(
            solicitud.ArchivoNombreOriginal,
            archivo.Valor.Contenido,
            archivo.Valor.TipoContenido));
    }

    private bool PuedeDescargar(SolicitudDetalle solicitud, Guid usuarioId)
    {
        if (solicitud.SolicitanteId == usuarioId)
        {
            return true;
        }

        var roles = _usuario.Roles;

        if (roles.Any(rol => string.Equals(rol, RolesAplicacion.Administrativo, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return roles.Any(rol => string.Equals(rol, RolesAplicacion.Docente, StringComparison.OrdinalIgnoreCase))
            && solicitud.TipoAprobacion == TipoAprobacion.Docente;
    }
}
