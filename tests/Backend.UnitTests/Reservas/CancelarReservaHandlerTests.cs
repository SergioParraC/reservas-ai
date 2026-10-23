using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Reservas;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.ValueObjects;
using Moq;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas de la cancelación: éxito del titular, reserva ajena, reserva inexistente y
/// cancelación fuera del margen de antelación.
/// </summary>
public sealed class CancelarReservaHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid PuestoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Titular = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Otro = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static OpcionesReservas Opciones() => new()
    {
        AntelacionCancelacionMinutos = 30,
        ZonaHoraria = "America/Bogota"
    };

    private static Reserva ReservaDe(Guid usuarioId, DateTimeOffset inicio) => Reserva.Confirmar(
        PuestoId,
        usuarioId,
        FranjaHoraria.Desde(inicio, inicio.AddHours(1)),
        Ahora);

    private static CancelarReservaHandler Handler(Mock<IReservasRepository> repositorio)
    {
        var reloj = new Mock<IRelojSistema>();
        reloj.SetupGet(servicio => servicio.Ahora).Returns(Ahora);

        var usuario = new Mock<IUsuarioActual>();
        usuario.SetupGet(servicio => servicio.UsuarioId).Returns(Titular);

        return new CancelarReservaHandler(repositorio.Object, Opciones(), reloj.Object, usuario.Object);
    }

    private static Mock<IReservasRepository> RepositorioCon(Reserva? reserva, bool guarda = true)
    {
        var repositorio = new Mock<IReservasRepository>();
        repositorio.Setup(servicio => servicio.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reserva);
        repositorio.Setup(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guarda);
        return repositorio;
    }

    [Fact]
    public async Task CancelarUnaReservaPropiaDevuelveExito()
    {
        var reserva = ReservaDe(Titular, Ahora.AddDays(1));

        var resultado = await Handler(RepositorioCon(reserva)).HandleAsync(reserva.Id);

        Assert.True(resultado.Exitoso);
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
    }

    [Fact]
    public async Task CancelarUnaReservaAjenaDevuelve403()
    {
        var reserva = ReservaDe(Otro, Ahora.AddDays(1));

        var resultado = await Handler(RepositorioCon(reserva)).HandleAsync(reserva.Id);

        Assert.Equal(TipoFallo.NoPermitido, resultado.TipoFallo);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    public async Task CancelarFueraDelPlazoDevuelve409()
    {
        var reserva = ReservaDe(Titular, Ahora.AddMinutes(20));

        var resultado = await Handler(RepositorioCon(reserva)).HandleAsync(reserva.Id);

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    public async Task CancelarUnaReservaInexistenteDevuelve404()
    {
        var resultado = await Handler(RepositorioCon(null)).HandleAsync(Guid.NewGuid());

        Assert.Equal(TipoFallo.NoEncontrado, resultado.TipoFallo);
    }

    [Fact]
    public async Task ElConflictoDeConcurrenciaAlGuardarDevuelve409()
    {
        var reserva = ReservaDe(Titular, Ahora.AddDays(1));

        var resultado = await Handler(RepositorioCon(reserva, guarda: false)).HandleAsync(reserva.Id);

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
    }
}
