using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API del agregado de áreas: longitudes, enums como <c>int</c>,
/// conversión del código QR, token de concurrencia optimista e índices únicos.
/// </summary>
public sealed class EspacioConfiguration : IEntityTypeConfiguration<Espacio>
{
    /// <summary>Longitud de la columna del código QR.</summary>
    public const int LongitudCodigoQr = 29;

    /// <summary>Longitud máxima de la ruta relativa de la fotografía.</summary>
    public const int LongitudMaximaFotografiaRuta = 300;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Espacio> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Espacios");
        builder.HasKey(espacio => espacio.Id);
        builder.Property(espacio => espacio.Id).ValueGeneratedNever();

        builder.Property(espacio => espacio.Nombre)
            .HasMaxLength(Espacio.LongitudMaximaNombre)
            .IsRequired();

        builder.Property(espacio => espacio.Ubicacion)
            .HasMaxLength(Espacio.LongitudMaximaUbicacion)
            .IsRequired();

        builder.Property(espacio => espacio.Categoria)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(espacio => espacio.TipoAprobacion)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(espacio => espacio.TipoModalidadPrestamo)
            .HasConversion<int>()
            .HasDefaultValue(TipoModalidadPrestamo.AreaCompleta)
            .IsRequired();

        builder.Property(espacio => espacio.TipoElementoPrestamo)
            .HasConversion<int>()
            .HasDefaultValue(TipoElementoPrestamo.LaboratorioEspecializado)
            .IsRequired();

        builder.Property(espacio => espacio.Estado)
            .HasConversion<int>()
            .IsRequired();

        // Conversión de una sola vía: el comando nunca reasigna el código QR.
        builder.Property(espacio => espacio.CodigoQR)
            .HasConversion(codigo => codigo.Valor, valor => CodigoQR.Desde(valor))
            .HasMaxLength(LongitudCodigoQr)
            .IsRequired();

        builder.Property(espacio => espacio.FotografiaRuta)
            .HasMaxLength(LongitudMaximaFotografiaRuta);

        builder.Property(espacio => espacio.CreadoEn).IsRequired();
        builder.Property(espacio => espacio.ActualizadoEn).IsRequired();
        builder.Property(espacio => espacio.RowVersion).IsRowVersion();

        builder.HasMany(espacio => espacio.Puestos)
            .WithOne()
            .HasForeignKey(puesto => puesto.EspacioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(espacio => espacio.Puestos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(espacio => espacio.CodigoQR)
            .IsUnique()
            .HasDatabaseName("IX_Espacios_CodigoQR");

        builder.HasIndex(espacio => new { espacio.Nombre, espacio.Ubicacion })
            .IsUnique()
            .HasDatabaseName("IX_Espacios_Nombre_Ubicacion");

        builder.HasIndex(espacio => new { espacio.Estado, espacio.Categoria, espacio.Aforo })
            .HasDatabaseName("IX_Espacios_Estado_Categoria_Aforo");
    }
}

/// <summary>Configuración Fluent API de los puestos de cómputo.</summary>
public sealed class PuestoConfiguration : IEntityTypeConfiguration<Puesto>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Puesto> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Puestos");
        builder.HasKey(puesto => puesto.Id);
        builder.Property(puesto => puesto.Id).ValueGeneratedNever();

        builder.Property(puesto => puesto.Identificador)
            .HasMaxLength(Espacio.LongitudMaximaIdentificadorPuesto)
            .IsRequired();

        builder.Property(puesto => puesto.DescripcionEquipo)
            .HasMaxLength(Espacio.LongitudMaximaDescripcionEquipo);

        builder.Property(puesto => puesto.CodigoQR)
            .HasConversion(codigo => codigo.Valor, valor => CodigoQR.Desde(valor))
            .HasMaxLength(EspacioConfiguration.LongitudCodigoQr)
            .IsRequired();

        builder.Property(puesto => puesto.Estado)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(puesto => puesto.CreadoEn).IsRequired();

        builder.HasIndex(puesto => puesto.CodigoQR)
            .IsUnique()
            .HasDatabaseName("IX_Puestos_CodigoQR");

        builder.HasIndex(puesto => new { puesto.EspacioId, puesto.Identificador })
            .IsUnique()
            .HasDatabaseName("IX_Puestos_EspacioId_Identificador");
    }
}
