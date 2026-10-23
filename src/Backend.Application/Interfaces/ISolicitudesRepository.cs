using Backend.Application.Common;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Interfaces;

/// <summary>Desenlace de un intento de persistir la decisión de una solicitud.</summary>
public enum ResultadoDecisionSolicitud
{
    /// <summary>La decisión, la ocupación y el correo se confirmaron en una sola transacción.</summary>
    Guardada = 0,

    /// <summary>La franja ya está ocupada por otra fila de la agenda del área.</summary>
    FranjaNoDisponible = 1,

    /// <summary>Conflicto de persistencia o de concurrencia optimista.</summary>
    Conflicto = 2
}

/// <summary>
/// Puerto de escritura de solicitudes. La decisión se confirma junto con la ocupación del área
/// y el correo de la cola en una única transacción serializable.
/// </summary>
public interface ISolicitudesRepository
{
    /// <summary>Carga una solicitud con su token de concurrencia o devuelve <see langword="null"/>.</summary>
    Task<SolicitudPrestamo?> ObtenerAsync(Guid solicitudId, CancellationToken cancellationToken = default);

    /// <summary>Agrega la solicitud radicada al contexto de persistencia.</summary>
    Task AgregarAsync(SolicitudPrestamo solicitud, CancellationToken cancellationToken = default);

    /// <summary>Confirma los cambios pendientes; devuelve <see langword="false"/> ante conflicto.</summary>
    Task<bool> GuardarCambiosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma la decisión de la solicitud, la ocupación del área (cuando se aprueba) y el
    /// correo pendiente dentro de la misma transacción serializable.
    /// </summary>
    Task<ResultadoDecisionSolicitud> GuardarDecisionAsync(
        SolicitudPrestamo solicitud,
        ReservaEspacio? ocupacion,
        CorreoPendiente? correo,
        CancellationToken cancellationToken = default);
}

/// <summary>Puerto de lectura de solicitudes propias y de la bandeja de aprobaciones.</summary>
public interface IConsultaSolicitudes
{
    /// <summary>Lista paginada de las solicitudes del usuario, de la más reciente a la más antigua.</summary>
    Task<PaginaResultado<SolicitudResumen>> ListarPropiasAsync(
        Guid usuarioId,
        FiltroSolicitudesPropias filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Lista la bandeja filtrada por competencia del revisor.</summary>
    Task<PaginaResultado<SolicitudBandejaItem>> ListarBandejaAsync(
        FiltroBandeja filtro,
        IReadOnlyCollection<TipoAprobacion> competencias,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene el detalle de una solicitud con los datos del área y del solicitante.</summary>
    Task<SolicitudDetalle?> ObtenerAsync(Guid solicitudId, CancellationToken cancellationToken = default);

    /// <summary>Cuenta las solicitudes activas (<c>Radicado</c> o <c>EnRevision</c>) del usuario.</summary>
    Task<int> ContarActivasAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

/// <summary>Documento binario servido por un endpoint autenticado.</summary>
public sealed record ArchivoDocumento(Stream Contenido, string TipoContenido, long Longitud);

/// <summary>
/// Puerto del almacén de pre-informes PDF. La base de datos solo guarda la ruta relativa y el
/// archivo nunca se publica en el almacén estático.
/// </summary>
public interface IAlmacenDocumentos
{
    /// <summary>Valida tamaño y firma del PDF y lo guarda con un nombre generado por el servidor.</summary>
    Task<Resultado<string>> GuardarPdfAsync(
        Guid solicitudId,
        Stream contenido,
        string nombreOriginal,
        long longitud,
        CancellationToken cancellationToken = default);

    /// <summary>Abre un documento del almacén para su descarga autenticada.</summary>
    Task<Resultado<ArchivoDocumento>> AbrirAsync(string rutaRelativa, CancellationToken cancellationToken = default);

    /// <summary>Elimina un documento del almacén; un archivo ausente no es un error.</summary>
    Task EliminarAsync(string rutaRelativa, CancellationToken cancellationToken = default);
}

/// <summary>
/// Puerto de la cola de salida de correos. El despachador vive en infraestructura y obtiene los
/// pendientes con bloqueo, de modo que dos instancias no envíen el mismo mensaje.
/// </summary>
public interface IColaCorreos
{
    /// <summary>Toma los correos pendientes cuyo próximo intento ya venció.</summary>
    Task<IReadOnlyList<CorreoPendiente>> TomarPendientesAsync(
        int maximo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken = default);

    /// <summary>Marca el correo como enviado con su fecha de entrega.</summary>
    Task MarcarEnviadoAsync(Guid correoId, DateTimeOffset momento, CancellationToken cancellationToken = default);

    /// <summary>Registra un fallo de envío y programa el siguiente intento.</summary>
    Task MarcarFalloAsync(
        Guid correoId,
        string error,
        DateTimeOffset momento,
        CancellationToken cancellationToken = default);
}

/// <summary>Resultado del intento de envío de un correo.</summary>
public sealed record ResultadoEnvioCorreo(bool Exitoso, string? Error);

/// <summary>Puerto de envío de mensajes SMTP.</summary>
public interface IEmailService
{
    /// <summary>Envía el mensaje HTML y devuelve el error cuando el servidor lo rechaza.</summary>
    Task<ResultadoEnvioCorreo> EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpoHtml,
        CancellationToken cancellationToken = default);
}
