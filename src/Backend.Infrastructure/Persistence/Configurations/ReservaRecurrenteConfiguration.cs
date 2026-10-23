using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API de las series semestrales: patrón persistido, periodo declarado,
/// referencias al área y al docente, y token de concurrencia optimista.
/// </summary>
public sealed class ReservaRecurrenteConfiguration : IEntityTypeConfiguration<ReservaRecurrente>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReservaRecurrente> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReservasRecurrentes");
        builder.HasKey(serie => serie.Id);
        builder.Property(serie => serie.Id).ValueGeneratedNever();

        builder.Property(serie => serie.EspacioId).IsRequired();
        builder.Property(serie => serie.DocenteId).IsRequired();

        builder.Property(serie => serie.TipoRecurrencia)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(serie => serie.DiaSemana)
            .HasConversion<int?>();

        builder.Property(serie => serie.FechasFijas)
            .HasMaxLength(1000);

        builder.Property(serie => serie.HoraInicio)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(serie => serie.HoraFin)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(serie => serie.PeriodoInicio)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(serie => serie.PeriodoFin)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(serie => serie.Materia)
            .HasMaxLength(ReservaRecurrente.LongitudMaximaMateria)
            .IsRequired();

        builder.Property(serie => serie.CreadaEn).IsRequired();
        builder.Property(serie => serie.RowVersion).IsRowVersion();

        builder.Ignore(serie => serie.Periodo);
        builder.Ignore(serie => serie.Patron);

        builder.HasOne<Espacio>()
            .WithMany()
            .HasForeignKey(serie => serie.EspacioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ReservasRecurrentes_Espacios");

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(serie => serie.DocenteId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_ReservasRecurrentes_AspNetUsers");

        builder.HasIndex(serie => new { serie.DocenteId, serie.CreadaEn })
            .HasDatabaseName("IX_ReservasRecurrentes_DocenteId_CreadaEn");

        builder.HasIndex(serie => new { serie.EspacioId, serie.PeriodoInicio })
            .HasDatabaseName("IX_ReservasRecurrentes_EspacioId_Periodo");
    }
}

/// <summary>
/// Configuración Fluent API de la agenda del área: índice único filtrado que impide dos
/// ocupaciones activas con el mismo inicio y estado en la misma área.
/// </summary>
public sealed class ReservaEspacioConfiguration : IEntityTypeConfiguration<ReservaEspacio>
{
    /// <summary>Nombre del índice único de ocupaciones activas por área e inicio.</summary>
    public const string IndiceUnicoActiva = "IX_ReservasEspacio_EspacioId_Inicio_Activa";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ReservaEspacio> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReservasEspacio");
        builder.HasKey(ocupacion => ocupacion.Id);
        builder.Property(ocupacion => ocupacion.Id).ValueGeneratedNever();

        builder.Property(ocupacion => ocupacion.EspacioId).IsRequired();
        builder.Property(ocupacion => ocupacion.TitularId).IsRequired();

        builder.Property(ocupacion => ocupacion.Origen)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(ocupacion => ocupacion.OrigenId).IsRequired();
        builder.Property(ocupacion => ocupacion.Inicio).IsRequired();
        builder.Property(ocupacion => ocupacion.Fin).IsRequired();

        builder.Property(ocupacion => ocupacion.Estado)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(ocupacion => ocupacion.CreadaEn).IsRequired();
        builder.Property(ocupacion => ocupacion.RowVersion).IsRowVersion();

        builder.Ignore(ocupacion => ocupacion.Franja);
        builder.Ignore(ocupacion => ocupacion.EstaActiva);

        builder.HasOne<Espacio>()
            .WithMany()
            .HasForeignKey(ocupacion => ocupacion.EspacioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ReservasEspacio_Espacios");

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(ocupacion => ocupacion.TitularId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_ReservasEspacio_AspNetUsers");

        builder.HasIndex(ocupacion => new { ocupacion.EspacioId, ocupacion.Inicio })
            .IsUnique()
            .HasFilter("[Estado] = 1")
            .HasDatabaseName(IndiceUnicoActiva);

        builder.HasIndex(ocupacion => new { ocupacion.EspacioId, ocupacion.Inicio, ocupacion.Fin })
            .HasDatabaseName("IX_ReservasEspacio_EspacioId_Ventana");

        builder.HasIndex(ocupacion => new { ocupacion.Origen, ocupacion.OrigenId })
            .HasDatabaseName("IX_ReservasEspacio_Origen");
    }
}
