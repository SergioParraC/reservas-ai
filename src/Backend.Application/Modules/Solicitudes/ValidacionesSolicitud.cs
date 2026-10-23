using System.Globalization;
using Backend.Application.Common;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;

namespace Backend.Application.Modules.Solicitudes;

/// <summary>
/// Validaciones compartidas por la radicación y la decisión de solicitudes: archivo, franja y
/// calendario institucional. Se centralizan para que ambas operaciones apliquen la misma regla.
/// </summary>
internal static class ValidacionesSolicitud
{
    /// <summary>Tipo de contenido admitido para el pre-informe.</summary>
    public const string TipoPdf = "application/pdf";

    /// <summary>
    /// Valida presencia, tamaño y tipo declarado del archivo. La firma del PDF la comprueba el
    /// almacén de documentos antes de escribir nada.
    /// </summary>
    public static (TipoFallo Tipo, string Mensaje)? ValidarArchivo(
        ComandoRadicarSolicitud comando,
        OpcionesSolicitudes opciones)
    {
        if (comando.Archivo is null || comando.LongitudArchivo <= 0 || string.IsNullOrWhiteSpace(comando.NombreArchivo))
        {
            return (TipoFallo.Validacion, MensajesSolicitud.ArchivoRequerido);
        }

        if (comando.LongitudArchivo > opciones.TamanoMaximoPdfBytes)
        {
            return (TipoFallo.ArchivoDemasiadoGrande, string.Format(
                CultureInfo.InvariantCulture,
                MensajesSolicitud.ArchivoDemasiadoGrande,
                opciones.TamanoMaximoPdfMegabytes));
        }

        return string.Equals(comando.TipoContenido, TipoPdf, StringComparison.OrdinalIgnoreCase)
            ? null
            : (TipoFallo.TipoNoSoportado, MensajesSolicitud.ArchivoNoValido);
    }

    /// <summary>
    /// Construye la franja solicitada validando duración, alineación, jornada y antelación
    /// sin aplicar el horizonte máximo del préstamo directo.
    /// </summary>
    public static FranjaHoraria CrearFranja(
        ComandoRadicarSolicitud comando,
        PoliticaReserva politica,
        TimeZoneInfo zona,
        DateTimeOffset ahora,
        int antelacionMinutos)
    {
        ArgumentNullException.ThrowIfNull(politica);
        ArgumentNullException.ThrowIfNull(zona);

        var inicio = TimeZoneInfo.ConvertTime(comando.Inicio, zona);
        var fin = TimeZoneInfo.ConvertTime(comando.Fin, zona);

        if (fin <= inicio)
        {
            throw new DomainException(MensajesSolicitud.FranjaInvalida);
        }

        var duracion = fin - inicio;

        if (duracion < politica.DuracionMinima || duracion > politica.DuracionMaxima)
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesSolicitud.DuracionFueraDeRango,
                (int)politica.DuracionMinima.TotalMinutes,
                (int)politica.DuracionMaxima.TotalMinutes));
        }

        var intervalo = politica.IntervaloMinutos;

        if (intervalo < 1
            || inicio.TimeOfDay.TotalMinutes % intervalo != 0
            || fin.TimeOfDay.TotalMinutes % intervalo != 0)
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesSolicitud.InicioNoAlineado,
                intervalo));
        }

        if (!politica.DentroDeJornada(TimeOnly.FromTimeSpan(inicio.TimeOfDay))
            || !politica.DentroDeJornada(TimeOnly.FromTimeSpan(fin.TimeOfDay)))
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesSolicitud.FranjaFueraDeJornada,
                politica.HoraApertura.ToString("HH\\:mm", CultureInfo.InvariantCulture),
                politica.HoraCierre.ToString("HH\\:mm", CultureInfo.InvariantCulture)));
        }

        if (inicio < ahora + TimeSpan.FromMinutes(Math.Max(0, antelacionMinutos)))
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesSolicitud.InicioFueraDeAntelacion,
                Math.Max(0, antelacionMinutos)));
        }

        return FranjaHoraria.Desde(inicio, fin);
    }

    /// <summary>Devuelve el mensaje de conflicto del calendario o <see langword="null"/> si es operable.</summary>
    public static string? MensajeCalendario(
        FranjaHoraria franja,
        CalendarioInstitucional calendario,
        TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(franja);
        ArgumentNullException.ThrowIfNull(calendario);

        return calendario.Evaluar(franja, zona).Motivo switch
        {
            MotivoCalendario.SinPeriodoActivo => MensajesCalendario.SinPeriodoActivo,
            MotivoCalendario.FueraDePeriodo => MensajesCalendario.FueraDePeriodoAcademico,
            MotivoCalendario.NoOperativa => MensajesCalendario.FranjaNoOperativa,
            _ => null
        };
    }
}
