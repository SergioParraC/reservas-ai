using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Asistencia;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Moq;

namespace Backend.UnitTests.Asistencia;

/// <summary>
/// Pruebas del barrido de inasistencias: libera el lote completo, respeta el corte con la
/// tolerancia configurada, no toca sesiones en uso, es idempotente y falla completo si no guarda.
/// </summary>
public sealed class LiberarReservasVencidasHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 3, 10, 20, 0, TimeSpan.FromHours(-5));

    private static LiberarReservasVencidasHandler Handler(
        Mock<IReservasRepository> repositorio,
        int tolerancia = 15,
        int maximo = 200)
    {
        return new LiberarReservasVencidasHandler(
            repositorio.Object,
            SoporteAsistencia.Opciones(tolerancia),
            new OpcionesCheckIn { MaximoReservasPorBarrido = maximo },
            SoporteAsistencia.Reloj(Ahora).Object);
    }

    private static Mock<IReservasRepository> RepositorioCon(
        IReadOnlyList<Domain.Entities.Reserva> reservas,
        bool guarda = true)
    {
        var repositorio = new Mock<IReservasRepository>();
        repositorio
            .Setup(servicio => servicio.ListarSinCheckInVencidasAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservas);
        repositorio.Setup(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guarda);
        return repositorio;
    }

    private static Domain.Entities.Reserva Vencida(DateTimeOffset inicio) =>
        SoporteAsistencia.Nueva(inicio: inicio);

    [Fact]
    public async Task ElBarridoLiberaElLoteYDevuelveElConteo()
    {
        var reservas = new[]
        {
            Vencida(Ahora.AddMinutes(-60)),
            Vencida(Ahora.AddMinutes(-45)),
            Vencida(Ahora.AddMinutes(-30))
        };
        var repositorio = RepositorioCon(reservas);

        var resultado = await Handler(repositorio).HandleAsync();

        Assert.True(resultado.Exitoso);
        Assert.Equal(3, resultado.Valor!.Liberadas);
        Assert.All(reservas, reserva => Assert.Equal(EstadoReserva.NoShow, reserva.Estado));
        Assert.All(reservas, reserva => Assert.Equal(Ahora, reserva.CanceladaEn));
        Assert.All(reservas, reserva => Assert.False(reserva.EstaVigente));
        repositorio.Verify(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ElCorteSePideConLaToleranciaYElTopeConfigurados()
    {
        var repositorio = RepositorioCon([Vencida(Ahora.AddMinutes(-60))]);

        await Handler(repositorio, tolerancia: 10, maximo: 50).HandleAsync();

        repositorio.Verify(
            servicio => servicio.ListarSinCheckInVencidasAsync(
                Ahora,
                TimeSpan.FromMinutes(10),
                50,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CambiarLaToleranciaMueveElCorteSinRecompilar()
    {
        var repositorio = RepositorioCon([]);

        await Handler(repositorio, tolerancia: 20).HandleAsync();

        repositorio.Verify(
            servicio => servicio.ListarSinCheckInVencidasAsync(
                Ahora,
                TimeSpan.FromMinutes(20),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SinReservasVencidasNoGuardaNiLibera()
    {
        var repositorio = RepositorioCon([]);

        var resultado = await Handler(repositorio).HandleAsync();

        Assert.True(resultado.Exitoso);
        Assert.Equal(0, resultado.Valor!.Liberadas);
        repositorio.Verify(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnaSesionEnUsoNoSeLiberaAunqueElRepositorioLaEntregue()
    {
        var reserva = Vencida(Ahora.AddMinutes(-60));
        reserva.RegistrarCheckIn(reserva.Inicio.AddMinutes(1), TimeSpan.FromMinutes(15));
        var repositorio = RepositorioCon([reserva]);

        var resultado = await Handler(repositorio).HandleAsync();

        Assert.True(resultado.Exitoso);
        Assert.Equal(0, resultado.Valor!.Liberadas);
        Assert.Equal(EstadoReserva.EnUso, reserva.Estado);
        repositorio.Verify(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnSegundoBarridoSobreLoYaLiberadoNoRegistraOtraLiberacion()
    {
        var reserva = Vencida(Ahora.AddMinutes(-60));
        var repositorio = RepositorioCon([reserva]);
        var handler = Handler(repositorio);

        var primero = await handler.HandleAsync();
        var segundo = await handler.HandleAsync();

        Assert.Equal(1, primero.Valor!.Liberadas);
        Assert.Equal(0, segundo.Valor!.Liberadas);
        Assert.Equal(EstadoReserva.NoShow, reserva.Estado);
        Assert.Equal(Ahora, reserva.CanceladaEn);
        repositorio.Verify(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ElFalloDelLoteDevuelveConflictoYElSiguienteCicloReintenta()
    {
        var reserva = Vencida(Ahora.AddMinutes(-60));
        var repositorio = RepositorioCon([reserva], guarda: false);

        var resultado = await Handler(repositorio).HandleAsync();

        Assert.False(resultado.Exitoso);
        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.LiberacionNoPersistida, resultado.Mensaje);
        repositorio.Verify(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
