namespace Backend.Application.Common;

/// <summary>
/// Roles oficiales del dominio. La autorización se resuelve exclusivamente con estos valores,
/// tanto para identidades locales como federadas.
/// </summary>
public static class RolesAplicacion
{
    /// <summary>Personal administrativo y de soporte.</summary>
    public const string Administrativo = "Administrativo";

    /// <summary>Docentes.</summary>
    public const string Docente = "Docente";

    /// <summary>Estudiantes.</summary>
    public const string Estudiante = "Estudiante";

    /// <summary>Indica si el rol es académico y puede asignarse a un perfil federado.</summary>
    public static bool EsRolAcademico(string? rol) =>
        string.Equals(rol, Docente, StringComparison.OrdinalIgnoreCase)
        || string.Equals(rol, Estudiante, StringComparison.OrdinalIgnoreCase);
}
