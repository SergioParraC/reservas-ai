using Backend.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API de los roles de autorización. Solo existen tres roles
/// (<c>Administrativo</c>, <c>Docente</c> y <c>Estudiante</c>) sembrados por la inicialización.
/// </summary>
public sealed class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    /// <summary>Longitud máxima del nombre del rol.</summary>
    public const int LongitudMaximaNombre = 256;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AspNetRoles");

        builder.Property(rol => rol.Name)
            .HasMaxLength(LongitudMaximaNombre)
            .IsRequired();

        builder.Property(rol => rol.NormalizedName)
            .HasMaxLength(LongitudMaximaNombre)
            .IsRequired();
    }
}
