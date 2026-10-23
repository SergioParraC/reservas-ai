using Backend.Domain.Exceptions;

namespace Backend.Presentation.Api.Problemas;

/// <summary>
/// Traduce los mensajes de series, solicitudes y modalidad de préstamo a los <c>type</c>
/// estables del contrato RFC 7807 y aporta sus títulos legibles.
/// </summary>
public static class ProblemasSolicitud
{
    /// <summary>Slug de la serie que choca con la agenda del área.</summary>
    public const string ColisionSerie = "colision-serie";

    /// <summary>Slug de la serie que no genera ocurrencias operativas.</summary>
    public const string SerieSinOcurrencias = "serie-sin-ocurrencias";

    /// <summary>Slug de la modalidad de préstamo incompatible con la operación.</summary>
    public const string ModalidadIncompatible = "modalidad-incompatible";

    /// <summary>Slug del cambio de modalidad bloqueado por ocupación futura.</summary>
    public const string ModalidadConOcupacion = "modalidad-con-ocupacion";

    /// <summary>Slug del área inactiva para reservas.</summary>
    public const string EspacioNoDisponible = "espacio-no-disponible";

    /// <summary>Slug del área con aprobación automática que no admite solicitudes.</summary>
    public const string EspacioNoSolicitable = "espacio-no-solicitable";

    /// <summary>Slug de la solicitud ajena al usuario.</summary>
    public const string SolicitudAjena = "solicitud-ajena";

    /// <summary>Slug de la transición de estado inválida.</summary>
    public const string TransicionInvalida = "transicion-invalida";

    /// <summary>Slug de la franja ya ocupada.</summary>
    public const string FranjaNoDisponible = "franja-no-disponible";

    /// <summary>Slug del archivo que no es un PDF válido.</summary>
    public const string ArchivoNoValido = "archivo-no-valido";

    /// <summary>Slug del archivo que supera el tamaño máximo.</summary>
    public const string ArchivoDemasiadoGrande = "archivo-demasiado-grande";

    /// <summary>Slug del cupo de solicitudes activas agotado.</summary>
    public const string LimiteSolicitudesAlcanzado = "limite-solicitudes-alcanzado";

    /// <summary>Devuelve el slug de conflicto del módulo o <see langword="null"/>.</summary>
    public static string? SlugConflicto(string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return null;
        }

        if (ProblemasCalendario.SlugConflicto(mensaje) is { } calendario)
        {
            return calendario;
        }

        if (mensaje.StartsWith(Prefijo(MensajesSemestre.ColisionSerie), StringComparison.Ordinal))
        {
            return ColisionSerie;
        }

        if (mensaje.StartsWith(Prefijo(MensajesReserva.CupoAgotado), StringComparison.Ordinal))
        {
            // Ambos cupos comparten el prefijo: el sufijo distingue reservas de solicitudes.
            return mensaje.Contains("reservas activas", StringComparison.Ordinal)
                ? ProblemasReserva.LimiteReservasAlcanzado
                : LimiteSolicitudesAlcanzado;
        }

        if (mensaje.StartsWith(Prefijo(MensajesSolicitud.ArchivoDemasiadoGrande), StringComparison.Ordinal))
        {
            return ArchivoDemasiadoGrande;
        }

        return mensaje switch
        {
            MensajesSemestre.SerieSinOcurrencias => SerieSinOcurrencias,
            MensajesSemestre.EspacioNoDisponible => EspacioNoDisponible,
            MensajesSemestre.ModalidadIncompatible
                or MensajesSolicitud.ModalidadIncompatible
                or MensajesEspacio.ModalidadIncompatible => ModalidadIncompatible,
            MensajesEspacio.ModalidadConOcupacion => ModalidadConOcupacion,
            MensajesSolicitud.EspacioNoSolicitable => EspacioNoSolicitable,
            MensajesSolicitud.EspacioNoDisponible => EspacioNoDisponible,
            MensajesSolicitud.SolicitudAjena => SolicitudAjena,
            MensajesSolicitud.TransicionInvalida => TransicionInvalida,
            MensajesSolicitud.FranjaNoDisponible or MensajesReserva.FranjaNoDisponible => FranjaNoDisponible,
            MensajesSolicitud.ArchivoNoValido => ArchivoNoValido,
            MensajesSolicitud.ArchivoDemasiadoGrande => ArchivoDemasiadoGrande,
            MensajesReserva.PuestoNoReservable => ProblemasReserva.PuestoNoReservable,
            MensajesReserva.FueraDePlazo => ProblemasReserva.FueraDePlazo,
            _ => null
        };
    }

    /// <summary>Título legible del problema a partir de su slug.</summary>
    public static string? Titulo(string slug) => slug switch
    {
        ColisionSerie => "Colisión de la serie",
        SerieSinOcurrencias => "Serie sin ocurrencias",
        ModalidadIncompatible => "Modalidad incompatible",
        ModalidadConOcupacion => "Modalidad con ocupación",
        EspacioNoDisponible => "Área no disponible",
        EspacioNoSolicitable => "Área no solicitable",
        SolicitudAjena => "Solicitud ajena",
        TransicionInvalida => "Transición inválida",
        FranjaNoDisponible => "Franja no disponible",
        ArchivoNoValido => "Archivo no válido",
        ArchivoDemasiadoGrande => "Archivo demasiado grande",
        LimiteSolicitudesAlcanzado => "Límite de solicitudes alcanzado",
        _ => null
    };

    private static string Prefijo(string plantilla) =>
        plantilla[..plantilla.IndexOf('{', StringComparison.Ordinal)];
}
