using Backend.Domain.Enums;

namespace Backend.Application.Modules.Solicitudes.Dtos;

/// <summary>Resumen de una solicitud en la consulta propia del solicitante.</summary>
public sealed record SolicitudResumen(
    Guid Id,
    Guid EspacioId,
    string EspacioNombre,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    EstadoSolicitud Estado,
    string? MotivoRechazo,
    DateTimeOffset RadicadaEn);

/// <summary>Elemento de la bandeja de aprobaciones con los datos del solicitante.</summary>
public sealed record SolicitudBandejaItem(
    Guid Id,
    Guid EspacioId,
    string EspacioNombre,
    Guid SolicitanteId,
    string SolicitanteNombre,
    string SolicitanteCorreo,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string Materia,
    EstadoSolicitud Estado,
    DateTimeOffset RadicadaEn);

/// <summary>Detalle completo de una solicitud, sin exponer la ruta física del archivo.</summary>
public sealed record SolicitudDetalle(
    Guid Id,
    Guid EspacioId,
    string EspacioNombre,
    TipoAprobacion TipoAprobacion,
    TipoModalidadPrestamo TipoModalidadPrestamo,
    Guid SolicitanteId,
    string SolicitanteNombre,
    string SolicitanteCorreo,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string Materia,
    string DocenteResponsable,
    string Justificacion,
    EstadoSolicitud Estado,
    DateTimeOffset RadicadaEn,
    DateTimeOffset? RevisadaEn,
    Guid? RevisorId,
    string? MotivoRechazo,
    string ArchivoNombreOriginal,
    long ArchivoTamanoBytes,
    string ArchivoRuta)
{
    /// <summary>Indica si la solicitud ya no admite transiciones.</summary>
    public bool EsTerminal => Estado is EstadoSolicitud.Aprobado or EstadoSolicitud.Rechazado;
}

/// <summary>Datos de entrada para radicar una solicitud con su pre-informe.</summary>
public sealed record ComandoRadicarSolicitud(
    Guid EspacioId,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string Materia,
    string DocenteResponsable,
    string Justificacion,
    Stream? Archivo,
    string? NombreArchivo,
    string? TipoContenido,
    long LongitudArchivo);

/// <summary>Filtros de la consulta de solicitudes propias.</summary>
public sealed record FiltroSolicitudesPropias(
    EstadoSolicitud? Estado,
    int Pagina,
    int Tamano);

/// <summary>Filtros combinables de la bandeja de aprobaciones.</summary>
public sealed record FiltroBandeja(
    EstadoSolicitud? Estado,
    Guid? EspacioId,
    DateTimeOffset? Desde,
    DateTimeOffset? Hasta,
    int Pagina,
    int Tamano);

/// <summary>Pre-informe listo para descargarse con su nombre original.</summary>
public sealed record PreinformeDescarga(
    string NombreOriginal,
    Stream Contenido,
    string TipoContenido);

/// <summary>Comando de cambio de estado de una solicitud.</summary>
public sealed record ComandoCambiarEstado(Guid SolicitudId, EstadoSolicitud Estado, string? Motivo);

/// <summary>Respuesta del alta de una solicitud, sin exponer la ruta interna del archivo.</summary>
public sealed record SolicitudRadicada(
    Guid Id,
    Guid EspacioId,
    EstadoSolicitud Estado,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    DateTimeOffset RadicadaEn,
    string ArchivoNombreOriginal,
    long ArchivoTamanoBytes);

