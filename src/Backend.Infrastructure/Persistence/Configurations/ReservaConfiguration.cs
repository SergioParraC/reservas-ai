using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API de las reservas: franjas en <c>datetimeoffset</c>, estado como
/// <c>int</c>, token de concurrencia optimista e índices de agenda y cupo. El índice único
/// filtrado solo cubre reservas confirmadas, de modo que una cancelación libera la franja.
/// </summary>
public sealed class ReservaConfiguration : IEntityTypeConfiguration<Reserva>
{
    /// <summary>Nombre del índice único de reservas vigentes por puesto e inicio.</summary>
    public const string IndiceUnicoVigente = "IX_Reservas_PuestoId_Inicio_Activa";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Reserva> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Reservas");
        builder.HasKey(reserva => reserva.Id);
        builder.Property(reserva => reserva.Id).ValueGeneratedNever();

        builder.Property(reserva => reserva.PuestoId).IsRequired();
        builder.Property(reserva => reserva.UsuarioId).IsRequired();
        builder.Property(reserva => reserva.Inicio).IsRequired();
        builder.Property(reserva => reserva.Fin).IsRequired();

        builder.Property(reserva => reserva.Estado)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(reserva => reserva.CreadaEn).IsRequired();
        builder.Property(reserva => reserva.CanceladaEn);
        builder.Property(reserva => reserva.RowVersion).IsRowVersion();

        // La franja y la vigencia son valores derivados de las columnas persistidas.
        builder.Ignore(reserva => reserva.Franja);
        builder.Ignore(reserva => reserva.EstaVigente);

        builder.HasOne<Puesto>()
            .WithMany()
            .HasForeignKey(reserva => reserva.PuestoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Reservas_Puestos");

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(reserva => reserva.UsuarioId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Reservas_AspNetUsers");

        builder.HasIndex(reserva => new { reserva.PuestoId, reserva.Inicio })
            .IsUnique()
            .HasFilter("[Estado] = 1")
            .HasDatabaseName(IndiceUnicoVigente);

        builder.HasIndex(reserva => new { reserva.PuestoId, reserva.Inicio, reserva.Fin })
            .HasDatabaseName("IX_Reservas_PuestoId_Ventana");

        builder.HasIndex(reserva => new { reserva.UsuarioId, reserva.Inicio })
            .HasDatabaseName("IX_Reservas_UsuarioId_Inicio");
    }
}
