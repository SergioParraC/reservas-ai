using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Application.Modules.Reservas;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Moq;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas de la disponibilidad de un área completa: franjas con la ocupación de la agenda,
/// área inexistente, modalidad incompatible y fecha fuera del horizonte.
/// </summary>
public sealed class ConsultarDisponibilidadEspacioHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid EspacioId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static OpcionesReservas Opciones() => new()
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

    private static EspacioParaReserva Area(
        TipoModalidadPrestamo modalidad = TipoModalidadPrestamo.AreaCompleta) =>
        new(
            EspacioId,
            "Laboratorio de Redes",
            EstadoUnidad.Activo,
            TipoAprobacion.Docente,
            modalidad,
            TipoElementoPrestamo.LaboratorioEspecializado);

    private static Mock<IConsultaEspacios> EspaciosCon(EspacioParaReserva? espacio)
    {
        var espacios = new Mock<IConsultaEspacios>();
        espacios.Setup(servicio => servicio.ObtenerParaReservaAsync(EspacioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(espacio);
        return espacios;
    }

    private static Mock<IConsultaOcupacionEspacio> OcupacionCon(params IntervaloOcupacion[] intervalos)
    {
        var ocupacion = new Mock<IConsultaOcupacionEspacio>();
        ocupacion.Setup(servicio => servicio.ObtenerAsync(
                EspacioId,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(intervalos);
        return ocupacion;
    }

    private static Mock<IConsultaCalendario> CalendarioAbierto()
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        var periodo = PeriodoAcademico.Crear("2026-2", new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 15), Ahora);
        periodo.Activar(Ahora);

        var calendario = CalendarioInstitucional.Crear(
            periodo,
            [],
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 12, 1),
            zona);

        var puerto = new Mock<IConsultaCalendario>();
        puerto.Setup(servicio => servicio.ObtenerCalendarioAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendario);

        return puerto;
    }

    private static ConsultarDisponibilidadEspacioHandler Handler(
        Mock<IConsultaEspacios> espacios,
        Mock<IConsultaOcupacionEspacio> ocupacion)
    {
        var reloj = new Mock<IRelojSistema>();
        reloj.SetupGet(servicio => servicio.Ahora).Returns(Ahora);

        return new ConsultarDisponibilidadEspacioHandler(
            espacios.Object,
            ocupacion.Object,
            CalendarioAbierto().Object,
            Opciones(),
            reloj.Object);
    }

    private static FiltroDisponibilidad Filtro(int dia, int? duracionMinutos = 60) =>
        new(new DateOnly(2026, 11, dia), null, null, duracionMinutos);

    private static DateTimeOffset EnZona(int dia, int hora) =>
        new(2026, 11, dia, hora, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public async Task DevuelveLasFranjasDelAreaConLaOcupacionMarcada()
    {
        var ocupacion = OcupacionCon(new IntervaloOcupacion(EnZona(3, 10), EnZona(3, 11)));

        var resultado = await Handler(EspaciosCon(Area()), ocupacion).HandleAsync(EspacioId, Filtro(3));

        Assert.True(resultado.Exitoso);
        Assert.NotNull(resultado.Valor);
        Assert.Equal("Laboratorio de Redes", resultado.Valor.EspacioNombre);
        Assert.Contains(resultado.Valor.Franjas, franja => franja.Disponible);
        Assert.Contains(
            resultado.Valor.Franjas,
            franja => !franja.Disponible && franja.Motivo == nameof(MotivoNoDisponible.Ocupada));
    }

    [Fact]
    public async Task UnAreaInexistenteDevuelve404()
    {
        var resultado = await Handler(EspaciosCon(null), OcupacionCon()).HandleAsync(EspacioId, Filtro(3));

        Assert.Equal(TipoFallo.NoEncontrado, resultado.TipoFallo);
    }

    [Fact]
    public async Task UnAreaDePuestoIndividualDevuelve409()
    {
        var resultado = await Handler(
                EspaciosCon(Area(TipoModalidadPrestamo.PuestoIndividual)),
                OcupacionCon())
            .HandleAsync(EspacioId, Filtro(3));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesEspacio.AreaNoReservable, resultado.Mensaje);
    }

    [Fact]
    public async Task UnaFechaFueraDelHorizonteDevuelve400()
    {
        var resultado = await Handler(EspaciosCon(Area()), OcupacionCon()).HandleAsync(EspacioId, Filtro(28));

        Assert.Equal(TipoFallo.Validacion, resultado.TipoFallo);
    }
}
