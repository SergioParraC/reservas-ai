namespace Backend.Domain.Enums;

/// <summary>
/// Estado del ciclo de vida de una unidad física (área o puesto).
/// La baja es lógica: el registro y su código QR se conservan.
/// </summary>
public enum EstadoUnidad
{
    /// <summary>La unidad está publicada y admite reservas.</summary>
    Activo = 1,

    /// <summary>La unidad fue dada de baja y no aparece en el catálogo.</summary>
    Inactivo = 2
}
