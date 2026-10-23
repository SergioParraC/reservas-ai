namespace Backend.Domain.Enums;

/// <summary>
/// Elemento físico que se presta en un área. Se combina con <see cref="TipoModalidadPrestamo"/>
/// según la matriz institucional de combinaciones válidas.
/// </summary>
public enum TipoElementoPrestamo
{
    /// <summary>Equipos de cómputo individuales.</summary>
    EquipoComputo = 1,

    /// <summary>Laboratorio especializado con equipamiento propio.</summary>
    LaboratorioEspecializado = 2,

    /// <summary>Zona de descanso o de juegos.</summary>
    ZonaDescansoJuegos = 3,

    /// <summary>Auditorio o sala de conferencias.</summary>
    Auditorio = 4
}
