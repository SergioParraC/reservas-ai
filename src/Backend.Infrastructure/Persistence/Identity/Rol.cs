using Microsoft.AspNetCore.Identity;

namespace Backend.Infrastructure.Persistence.Identity;

/// <summary>
/// Rol de autorización de la plataforma. Se siembra con exactamente tres valores:
/// <c>Administrativo</c>, <c>Docente</c> y <c>Estudiante</c>.
/// </summary>
public sealed class Rol : IdentityRole<Guid>
{
    /// <summary>Constructor sin parámetros requerido por EF Core.</summary>
    private Rol()
    {
    }

    /// <summary>Crea un rol con la clave y el nombre indicados.</summary>
    public Rol(Guid id, string nombre)
    {
        Id = id;
        Name = nombre;
        NormalizedName = nombre.ToUpperInvariant();
    }
}
