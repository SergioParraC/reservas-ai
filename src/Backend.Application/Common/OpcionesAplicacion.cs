using System.Globalization;
using Backend.Domain.ValueObjects;

namespace Backend.Application.Common;

/// <summary>
/// Política institucional del catálogo físico. Se enlaza desde configuración en infraestructura
/// y se inyecta como objeto plano para no arrastrar el framework de opciones a la aplicación.
/// </summary>
public sealed class OpcionesEspacios
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Espacios";

    /// <summary>Aforo máximo permitido para cualquier área.</summary>
    public int AforoMaximo { get; set; } = 500;

    /// <summary>Cantidad de etiquetas QR por hoja A4.</summary>
    public int EtiquetasPorHoja { get; set; } = 18;

    /// <summary>Tamaño máximo admitido para la fotografía de un área.</summary>
    public long TamanoMaximoImagenBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Cantidad máxima de unidades que puede incluir una plantilla QR.</summary>
    public int MaximoUnidadesPlantilla { get; set; } = 200;
}

/// <summary>Opciones del almacén de archivos de las fotografías.</summary>
public sealed class OpcionesAlmacen
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Almacen";

    /// <summary>Raíz física del almacén; por omisión el volumen de contenedor.</summary>
    public string RutaBase { get; set; } = "/var/lib/reservas";
}

/// <summary>Política del autorregistro de cuentas.</summary>
public sealed class OpcionesIdentidad
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Identidad";

    /// <summary>Indica si el formulario público de registro está habilitado.</summary>
    public bool PermitirAutorregistro { get; set; } = true;

    /// <summary>Dominios de correo institucional admitidos.</summary>
    public IReadOnlyList<string> DominiosCorreo { get; set; } = [];

    /// <summary>Texto original de la clave <c>Identidad:DominioCorreoInstitucional</c>.</summary>
    /// <remarks>
    /// Admite uno o varios dominios separados por comas. El prefijo <c>@</c> es opcional:
    /// <c>uninpahu.edu.co</c> y <c>@uninpahu.edu.co</c> se consideran equivalentes.
    /// </remarks>
    public string DominioCorreoInstitucional { get; set; } = string.Empty;

    /// <summary>Normaliza la lista de dominios admitidos.</summary>
    public IReadOnlyList<string> ObtenerDominios()
    {
        if (DominiosCorreo.Count > 0)
        {
            return DominiosCorreo;
        }

        return DominioCorreoInstitucional
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}

