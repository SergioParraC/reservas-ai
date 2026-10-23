using System.ComponentModel.DataAnnotations;

namespace Backend.Presentation.Api.Contracts;

/// <summary>Cuerpo del formulario público de autorregistro.</summary>
public sealed class SolicitudRegistroUsuario
{
    /// <summary>Nombre y apellidos del solicitante.</summary>
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [MaxLength(200, ErrorMessage = "El nombre completo no puede superar los 200 caracteres.")]
    public string NombreCompleto { get; init; } = string.Empty;

    /// <summary>Correo institucional que se usará también en Entra ID.</summary>
    [Required(ErrorMessage = "El correo institucional es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo institucional no tiene un formato válido.")]
    public string CorreoInstitucional { get; init; } = string.Empty;

    /// <summary>Contraseña local que cumple la política de Identity.</summary>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Contrasena { get; init; } = string.Empty;
}

/// <summary>Token de Entra ID que prueba la posesión de la identidad institucional.</summary>
public sealed class SolicitudVinculacionEntra
{
    /// <summary>Access token emitido por el inquilino institucional.</summary>
    [Required(ErrorMessage = "El token de Entra ID es obligatorio.")]
    public string TokenEntra { get; init; } = string.Empty;
}

/// <summary>Datos de alta de un área.</summary>
public class SolicitudCrearEspacio
{
    /// <summary>Nombre visible del área.</summary>
    [Required(ErrorMessage = "El nombre del área es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El nombre del área no puede superar los 100 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Ubicación física del área.</summary>
    [Required(ErrorMessage = "La ubicación del área es obligatoria.")]
    [MaxLength(150, ErrorMessage = "La ubicación del área no puede superar los 150 caracteres.")]
    public string Ubicacion { get; init; } = string.Empty;

    /// <summary>Categoría funcional (nombre del enum, sin distinguir mayúsculas).</summary>
    [Required(ErrorMessage = "La categoría del área es obligatoria.")]
    public string Categoria { get; init; } = string.Empty;

    /// <summary>Aforo máximo de personas.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "El aforo debe ser mayor o igual a 1.")]
    public int Aforo { get; init; }

    /// <summary>Tipo de aprobación de reservas (nombre del enum).</summary>
    [Required(ErrorMessage = "El tipo de aprobación es obligatorio.")]
    public string TipoAprobacion { get; init; } = string.Empty;
}

/// <summary>Datos de edición de un área; nunca incluye el código QR.</summary>
public sealed class SolicitudActualizarEspacio : SolicitudCrearEspacio
{
    /// <summary>Estado de la unidad (nombre del enum).</summary>
    [Required(ErrorMessage = "El estado del área es obligatorio.")]
    public string Estado { get; init; } = string.Empty;

    /// <summary>Token de concurrencia devuelto por la consulta anterior.</summary>
    public string? RowVersion { get; init; }
}

/// <summary>Datos de alta de un puesto.</summary>
public sealed class SolicitudCrearPuesto
{
    /// <summary>Identificador visible del puesto dentro del área.</summary>
    [Required(ErrorMessage = "El identificador del puesto es obligatorio.")]
    [MaxLength(50, ErrorMessage = "El identificador del puesto no puede superar los 50 caracteres.")]
    public string Identificador { get; init; } = string.Empty;

    /// <summary>Descripción del equipo instalado.</summary>
    [MaxLength(150, ErrorMessage = "La descripción del equipo no puede superar los 150 caracteres.")]
    public string? DescripcionEquipo { get; init; }
}

/// <summary>Datos de edición de un puesto; nunca incluye el código QR.</summary>
public sealed class SolicitudActualizarPuesto
{
    /// <summary>Identificador visible del puesto dentro del área.</summary>
    [Required(ErrorMessage = "El identificador del puesto es obligatorio.")]
    [MaxLength(50, ErrorMessage = "El identificador del puesto no puede superar los 50 caracteres.")]
    public string Identificador { get; init; } = string.Empty;

    /// <summary>Descripción del equipo instalado.</summary>
    [MaxLength(150, ErrorMessage = "La descripción del equipo no puede superar los 150 caracteres.")]
    public string? DescripcionEquipo { get; init; }

    /// <summary>Estado del puesto (nombre del enum).</summary>
    [Required(ErrorMessage = "El estado del puesto es obligatorio.")]
    public string Estado { get; init; } = string.Empty;
}

/// <summary>Rol que se asigna a una cuenta.</summary>
public sealed class SolicitudCambiarRol
{
    /// <summary>Rol solicitado: <c>Administrativo</c>, <c>Docente</c> o <c>Estudiante</c>.</summary>
    [Required(ErrorMessage = "El rol es obligatorio.")]
    public string Rol { get; init; } = string.Empty;
}
