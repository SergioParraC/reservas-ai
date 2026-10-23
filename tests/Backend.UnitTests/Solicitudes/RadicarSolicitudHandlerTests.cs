using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Application.Modules.Solicitudes;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Moq;

namespace Backend.UnitTests.Solicitudes;

/// <summary>
/// Pruebas de la radicación: alta válida, ausencia de archivo, archivo sobredimensionado,
/// tipo declarado falso y área con modalidad incompatible.
/// </summary>
public sealed class RadicarSolicitudHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid EspacioId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Solicitante = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private static OpcionesSolicitudes Solicitudes() => new()
    {
        TamanoMaximoPdfBytes = 5 * 1024 * 1024,
        MaxSolicitudesActivasPorUsuario = 3,
        LongitudMinimaMotivoRechazo = 10,
        AntelacionMinimaMinutos = 30
    };

    private static OpcionesReservas Reservas() => new()
    {
        AntelacionMinimaMinutos = 30,
        AntelacionMaximaDias = 14,
        DuracionMinimaMinutos = 30,
        DuracionMaximaMinutos = 240,
        IntervaloMinutos = 15,
        AntelacionCancelacionMinutos = 30,
        MaxReservasActivasPorUsuario = 2,
        HoraApertura = "07:00",
        HoraCierre = "21:00",
        ZonaHoraria = "America/Bogota"
    };

    private static EspacioParaReserva Espacio(TipoModalidadPrestamo modalidad = TipoModalidadPrestamo.AreaCompleta) =>
        new(
            EspacioId,
            "Laboratorio de Redes",
            EstadoUnidad.Activo,
            TipoAprobacion.Docente,
            modalidad,
            TipoElementoPrestamo.LaboratorioEspecializado);

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

    private static ComandoRadicarSolicitud Comando(
        string tipoContenido = "application/pdf",
        long longitud = 819_200,
        bool conArchivo = true) => new(
        EspacioId,
        new DateTimeOffset(2026, 11, 5, 14, 0, 0, TimeSpan.FromHours(-5)),
        new DateTimeOffset(2026, 11, 5, 16, 0, 0, TimeSpan.FromHours(-5)),
        "Bases de datos",
        "Ing. Laura Gómez",
        "Práctica de laboratorio con pre-informe.",
        conArchivo ? new MemoryStream([1, 2, 3]) : null,
        conArchivo ? "informe.pdf" : null,
        tipoContenido,
        longitud);

    private static RadicarSolicitudHandler Handler(
        EspacioParaReserva? espacio,
        Mock<IAlmacenDocumentos>? documentos = null,
        Mock<ISolicitudesRepository>? repositorio = null,
        Mock<IConsultaSolicitudes>? consulta = null)
    {
        var espacios = new Mock<IConsultaEspacios>();
        espacios.Setup(servicio => servicio.ObtenerParaReservaAsync(EspacioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(espacio);

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
        usuario.SetupGet(servicio => servicio.UsuarioId).Returns(Solicitante);

        documentos ??= new Mock<IAlmacenDocumentos>();
        documentos.Setup(servicio => servicio.GuardarPdfAsync(
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Resultado<string>.Exito("solicitudes/abc/informe.pdf"));

        repositorio ??= new Mock<ISolicitudesRepository>();
        repositorio.Setup(servicio => servicio.AgregarAsync(It.IsAny<SolicitudPrestamo>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repositorio.Setup(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        consulta ??= new Mock<IConsultaSolicitudes>();
        consulta.Setup(servicio => servicio.ContarActivasAsync(Solicitante, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        return new RadicarSolicitudHandler(
            espacios.Object,
            calendario.Object,
            consulta.Object,
            repositorio.Object,
            documentos.Object,
            Solicitudes(),
            Reservas(),
            reloj.Object,
            usuario.Object);
    }

    [Fact]
    public async Task LaRadicacionValidaDevuelveElEstadoRadicado()
    {
        var resultado = await Handler(Espacio()).HandleAsync(Comando());

        Assert.True(resultado.Exitoso);
        Assert.NotNull(resultado.Valor);
        Assert.Equal(EstadoSolicitud.Radicado, resultado.Valor.Estado);
        Assert.Equal("informe.pdf", resultado.Valor.ArchivoNombreOriginal);
    }

    [Fact]
    public async Task LaRadicacionSinArchivoDevuelve400()
    {
        var resultado = await Handler(Espacio()).HandleAsync(Comando(conArchivo: false));

        Assert.Equal(TipoFallo.Validacion, resultado.TipoFallo);
        Assert.True(resultado.Errores.ContainsKey("archivo"));
    }

    [Fact]
    public async Task ElArchivoSobredimensionadoDevuelve413()
    {
        var resultado = await Handler(Espacio()).HandleAsync(Comando(longitud: 6 * 1024 * 1024));

        Assert.Equal(TipoFallo.ArchivoDemasiadoGrande, resultado.TipoFallo);
    }

    [Fact]
    public async Task ElTipoDeclaradoFalsoDevuelve415()
    {
        var resultado = await Handler(Espacio()).HandleAsync(Comando(tipoContenido: "text/plain"));

        Assert.Equal(TipoFallo.TipoNoSoportado, resultado.TipoFallo);
    }

    [Fact]
    public async Task ElAreaPorPuestoIndividualDevuelve409DeModalidad()
    {
        var resultado = await Handler(Espacio(TipoModalidadPrestamo.PuestoIndividual))
            .HandleAsync(Comando());

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesSolicitud.ModalidadIncompatible, resultado.Mensaje);
    }

    [Fact]
    public async Task ElAreaInexistenteDevuelve404()
    {
        var resultado = await Handler(null).HandleAsync(Comando());

        Assert.Equal(TipoFallo.NoEncontrado, resultado.TipoFallo);
    }
}
