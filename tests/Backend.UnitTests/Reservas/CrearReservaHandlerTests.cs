using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Reservas;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Moq;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas del alta autoconfirmada: éxito, puesto inexistente, área sin aprobación automática,
/// franja ocupada, cupo agotado y franja inválida.
/// </summary>
public sealed class CrearReservaHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid PuestoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EspacioId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Titular = Guid.Parse("33333333-3333-3333-3333-333333333333");

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

    private static PuestoReservable Puesto(TipoAprobacion aprobacion = TipoAprobacion.Automatica) =>
        new(PuestoId, "PC-01", EstadoUnidad.Activo, EspacioId, "Sala de Cómputo 1", EstadoUnidad.Activo, aprobacion);

    private static ComandoCrearReserva Comando(DateTimeOffset inicio, TimeSpan duracion) =>
        new(PuestoId, inicio, inicio + duracion);

    private static DateTimeOffset EnZona(int dia, int hora) =>
        new(2026, 11, dia, hora, 0, 0, TimeSpan.FromHours(-5));

    private static Mock<IConsultaReservas> ConsultaCon(PuestoReservable? puesto)
    {
        var consulta = new Mock<IConsultaReservas>();
        consulta.Setup(servicio => servicio.ObtenerPuestoAsync(PuestoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(puesto);
        return consulta;
    }

    private static CrearReservaHandler Handler(Mock<IConsultaReservas> consulta, Mock<IReservasRepository> repositorio)
    {
        var reloj = new Mock<IRelojSistema>();
        reloj.SetupGet(servicio => servicio.Ahora).Returns(Ahora);

        var usuario = new Mock<IUsuarioActual>();
        usuario.SetupGet(servicio => servicio.UsuarioId).Returns(Titular);

        return new CrearReservaHandler(
            repositorio.Object,
            consulta.Object,
            CalendarioAbierto().Object,
            Opciones(),
            reloj.Object,
            usuario.Object);
    }

    private static Mock<IConsultaCalendario> CalendarioAbierto()
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        var periodo = PeriodoAcademico.Crear(
            "2026-2",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 12, 15),
            Ahora);
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

    [Fact]
    public async Task ElAltaConfirmadaDevuelveElDetalleDeLaReserva()
    {
        var repositorio = new Mock<IReservasRepository>();
        repositorio.Setup(servicio => servicio.GuardarSiDisponibleAsync(
                It.IsAny<Reserva>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoGuardadoReserva.Guardada);

        var resultado = await Handler(ConsultaCon(Puesto()), repositorio)
            .HandleAsync(Comando(EnZona(3, 10), TimeSpan.FromHours(1)));

        Assert.True(resultado.Exitoso);
        Assert.NotNull(resultado.Valor);
        Assert.Equal("PC-01", resultado.Valor.PuestoIdentificador);
        Assert.Equal(EstadoReserva.Confirmada, resultado.Valor.Estado);
    }

    [Fact]
    public async Task ElAltaSobreUnPuestoInexistenteDevuelve404()
    {
        var resultado = await Handler(ConsultaCon(null), new Mock<IReservasRepository>())
            .HandleAsync(Comando(EnZona(3, 10), TimeSpan.FromHours(1)));

        Assert.Equal(TipoFallo.NoEncontrado, resultado.TipoFallo);
    }

    [Fact]
    public async Task ElAltaSobreUnAreaConAprobacionDocenteDevuelve409()
    {
        var resultado = await Handler(ConsultaCon(Puesto(TipoAprobacion.Docente)), new Mock<IReservasRepository>())
            .HandleAsync(Comando(EnZona(3, 10), TimeSpan.FromHours(1)));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.PuestoNoReservable, resultado.Mensaje);
    }

    [Fact]
    public async Task ElAltaSobreUnaFranjaOcupadaDevuelve409()
    {
        var repositorio = new Mock<IReservasRepository>();
        repositorio.Setup(servicio => servicio.GuardarSiDisponibleAsync(
                It.IsAny<Reserva>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoGuardadoReserva.FranjaOcupada);

        var resultado = await Handler(ConsultaCon(Puesto()), repositorio)
            .HandleAsync(Comando(EnZona(3, 10), TimeSpan.FromHours(1)));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.FranjaNoDisponible, resultado.Mensaje);
    }

    [Fact]
    public async Task ElAltaConElCupoAgotadoDevuelve409ConElMaximo()
    {
        var repositorio = new Mock<IReservasRepository>();
        repositorio.Setup(servicio => servicio.GuardarSiDisponibleAsync(
                It.IsAny<Reserva>(), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoGuardadoReserva.CupoAgotado);

        var resultado = await Handler(ConsultaCon(Puesto()), repositorio)
            .HandleAsync(Comando(EnZona(3, 10), TimeSpan.FromHours(1)));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Contains("2", resultado.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElAltaConDuracionFueraDeRangoDevuelve400()
    {
        var resultado = await Handler(ConsultaCon(Puesto()), new Mock<IReservasRepository>())
            .HandleAsync(Comando(EnZona(3, 10), TimeSpan.FromMinutes(15)));

        Assert.Equal(TipoFallo.Validacion, resultado.TipoFallo);
        Assert.True(resultado.Errores.ContainsKey("inicio"));
    }
}
