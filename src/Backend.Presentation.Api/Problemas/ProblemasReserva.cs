using Backend.Domain.Exceptions;

namespace Backend.Presentation.Api.Problemas;

/// <summary>
/// Traduce los mensajes de conflicto del dominio de reservas a los <c>type</c> estables
/// del contrato RFC 7807.
/// </summary>
public static class ProblemasReserva
{
    /// <summary>Slug del problema de franja ya ocupada.</summary>
    public const string FranjaNoDisponible = "franja-no-disponible";

    /// <summary>Slug del problema de puesto que no admite préstamo directo.</summary>
    public const string PuestoNoReservable = "puesto-no-reservable";

    /// <summary>Slug del problema de cancelación fuera del plazo permitido.</summary>
    public const string FueraDePlazo = "fuera-de-plazo";

    /// <summary>Slug del problema de cupo de reservas activas agotado.</summary>
    public const string LimiteReservasAlcanzado = "limite-reservas-alcanzado";

    /// <summary>Slug del problema de check-in fuera de la ventana de tolerancia.</summary>
    public const string CheckInFueraDeVentana = "check-in-fuera-de-ventana";

    /// <summary>Slug del problema de código QR que no pertenece al puesto reservado.</summary>
    public const string CheckInCodigoInvalido = "check-in-codigo-invalido";

    /// <summary>Slug del problema de transición de estado no admitida.</summary>
    public const string TransicionInvalida = "transicion-invalida";

    /// <summary>Slug del problema de reserva de otro titular.</summary>
    public const string ReservaAjena = "reserva-ajena";

    /// <summary>
    /// Devuelve el slug de asistencia correspondiente al mensaje de check-in o check-out, o
    /// <see langword="null"/> cuando el mensaje pertenece a otro módulo.
    /// </summary>
    public static string? SlugAsistencia(string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return null;
        }

        if (string.Equals(mensaje, MensajesReserva.CheckInCodigoInvalido, StringComparison.Ordinal))
        {
            return CheckInCodigoInvalido;
        }

        if (string.Equals(mensaje, MensajesReserva.TransicionInvalida, StringComparison.Ordinal))
        {
            return TransicionInvalida;
        }

        if (string.Equals(mensaje, MensajesReserva.ReservaAjena, StringComparison.Ordinal))
        {
            return ReservaAjena;
        }

        return mensaje.StartsWith(Prefijo(MensajesReserva.CheckInFueraDeVentana), StringComparison.Ordinal)
            ? CheckInFueraDeVentana
            : null;
    }

    /// <summary>Devuelve el slug de conflicto correspondiente al mensaje o <see langword="null"/>.</summary>
    public static string? SlugConflicto(string? mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return null;
        }

        if (string.Equals(mensaje, MensajesReserva.FranjaNoDisponible, StringComparison.Ordinal))
        {
            return FranjaNoDisponible;
        }

        if (string.Equals(mensaje, MensajesReserva.PuestoNoReservable, StringComparison.Ordinal))
        {
            return PuestoNoReservable;
        }

        if (mensaje.StartsWith(Prefijo(MensajesReserva.CupoAgotado), StringComparison.Ordinal))
        {
            return LimiteReservasAlcanzado;
        }

        return mensaje.StartsWith(Prefijo(MensajesReserva.FueraDePlazo), StringComparison.Ordinal)
            ? FueraDePlazo
            : null;
    }

    private static string Prefijo(string plantilla) =>
        plantilla[..plantilla.IndexOf('{', StringComparison.Ordinal)];

    /// <summary>Título legible del problema a partir de su slug.</summary>
    public static string Titulo(string slug) => slug switch
    {
        FranjaNoDisponible => "Franja no disponible",
        PuestoNoReservable => "Puesto no reservable",
        FueraDePlazo => "Fuera de plazo",
        LimiteReservasAlcanzado => "Límite de reservas alcanzado",
        CheckInFueraDeVentana => "Check-in fuera de ventana",
        CheckInCodigoInvalido => "Código de check-in inválido",
        TransicionInvalida => "Transición de estado inválida",
        ReservaAjena => "Reserva ajena",
        _ => "Conflicto con el estado actual"
    };
}
