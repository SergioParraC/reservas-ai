namespace Backend.Domain.Enums;

/// <summary>
/// Forma en que un área se presta. Determina qué tipo de reserva admite: series o solicitudes
/// de área completa frente a reservas de unidades individuales.
/// </summary>
public enum TipoModalidadPrestamo
{
    /// <summary>El área se reserva como una unidad indivisible (laboratorio, auditorio).</summary>
    AreaCompleta = 1,

    /// <summary>Se reservan puestos individuales del área (sala de cómputo).</summary>
    PuestoIndividual = 2,

    /// <summary>Se reservan sub-zonas compartidas del área (zona de descanso, juegos).</summary>
    SubZonaCompartida = 3
}
