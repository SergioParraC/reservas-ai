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
        _ => "Conflicto con el estado actual"
    };
}
