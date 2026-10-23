using Backend.Domain.Enums;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Solicitudes.Dtos;

/// <summary>Mapeos y clasificación de mensajes del módulo de solicitudes.</summary>
public static class MapeoSolicitudes
{
    /// <summary>Construye el diccionario de errores por campo del contrato RFC 7807.</summary>
    public static IDictionary<string, string[]> Errores(string campo, string mensaje) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal) { [campo] = [mensaje] };

    /// <summary>Determina el campo del cuerpo al que corresponde un mensaje de dominio.</summary>
    public static string CampoDe(string mensaje) => mensaje switch
    {
        MensajesSolicitud.MateriaRequerida or MensajesSolicitud.MateriaDemasiadoLarga => "materia",
        MensajesSolicitud.DocenteRequerido or MensajesSolicitud.DocenteDemasiadoLargo => "docenteResponsable",
        MensajesSolicitud.JustificacionRequerida or MensajesSolicitud.JustificacionDemasiadoLarga => "justificacion",
        MensajesSolicitud.ArchivoRequerido or MensajesSolicitud.ArchivoNoValido
            or MensajesSolicitud.ArchivoDemasiadoGrande => "archivo",
        MensajesSolicitud.MotivoRequerido or MensajesSolicitud.MotivoDemasiadoCorto
            or MensajesSolicitud.MotivoDemasiadoLargo => "motivo",
        MensajesSolicitud.FranjaInvalida or MensajesSolicitud.InicioFueraDeAntelacion
            or MensajesSolicitud.InicioNoAlineado or MensajesSolicitud.DuracionFueraDeRango => "inicio",
        MensajesSolicitud.FranjaFueraDeJornada => "fin",
        _ => "espacioId"
    };

    /// <summary>Indica si el estado recibido es el de radicación inicial.</summary>
    public static bool EsRadicado(EstadoSolicitud estado) => estado == EstadoSolicitud.Radicado;
}
