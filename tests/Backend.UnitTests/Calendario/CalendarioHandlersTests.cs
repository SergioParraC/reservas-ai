using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Calendario;
using Backend.Application.Modules.Calendario.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Exceptions;
using Moq;

namespace Backend.UnitTests.Calendario;

/// <summary>
/// Pruebas de los casos de uso de periodos y franjas: solape, ocupación fuera de la nueva
/// ventana y duplicado de franjas.
/// </summary>
public sealed class CalendarioHandlersTests
{
    private static readonly DateTimeOffset Momento = new(2026, 7, 1, 8, 0, 0, TimeSpan.FromHours(-5));

    private static PeriodoAcademico Periodo() =>
        PeriodoAcademico.Crear("2026-2", new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 15), Momento);

    private static Mock<IPeriodosAcademicosRepository> RepositorioPeriodos(PeriodoAcademico periodo, bool solapa, bool ocupacion)
    {
        var repositorio = new Mock<IPeriodosAcademicosRepository>();
        repositorio.Setup(servicio => servicio.ObtenerAsync(periodo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(periodo);
        repositorio.Setup(servicio => servicio.ExisteSolapeAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(solapa);
        repositorio.Setup(servicio => servicio.ExisteOcupacionFueraDeAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocupacion);
        repositorio.Setup(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return repositorio;
    }

    private static ActualizarPeriodoHandler Handler(
        PeriodoAcademico periodo,
        bool solapa,
        bool ocupacion) => new(
        RepositorioPeriodos(periodo, solapa, ocupacion).Object,
        Mock.Of<IConsultaCalendario>(),
        new OpcionesReservas { ZonaHoraria = "America/Bogota" },
        Mock.Of<IRelojSistema>(reloj => reloj.Ahora == Momento));

    [Fact]
    public async Task ElPeriodoSolapadoDevuelve409()
    {
        var periodo = Periodo();

        var resultado = await Handler(periodo, solapa: true, ocupacion: false).HandleAsync(
            periodo.Id,
            new ComandoActualizarPeriodo("2026-2", new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 15), null));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesCalendario.PeriodoSolapado, resultado.Mensaje);
    }

    [Fact]
    public async Task ElPeriodoConOcupacionFueraDeLaVentanaDevuelve409()
    {
        var periodo = Periodo();

        var resultado = await Handler(periodo, solapa: false, ocupacion: true).HandleAsync(
            periodo.Id,
            new ComandoActualizarPeriodo("2026-2", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 15), null));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesCalendario.PeriodoConOcupacion, resultado.Mensaje);
    }

    [Fact]
    public async Task LaFranjaDuplicadaDevuelve409()
    {
        var repositorio = new Mock<IFranjasNoOperativasRepository>();
        repositorio.Setup(servicio => servicio.ExisteDuplicadaAsync(
                It.IsAny<DayOfWeek?>(), It.IsAny<DateOnly?>(), It.IsAny<TimeOnly>(),
                It.IsAny<TimeOnly>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var reloj = new Mock<IRelojSistema>();
        reloj.SetupGet(servicio => servicio.Ahora).Returns(Momento);

        var handler = new CrearFranjaHandler(repositorio.Object, Mock.Of<IConsultaCalendario>(), reloj.Object);

        var resultado = await handler.HandleAsync(new ComandoCrearFranjaNoOperativa(
            DayOfWeek.Sunday,
            null,
            new TimeOnly(0, 0),
            new TimeOnly(23, 59),
            false,
            "Cierre dominical"));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesCalendario.FranjaDuplicada, resultado.Mensaje);
        repositorio.Verify(
            servicio => servicio.AgregarAsync(It.IsAny<FranjaNoOperativa>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
