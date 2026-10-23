using System.Globalization;
using System.Net;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Enums;

namespace Backend.Application.Modules.Solicitudes;

/// <summary>
/// Plantilla HTML de las notificaciones de estado. Escapa todo el contenido ingresado por el
/// usuario y nunca incluye la ruta física ni la ruta relativa del pre-informe.
/// </summary>
public static class PlantillaCorreoSolicitud
{
    /// <summary>Asunto del mensaje según el estado notificado.</summary>
    public static string Asunto(EstadoSolicitud estado) => estado switch
    {
        EstadoSolicitud.EnRevision => "Su solicitud de préstamo está en revisión",
        EstadoSolicitud.Aprobado => "Su solicitud de préstamo fue aprobada",
        EstadoSolicitud.Rechazado => "Su solicitud de préstamo fue rechazada",
        _ => "Su solicitud de préstamo fue radicada"
    };

    /// <summary>
    /// Compone el cuerpo HTML con área, franja, estado y motivo cuando aplica.
    /// </summary>
    public static string Componer(SolicitudDetalle solicitud, EstadoSolicitud estado, string? motivo)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var filas = new List<string>
        {
            Fila("Área", solicitud.EspacioNombre),
            Fila("Materia", solicitud.Materia),
            Fila("Franja", $"{Formatear(solicitud.Inicio)} - {Formatear(solicitud.Fin)}"),
            Fila("Estado", estado.ToString())
        };

        if (estado == EstadoSolicitud.Rechazado && !string.IsNullOrWhiteSpace(motivo))
        {
            filas.Add(Fila("Motivo", motivo));
        }

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="utf-8" /><title>{Escapar(Asunto(estado))}</title></head>
            <body style="font-family: Segoe UI, Arial, sans-serif; color: #1f2937;">
              <h1 style="font-size: 20px;">{Escapar(Asunto(estado))}</h1>
              <p>Hola {Escapar(solicitud.SolicitanteNombre)},</p>
              <p>Le informamos que su solicitud de pre-informe cambió de estado:</p>
              <table style="border-collapse: collapse;" cellpadding="6">
                {string.Join(Environment.NewLine, filas)}
              </table>
              <p style="font-size: 12px; color: #6b7280;">
                Este mensaje lo genera automáticamente el sistema de reservas de espacios.
              </p>
            </body>
            </html>
            """;
    }

    /// <summary>Escapa el texto para insertarlo con seguridad dentro del HTML.</summary>
    public static string Escapar(string? valor) => WebUtility.HtmlEncode(valor ?? string.Empty);

    private static string Fila(string etiqueta, string valor) =>
        $"<tr><td><strong>{Escapar(etiqueta)}</strong></td><td>{Escapar(valor)}</td></tr>";

    private static string Formatear(DateTimeOffset momento) =>
        momento.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