/// <summary>
/// Política de préstamo directo de puestos. Se enlaza desde configuración en infraestructura
/// y se inyecta como objeto plano para no arrastrar el framework de opciones a la aplicación.
/// </summary>
public sealed class OpcionesReservas
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Reservas";

    /// <summary>Antelación mínima, en minutos, para iniciar una reserva nueva.</summary>
    public int AntelacionMinimaMinutos { get; set; } = 30;

    /// <summary>Horizonte máximo, en días, para consultar y reservar.</summary>
    public int AntelacionMaximaDias { get; set; } = 14;

    /// <summary>Duración mínima de una franja, en minutos.</summary>
    public int DuracionMinimaMinutos { get; set; } = 30;

    /// <summary>Duración máxima de una franja, en minutos.</summary>
    public int DuracionMaximaMinutos { get; set; } = 240;

    /// <summary>Granularidad de alineación de las franjas, en minutos.</summary>
    public int IntervaloMinutos { get; set; } = 15;

    /// <summary>Margen mínimo, en minutos, para cancelar una reserva.</summary>
    public int AntelacionCancelacionMinutos { get; set; } = 30;

    /// <summary>
    /// Tolerancia simétrica, en minutos, alrededor del inicio de la franja en la que se admite el
    /// check-in. Es la única fuente de la ventana: no se persiste ni se replica en el dominio.
    /// </summary>
    public int VentanaToleranciaCheckInMinutos { get; set; } = 15;

    /// <summary>Cupo de reservas confirmadas simultáneas por cuenta.</summary>
    public int MaxReservasActivasPorUsuario { get; set; } = 2;

    /// <summary>Hora de apertura de la jornada institucional (<c>HH:mm</c>).</summary>
    public string HoraApertura { get; set; } = "07:00";

    /// <summary>Hora de cierre de la jornada institucional (<c>HH:mm</c>).</summary>
    public string HoraCierre { get; set; } = "21:00";

    /// <summary>Zona horaria institucional usada para construir y mostrar franjas.</summary>
    public string ZonaHoraria { get; set; } = "America/Bogota";

    /// <summary>Construye la política de dominio a partir de la configuración.</summary>
    public PoliticaReserva ObtenerPolitica() => new(
        TimeSpan.FromMinutes(DuracionMinimaMinutos),
        TimeSpan.FromMinutes(DuracionMaximaMinutos),
        IntervaloMinutos,
        TimeSpan.FromMinutes(AntelacionMinimaMinutos),
        TimeSpan.FromDays(AntelacionMaximaDias),
        TimeSpan.FromMinutes(AntelacionCancelacionMinutos),
        TimeOnly.Parse(HoraApertura, CultureInfo.InvariantCulture),
        TimeOnly.Parse(HoraCierre, CultureInfo.InvariantCulture));

    /// <summary>Resuelve la zona horaria institucional.</summary>
    public TimeZoneInfo ObtenerZonaHoraria() => TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria);

    /// <summary>Ventana de tolerancia del check-in como <see cref="TimeSpan"/>.</summary>
    public TimeSpan ObtenerVentanaTolerancia() => TimeSpan.FromMinutes(VentanaToleranciaCheckInMinutos);

    /// <summary>Valida la coherencia de la configuración y falla el arranque si es inválida.</summary>
    public void Validar()
    {
        if (DuracionMinimaMinutos < 1 || DuracionMaximaMinutos < DuracionMinimaMinutos)
        {
            throw new InvalidOperationException(
                "Reservas:DuracionMinimaMinutos y Reservas:DuracionMaximaMinutos no forman un rango válido.");
        }

        if (IntervaloMinutos < 1 || IntervaloMinutos > DuracionMinimaMinutos)
        {
            throw new InvalidOperationException(
                "Reservas:IntervaloMinutos debe ser positivo y no superar la duración mínima.");
        }

        if (AntelacionMinimaMinutos < 0 || AntelacionMaximaDias < 1)
        {
            throw new InvalidOperationException(
                "Reservas:AntelacionMinimaMinutos y Reservas:AntelacionMaximaDias no son válidos.");
        }

        if (AntelacionCancelacionMinutos < 0 || MaxReservasActivasPorUsuario < 1)
        {
            throw new InvalidOperationException(
                "Reservas:AntelacionCancelacionMinutos y Reservas:MaxReservasActivasPorUsuario no son válidos.");
        }

        if (VentanaToleranciaCheckInMinutos is < 1 or > 720)
        {
            throw new InvalidOperationException(
                "Reservas:VentanaToleranciaCheckInMinutos debe estar entre 1 y 720 minutos.");
        }

        var politica = ObtenerPolitica();

        if (politica.HoraCierre <= politica.HoraApertura)
        {
            throw new InvalidOperationException(
                "Reservas:HoraCierre debe ser posterior a Reservas:HoraApertura.");
        }

        try
        {
            ObtenerZonaHoraria();
        }
        catch (TimeZoneNotFoundException excepcion)
        {
            throw new InvalidOperationException(
                $"Reservas:ZonaHoraria no corresponde a una zona horaria conocida ('{ZonaHoraria}').",
                excepcion);
        }
        catch (InvalidTimeZoneException excepcion)
        {
            throw new InvalidOperationException(
                $"Reservas:ZonaHoraria no es una zona horaria válida ('{ZonaHoraria}').",
                excepcion);
        }
    }
}

/// <summary>Rutas públicas estables de la API.</summary>
public static class RutasPublicas
{
    /// <summary>Prefijo del almacén estático de fotografías.</summary>
    public const string Media = "/media";
}
