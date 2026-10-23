using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API del calendario institucional: rangos de fechas, horas locales,
/// token de concurrencia y el índice único filtrado que garantiza un solo periodo activo.
/// </summary>
public sealed class PeriodoAcademicoConfiguration : IEntityTypeConfiguration<PeriodoAcademico>
{
    /// <summary>Nombre del índice único del periodo vigente.</summary>
    public const string IndicePeriodoActivo = "IX_PeriodosAcademicos_Activo";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PeriodoAcademico> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PeriodosAcademicos");
        builder.HasKey(periodo => periodo.Id);
        builder.Property(periodo => periodo.Id).ValueGeneratedNever();

        builder.Property(periodo => periodo.Nombre)
            .HasMaxLength(PeriodoAcademico.LongitudMaximaNombre)
            .IsRequired();

        builder.Property(periodo => periodo.FechaInicio)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(periodo => periodo.FechaFin)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(periodo => periodo.EstadoActivo).IsRequired();
        builder.Property(periodo => periodo.CreadoEn).IsRequired();
        builder.Property(periodo => periodo.ActualizadoEn).IsRequired();
        builder.Property(periodo => periodo.RowVersion).IsRowVersion();

        builder.Ignore(periodo => periodo.Dias);

        builder.HasIndex(periodo => periodo.EstadoActivo)
            .IsUnique()
            .HasFilter("[EstadoActivo] = 1")
            .HasDatabaseName(IndicePeriodoActivo);

        builder.HasIndex(periodo => new { periodo.FechaInicio, periodo.FechaFin })
            .HasDatabaseName("IX_PeriodosAcademicos_Rango");
    }
}

/// <summary>
/// Configuración Fluent API de las franjas no operativas: criterio excluyente, horas locales,
/// baja lógica e índice de consulta por vigencia.
/// </summary>
public sealed class FranjaNoOperativaConfiguration : IEntityTypeConfiguration<FranjaNoOperativa>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FranjaNoOperativa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("FranjasNoOperativas", tabla => tabla.HasCheckConstraint(
            "CK_FranjasNoOperativas_Criterio",
            "([DiaSemana] IS NOT NULL AND [FechaEspecifica] IS NULL) OR ([DiaSemana] IS NULL AND [FechaEspecifica] IS NOT NULL)"));

        builder.HasKey(franja => franja.Id);
        builder.Property(franja => franja.Id).ValueGeneratedNever();

        builder.Property(franja => franja.DiaSemana)
            .HasConversion<int?>();

        builder.Property(franja => franja.FechaEspecifica)
            .HasColumnType("date");

        builder.Property(franja => franja.HoraInicio)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(franja => franja.HoraFin)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(franja => franja.EsFeriado).IsRequired();

        builder.Property(franja => franja.Motivo)
            .HasMaxLength(FranjaNoOperativa.LongitudMaximaMotivo)
            .IsRequired();

        builder.Property(franja => franja.Activa).IsRequired();
        builder.Property(franja => franja.CreadoEn).IsRequired();
        builder.Property(franja => franja.RowVersion).IsRowVersion();

        builder.Ignore(franja => franja.EsDiaCompleto);

        builder.HasIndex(franja => new { franja.Activa, franja.DiaSemana, franja.FechaEspecifica })
            .HasDatabaseName("IX_FranjasNoOperativas_Vigencia");
    }
}
