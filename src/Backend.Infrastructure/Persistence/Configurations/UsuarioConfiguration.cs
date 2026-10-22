using Backend.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API del perfil institucional: tabla, longitudes máximas,
/// conversión explícita de <c>TipoActor</c> a <c>int</c> e índice único filtrado
/// sobre el identificador federado.
/// </summary>
public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    /// <summary>Nombre máximo del perfil institucional.</summary>
    public const int LongitudMaximaNombreCompleto = 200;

    /// <summary>Longitud máxima del claim <c>oid</c> de Entra ID.</summary>
    public const int LongitudMaximaIdentificadorFederado = 100;

    /// <summary>Nombre del índice único filtrado sobre el identificador federado.</summary>
    public const string NombreIndiceIdentificadorFederado = "IX_AspNetUsers_IdentificadorFederado";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AspNetUsers");

        builder.Property(usuario => usuario.NombreCompleto)
            .HasMaxLength(LongitudMaximaNombreCompleto)
            .IsRequired();

        // Se persiste como int para que el esquema no dependa del orden de los nombres del enum.
        builder.Property(usuario => usuario.TipoActor)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(usuario => usuario.IdentificadorFederado)
            .HasMaxLength(LongitudMaximaIdentificadorFederado);

        builder.Property(usuario => usuario.Activo)
            .IsRequired();

        builder.Property(usuario => usuario.CreadoEn)
            .IsRequired();

        builder.HasIndex(usuario => usuario.IdentificadorFederado)
            .IsUnique()
            .HasDatabaseName(NombreIndiceIdentificadorFederado)
            .HasFilter("[IdentificadorFederado] IS NOT NULL");
    }
}
