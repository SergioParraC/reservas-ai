using Backend.Domain.Enums;
using Backend.Domain.Exceptions;

namespace Backend.Domain.Entities;

/// <summary>
/// Correo de la cola de salida. Se persiste en la misma transacción de la decisión para que
/// ninguna decisión quede sin notificación y ninguna notificación sin decisión.
/// </summary>
public sealed class CorreoPendiente
{
    /// <summary>Reintentos máximos antes de marcar el correo como fallido.</summary>
    public const int IntentosMaximos = 5;

    private CorreoPendiente()
    {
    }

    private CorreoPendiente(
        Guid id,
        string destinatario,
        string asunto,
        string cuerpoHtml,
        Guid referenciaId,
        DateTimeOffset creadoEn,
        DateTimeOffset proximoIntentoEn)
    {
        Id = id;
        Destinatario = destinatario;
        Asunto = asunto;
        CuerpoHtml = cuerpoHtml;
        ReferenciaId = referenciaId;
        Estado = EstadoCorreo.Pendiente;
        Intentos = 0;
        ProximoIntentoEn = proximoIntentoEn;
        CreadoEn = creadoEn;
    }

    /// <summary>Identificador interno del correo.</summary>
    public Guid Id { get; private set; }

    /// <summary>Buzón del solicitante.</summary>
    public string Destinatario { get; private set; } = string.Empty;

    /// <summary>Asunto del mensaje.</summary>
    public string Asunto { get; private set; } = string.Empty;

    /// <summary>Cuerpo HTML ya escapado y compuesto.</summary>
    public string CuerpoHtml { get; private set; } = string.Empty;

    /// <summary>Solicitud que originó la notificación.</summary>
    public Guid ReferenciaId { get; private set; }

    /// <summary>Estado del despacho.</summary>
    public EstadoCorreo Estado { get; private set; }

    /// <summary>Intentos de envío realizados.</summary>
    public int Intentos { get; private set; }

    /// <summary>Instante a partir del cual el despachador puede reintentar.</summary>
    public DateTimeOffset ProximoIntentoEn { get; private set; }

    /// <summary>Momento del encolado.</summary>
    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Momento del envío exitoso.</summary>
    public DateTimeOffset? EnviadoEn { get; private set; }

    /// <summary>Último error registrado por el despachador.</summary>
    public string? UltimoError { get; private set; }

    /// <summary>Indica si el correo aún puede despacharse.</summary>
    public bool EstaPendiente => Estado == EstadoCorreo.Pendiente;

    /// <summary>Encola una notificación pendiente de despacho inmediato.</summary>
    public static CorreoPendiente Crear(
        string destinatario,
        string asunto,
        string cuerpoHtml,
        Guid referenciaId,
        DateTimeOffset creadoEn)
    {
        if (string.IsNullOrWhiteSpace(destinatario))
        {
            throw new DomainException("El destinatario del correo es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(asunto))
        {
            throw new DomainException("El asunto del correo es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(cuerpoHtml))
        {
            throw new DomainException("El cuerpo del correo es obligatorio.");
        }

        return new CorreoPendiente(
            Guid.NewGuid(),
            destinatario.Trim(),
            asunto.Trim(),
            cuerpoHtml,
            referenciaId,
            creadoEn,
            creadoEn);
    }

    /// <summary>Marca el correo como enviado con su fecha de entrega.</summary>
    public void MarcarEnviado(DateTimeOffset momento)
    {
        Estado = EstadoCorreo.Enviado;
        EnviadoEn = momento;
        UltimoError = null;
    }

    /// <summary>
    /// Registra un fallo de envío y programa el siguiente intento con retroceso exponencial.
    /// Agotados los reintentos, el correo queda como fallido y se conserva para inspección.
    /// </summary>
    public void RegistrarFallo(string error, DateTimeOffset momento)
    {
        Intentos++;
        UltimoError = string.IsNullOrWhiteSpace(error) ? "Fallo de envío desconocido." : error.Trim();

        if (Intentos >= IntentosMaximos)
        {
            Estado = EstadoCorreo.Fallido;
            return;
        }

        var espera = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Intentos) * 30, 3600));

        ProximoIntentoEn = momento + espera;
    }
}
