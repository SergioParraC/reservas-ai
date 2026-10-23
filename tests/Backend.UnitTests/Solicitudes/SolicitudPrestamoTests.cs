using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.UnitTests.Solicitudes;

/// <summary>
/// Pruebas de la máquina de estados de la solicitud: radicación, revisión, aprobación,
/// rechazo con motivo y terminalidad.
/// </summary>
public sealed class SolicitudPrestamoTests
{
    private static readonly DateTimeOffset Momento = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Solicitante = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Revisor = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Espacio = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static SolicitudPrestamo Radicada() => SolicitudPrestamo.Radicar(
        Solicitante,
        Espacio,
        FranjaHoraria.Desde(
            new DateTimeOffset(2026, 11, 5, 14, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 11, 5, 16, 0, 0, TimeSpan.FromHours(-5))),
        "Bases de datos",
        "Ing. Laura Gómez",
        "Práctica de laboratorio con pre-informe aprobado por el docente.",
        "solicitudes/abc/informe.pdf",
        "informe.pdf",
        819_200,
        Momento);

    [Fact]
    public void LaRadicacionDejaLaSolicitudEnEstadoRadicado()
    {
        var solicitud = Radicada();

        Assert.Equal(EstadoSolicitud.Radicado, solicitud.Estado);
        Assert.False(solicitud.EsTerminal);
        Assert.Equal("informe.pdf", solicitud.ArchivoNombreOriginal);
        Assert.Equal(819_200, solicitud.ArchivoTamanoBytes);
    }

    [Fact]
    public void LaRevisionEsValidaDesdeRadicado()
    {
        var solicitud = Radicada();

        solicitud.TomarEnRevision(Revisor, Momento);

        Assert.Equal(EstadoSolicitud.EnRevision, solicitud.Estado);
        Assert.Equal(Revisor, solicitud.RevisorId);
        Assert.Equal(Momento, solicitud.RevisadaEn);
    }

    [Fact]
    public void AprobarDesdeRadicadoEsUnSaltoInvalido()
    {
        var solicitud = Radicada();

        var excepcion = Assert.Throws<ConflictoException>(() => solicitud.Aprobar(Revisor, Momento));

        Assert.Equal(MensajesSolicitud.TransicionInvalida, excepcion.Message);
        Assert.Equal(EstadoSolicitud.Radicado, solicitud.Estado);
    }

    [Fact]
    public void LaAprobacionEsTerminal()
    {
        var solicitud = Radicada();
        solicitud.TomarEnRevision(Revisor, Momento);
        solicitud.Aprobar(Revisor, Momento);

        Assert.True(solicitud.EsTerminal);

        var excepcion = Assert.Throws<ConflictoException>(() =>
            solicitud.Rechazar(Revisor, "Motivo suficientemente largo", 10, Momento));

        Assert.Equal(MensajesSolicitud.TransicionInvalida, excepcion.Message);
        Assert.Equal(EstadoSolicitud.Aprobado, solicitud.Estado);
    }

    [Fact]
    public void ElRechazoExigeMotivo()
    {
        var solicitud = Radicada();
        solicitud.TomarEnRevision(Revisor, Momento);

        var excepcion = Assert.Throws<DomainException>(() =>
            solicitud.Rechazar(Revisor, "   ", 10, Momento));

        Assert.Equal(MensajesSolicitud.MotivoRequerido, excepcion.Message);
        Assert.Equal(EstadoSolicitud.EnRevision, solicitud.Estado);
    }

    [Fact]
    public void ElRechazoExigeLaLongitudMinimaDelMotivo()
    {
        var solicitud = Radicada();
        solicitud.TomarEnRevision(Revisor, Momento);

        var excepcion = Assert.Throws<DomainException>(() =>
            solicitud.Rechazar(Revisor, "corto", 10, Momento));

        Assert.Contains("10", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ElRechazoConMotivoSuficienteSeRegistra()
    {
        var solicitud = Radicada();
        solicitud.TomarEnRevision(Revisor, Momento);

        solicitud.Rechazar(Revisor, "La franja se cruza con una práctica programada.", 10, Momento);

        Assert.Equal(EstadoSolicitud.Rechazado, solicitud.Estado);
        Assert.Contains("práctica programada", solicitud.MotivoRechazo, StringComparison.Ordinal);
        Assert.True(solicitud.EsTerminal);
    }

    [Fact]
    public void ElSolicitanteNoPuedeDecidirSuPropiaSolicitud()
    {
        var solicitud = Radicada();

        Assert.Throws<ConflictoException>(() => solicitud.TomarEnRevision(Solicitante, Momento));
    }
}
