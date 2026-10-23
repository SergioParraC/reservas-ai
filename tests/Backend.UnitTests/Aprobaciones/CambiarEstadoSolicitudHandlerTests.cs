using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Aprobaciones;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;
using Moq;

namespace Backend.UnitTests.Aprobaciones;

/// <summary>
/// Pruebas del cambio de estado: transición válida, salto de estado, rechazo sin motivo,
/// decisión propia y aprobación que ocupa la agenda con el correo encolado.
/// </summary>
public sealed class CambiarEstadoSolicitudHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Solicitante = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Revisor = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid EspacioId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private static SolicitudDetalle Detalle(EstadoSolicitud estado = EstadoSolicitud.Radicado) => new(
        Guid.NewGuid(),
        EspacioId,
        "Laboratorio de Redes",
        TipoAprobacion.Docente,
        TipoModalidadPrestamo.AreaCompleta,
        Solicitante,
        "Camila Restrepo",
        "camila@uninpahu.edu.co",
        new DateTimeOffset(2026, 11, 5, 14, 0, 0, TimeSpan.FromHours(-5)),
        new DateTimeOffset(2026, 11, 5, 16, 0, 0, TimeSpan.FromHours(-5)),
        "Bases de datos",
        "Ing. Laura Gómez",
        "Práctica de laboratorio.",
        estado,
        Ahora,
        null,
        null,
        null,
        "informe.pdf",
        819_200,
        "solicitudes/abc/informe.pdf");

    private static SolicitudPrestamo Solicitud(EstadoSolicitud estado = EstadoSolicitud.Radicado)
    {
        var solicitud = SolicitudPrestamo.Radicar(
            Solicitante,
            EspacioId,
            FranjaHoraria.Desde(
                new DateTimeOffset(2026, 11, 5, 14, 0, 0, TimeSpan.FromHours(-5)),
                new DateTimeOffset(2026, 11, 5, 16, 0, 0, TimeSpan.FromHours(-5))),
            "Bases de datos",
            "Ing. Laura Gómez",
            "Práctica de laboratorio.",
            "solicitudes/abc/informe.pdf",
            "informe.pdf",
            819_200,
            Ahora);

        if (estado is EstadoSolicitud.EnRevision or EstadoSolicitud.Aprobado or EstadoSolicitud.Rechazado)
        {
            solicitud.TomarEnRevision(Revisor, Ahora);
        }

        if (estado == EstadoSolicitud.Aprobado)
        {
            solicitud.Aprobar(Revisor, Ahora);
        }

        return solicitud;
    }

    private static CalendarioInstitucional Calendario()
    {
        var periodo = PeriodoAcademico.Crear("2026-2", new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 15), Ahora);
        periodo.Activar(Ahora);

        return CalendarioInstitucional.Crear(
            periodo,
            [],
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30),
            Zona);
    }

    private static CambiarEstadoSolicitudHandler Handler(
        SolicitudDetalle detalle,
        SolicitudPrestamo solicitud,
        out Mock<ISolicitudesRepository> repositorio,
        IReadOnlyCollection<string>? roles = null,
        Guid? usuarioId = null)
    {
        var consulta = new Mock<IConsultaSolicitudes>();
        consulta.Setup(servicio => servicio.ObtenerAsync(detalle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detalle);

        repositorio = new Mock<ISolicitudesRepository>();
        repositorio.Setup(servicio => servicio.ObtenerAsync(detalle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitud);
        repositorio.Setup(servicio => servicio.GuardarDecisionAsync(
                It.IsAny<SolicitudPrestamo>(),
                It.IsAny<ReservaEspacio?>(),
                It.IsAny<CorreoPendiente?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoDecisionSolicitud.Guardada);

        var calendario = new Mock<IConsultaCalendario>();
        calendario.Setup(servicio => servicio.ObtenerCalendarioAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Calendario());

        var reloj = new Mock<IRelojSistema>();
        reloj.SetupGet(servicio => servicio.Ahora).Returns(Ahora);

        var usuario = new Mock<IUsuarioActual>();
        usuario.SetupGet(servicio => servicio.UsuarioId).Returns(usuarioId ?? Revisor);
        usuario.SetupGet(servicio => servicio.Roles).Returns(roles ?? ["Docente"]);

        return new CambiarEstadoSolicitudHandler(
            consulta.Object,
            repositorio.Object,
            calendario.Object,
            new OpcionesSolicitudes { LongitudMinimaMotivoRechazo = 10 },
            new OpcionesReservas { ZonaHoraria = "America/Bogota" },
            reloj.Object,
            usuario.Object);
    }

    [Fact]
    public async Task LaRevisionValidaDevuelveElNuevoEstado()
    {
        var detalle = Detalle();
        var handler = Handler(detalle, Solicitud(), out _, usuarioId: Revisor);

        var resultado = await handler.HandleAsync(new ComandoCambiarEstado(detalle.Id, EstadoSolicitud.EnRevision, null));

        Assert.True(resultado.Exitoso);
        Assert.Equal(EstadoSolicitud.EnRevision, resultado.Valor?.Estado);
    }

    [Fact]
    public async Task ElSaltoDeRadicadoAAprobadoDevuelve409()
    {
        var detalle = Detalle();
        var handler = Handler(detalle, Solicitud(), out _, usuarioId: Revisor);

        var resultado = await handler.HandleAsync(new ComandoCambiarEstado(detalle.Id, EstadoSolicitud.Aprobado, null));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesSolicitud.TransicionInvalida, resultado.Mensaje);
    }

    [Fact]
    public async Task ElRechazoSinMotivoDevuelve400ConElCampoMotivo()
    {
        var detalle = Detalle(EstadoSolicitud.EnRevision);
        var handler = Handler(detalle, Solicitud(EstadoSolicitud.EnRevision), out _, usuarioId: Revisor);

        var resultado = await handler.HandleAsync(new ComandoCambiarEstado(detalle.Id, EstadoSolicitud.Rechazado, null));

        Assert.Equal(TipoFallo.Validacion, resultado.TipoFallo);
        Assert.True(resultado.Errores.ContainsKey("motivo"));
    }

    [Fact]
    public async Task ElSolicitanteNoPuedeDecidirSuPropiaSolicitud()
    {
        var detalle = Detalle(EstadoSolicitud.EnRevision);
        var handler = Handler(detalle, Solicitud(EstadoSolicitud.EnRevision), out _, usuarioId: Solicitante);

        var resultado = await handler.HandleAsync(new ComandoCambiarEstado(detalle.Id, EstadoSolicitud.Aprobado, null));

        Assert.Equal(TipoFallo.NoPermitido, resultado.TipoFallo);
    }

    [Fact]
    public async Task LaAprobacionConfirmaLaOcupacionYElCorreo()
    {
        var detalle = Detalle(EstadoSolicitud.EnRevision);
        var handler = Handler(
            detalle,
            Solicitud(EstadoSolicitud.EnRevision),
            out var repositorio,
            usuarioId: Revisor);

        var resultado = await handler.HandleAsync(new ComandoCambiarEstado(detalle.Id, EstadoSolicitud.Aprobado, null));

        Assert.True(resultado.Exitoso);
        Assert.Equal(EstadoSolicitud.Aprobado, resultado.Valor?.Estado);

        repositorio.Verify(servicio => servicio.GuardarDecisionAsync(
            It.IsAny<SolicitudPrestamo>(),
            It.Is<ReservaEspacio?>(ocupacion => ocupacion != null),
            It.Is<CorreoPendiente?>(correo => correo != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ElRevisorSinCompetenciaRecibe403()
    {
        var detalle = Detalle(EstadoSolicitud.EnRevision);
        var handler = Handler(
            detalle,
            Solicitud(EstadoSolicitud.EnRevision),
            out _,
            roles: ["Estudiante"],
            usuarioId: Revisor);

        var resultado = await handler.HandleAsync(new ComandoCambiarEstado(detalle.Id, EstadoSolicitud.Aprobado, null));

        Assert.Equal(TipoFallo.NoPermitido, resultado.TipoFallo);
    }
}
