namespace Backend.Domain.Enums;

/// <summary>
/// Responsable de aprobar las solicitudes de reserva de un área.
/// </summary>
public enum TipoAprobacion
{
    /// <summary>La reserva se confirma sin intervención humana.</summary>
    Automatica = 1,

    /// <summary>Un docente del área aprueba la solicitud.</summary>
    Docente = 2,

    /// <summary>El personal administrativo aprueba la solicitud.</summary>
    Administrativo = 3
}
