using Backend.Domain.Entities;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Reservas.Dtos;

/// <summary>Mapeos y clasificación de mensajes del módulo de reservas.</summary>
public static class MapeoReservas
{
    /// <summary>Proyecta una reserva recién creada al contrato de detalle.</summary>
    public static ReservaDetalle ADetalle(Reserva reserva, PuestoReservable puesto)
    {
        ArgumentNullException.ThrowIfNull(reserva);
        ArgumentNullException.ThrowIfNull(puesto);

        return new ReservaDetalle(
            reserva.Id,
            reserva.PuestoId,
            puesto.Identificador,
            puesto.EspacioId,
            puesto.EspacioNombre,
            reserva.Inicio,
            reserva.Fin,
            reserva.FinPlanificado,
            reserva.Estado,
            reserva.CreadaEn,
            reserva.CanceladaEn,
            reserva.FechaCheckIn,
            reserva.FechaCheckOut,
            Convert.ToBase64String(reserva.RowVersion));
    }

    /// <summary>Construye el diccionario de errores por campo que consume el contrato RFC 7807.</summary>
    public static IDictionary<string, string[]> Errores(string campo, string mensaje) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal) { [campo] = [mensaje] };

    /// <summary>Determina el campo del cuerpo al que corresponde un mensaje de dominio.</summary>
    public static string CampoDe(string mensaje) => mensaje switch
    {
        MensajesReserva.RangoInvalido or MensajesReserva.FechaFueraDeHorizonte => "fecha",
        MensajesReserva.FranjaNoDisponible => "inicio",
        MensajesReserva.PuestoNoEncontrado or MensajesReserva.PuestoNoReservable => "puestoId",
        MensajesReserva.CupoAgotado => "usuarioId",
        _ => "inicio"
    };
}
