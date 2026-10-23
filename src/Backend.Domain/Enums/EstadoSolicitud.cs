namespace Backend.Domain.Enums;

/// <summary>
/// Estado del ciclo de vida de una solicitud de préstamo. Los estados
/// <see cref="Aprobado"/> y <see cref="Rechazado"/> son terminales.
/// </summary>
public enum EstadoSolicitud
{
    /// <summary>La solicitud fue radicada con su pre-informe y espera revisión.</summary>
    Radicado = 1,

    /// <summary>Un revisor competente tomó la solicitud para decidirla.</summary>
    EnRevision = 2,

    /// <summary>La solicitud fue aprobada y su franja ocupa la agenda del área.</summary>
    Aprobado = 3,

    /// <summary>La solicitud fue rechazada con un motivo obligatorio.</summary>
    Rechazado = 4
}
