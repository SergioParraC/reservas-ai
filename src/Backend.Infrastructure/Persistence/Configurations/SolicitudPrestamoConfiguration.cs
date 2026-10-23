using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API de las solicitudes de préstamo: franja, solicitante y revisor,
/// metadatos del pre-informe (nunca el binario) y token de concurrencia optimista.
/// </summary>
public sealed class SolicitudPrestamoConfiguration : IEntityTypeConfiguration<SolicitudPrestamo>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SolicitudPrestamo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SolicitudesPrestamo");
        builder.HasKey(solicitud => solicitud.Id);
        builder.Property(solicitud => solicitud.Id).ValueGeneratedNever();

        builder.Property(solicitud => solicitud.SolicitanteId).IsRequired();
        builder.Property(solicitud => solicitud.EspacioId).IsRequired();
        builder.Property(solicitud => solicitud.Inicio).IsRequired();
        builder.Property(solicitud => solicitud.Fin).IsRequired();

        builder.Property(solicitud => solicitud.Materia)
            .HasMaxLength(SolicitudPrestamo.LongitudMaximaMateria)
            .IsRequired();

        builder.Property(solicitud => solicitud.DocenteResponsable)
            .HasMaxLength(SolicitudPrestamo.LongitudMaximaDocente)
            .IsRequired();

        builder.Property(solicitud => solicitud.Justificacion)
            .HasMaxLength(SolicitudPrestamo.LongitudMaximaJustificacion)
            .IsRequired();

        builder.Property(solicitud => solicitud.ArchivoRuta)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(solicitud => solicitud.ArchivoNombreOriginal)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(solicitud => solicitud.ArchivoTamanoBytes).IsRequired();

        builder.Property(solicitud => solicitud.Estado)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(solicitud => solicitud.RadicadaEn).IsRequired();
        builder.Property(solicitud => solicitud.RevisadaEn);

        builder.Property(solicitud => solicitud.MotivoRechazo)
            .HasMaxLength(SolicitudPrestamo.LongitudMaximaMotivoRechazo);

        builder.Property(solicitud => solicitud.RowVersion).IsRowVersion();

        builder.Ignore(solicitud => solicitud.Franja);
        builder.Ignore(solicitud => solicitud.EsTerminal);

        builder.HasOne<Espacio>()
            .WithMany()
            .HasForeignKey(solicitud => solicitud.EspacioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_SolicitudesPrestamo_Espacios");

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(solicitud => solicitud.SolicitanteId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_SolicitudesPrestamo_Solicitante");

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(solicitud => solicitud.RevisorId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_SolicitudesPrestamo_Revisor");

        builder.HasIndex(solicitud => new { solicitud.SolicitanteId, solicitud.RadicadaEn })
            .HasDatabaseName("IX_SolicitudesPrestamo_SolicitanteId_RadicadaEn");

        builder.HasIndex(solicitud => new { solicitud.Estado, solicitud.RadicadaEn })
            .HasDatabaseName("IX_SolicitudesPrestamo_Estado_RadicadaEn");

        builder.HasIndex(solicitud => new { solicitud.EspacioId, solicitud.Inicio, solicitud.Fin })
            .HasDatabaseName("IX_SolicitudesPrestamo_EspacioId_Ventana");
    }
}

/// <summary>
/// Configuración Fluent API de la cola de salida de correos: cuerpo HTML, estado del despacho
/// e índice del próximo intento para el barrido del despachador.
/// </summary>
public sealed class CorreoPendienteConfiguration : IEntityTypeConfiguration<CorreoPendiente>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CorreoPendiente> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CorreosPendientes");
        builder.HasKey(correo => correo.Id);
        builder.Property(correo => correo.Id).ValueGeneratedNever();

        builder.Property(correo => correo.Destinatario)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(correo => correo.Asunto)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(correo => correo.CuerpoHtml)
            .IsRequired();

        builder.Property(correo => correo.ReferenciaId).IsRequired();

        builder.Property(correo => correo.Estado)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(correo => correo.Intentos).IsRequired();
        builder.Property(correo => correo.ProximoIntentoEn).IsRequired();
        builder.Property(correo => correo.CreadoEn).IsRequired();
        builder.Property(correo => correo.EnviadoEn);

        builder.Property(correo => correo.UltimoError)
            .HasMaxLength(500);

        builder.Ignore(correo => correo.EstaPendiente);

        builder.HasIndex(correo => new { correo.Estado, correo.ProximoIntentoEn })
            .HasDatabaseName("IX_CorreosPendientes_Estado_ProximoIntento");

        builder.HasIndex(correo => correo.ReferenciaId)
            .HasDatabaseName("IX_CorreosPendientes_ReferenciaId");
    }
}
