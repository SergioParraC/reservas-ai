using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas del agregado de reserva: estado inicial, titularidad y cancelación dentro y fuera
/// del margen configurado.
/// </summary>
public sealed class ReservaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Puesto = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Titular = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Otro = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static PoliticaReserva Politica() => new(
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(4),
        IntervaloMinutos: 15,
        TimeSpan.FromMinutes(30),
        TimeSpan.FromDays(14),
        TimeSpan.FromMinutes(30),
        new TimeOnly(7, 0),
        new TimeOnly(21, 0));

    private static Reserva Nueva(DateTimeOffset inicio) => Reserva.Confirmar(
        Puesto,
        Titular,
        FranjaHoraria.Desde(inicio, inicio.AddHours(1)),
        Ahora);

    [Fact]
    public void LaReservaNaceConfirmadaYVigente()
    {
        var reserva = Nueva(Ahora.AddDays(1));

        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        Assert.True(reserva.EstaVigente);
        Assert.Null(reserva.CanceladaEn);
        Assert.Equal(Puesto, reserva.PuestoId);
    }

    [Fact]
    public void CancelarDentroDelPlazoCambiaElEstadoYRegistraElMomento()
    {
        var reserva = Nueva(Ahora.AddDays(1));

        reserva.Cancelar(Ahora, Politica());

        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
        Assert.False(reserva.EstaVigente);
        Assert.Equal(Ahora, reserva.CanceladaEn);
    }

    [Fact]
    public void CancelarEnElLimiteExactoDelMargenEstaPermitido()
    {
        var inicio = Ahora.AddMinutes(30);
        var reserva = Nueva(inicio);

        reserva.Cancelar(Ahora, Politica());

        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
    }

    [Fact]
    public void CancelarFueraDelPlazoLanzaConflicto()
    {
        var reserva = Nueva(Ahora.AddMinutes(20));

        var excepcion = Assert.Throws<ConflictoException>(() => reserva.Cancelar(Ahora, Politica()));

        Assert.Contains("30", excepcion.Message, StringComparison.Ordinal);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    public void CancelarDosVecesLanzaConflicto()
    {
        var reserva = Nueva(Ahora.AddDays(1));
        reserva.Cancelar(Ahora, Politica());

        Assert.Throws<ConflictoException>(() => reserva.Cancelar(Ahora, Politica()));
    }

    [Fact]
    public void LaReservaNoPerteneceAOtroUsuario()
    {
        var reserva = Nueva(Ahora.AddDays(1));

        Assert.True(reserva.PerteneceA(Titular));
        Assert.False(reserva.PerteneceA(Otro));
    }

    [Fact]
    public void LiberarPorNoShowMarcaElEstadoYConservaLasMarcasDeLaReserva()
    {
        var reserva = Nueva(Ahora.AddDays(1));

        reserva.LiberarPorNoShow(Ahora.AddMinutes(15));

        Assert.Equal(EstadoReserva.NoShow, reserva.Estado);
        Assert.False(reserva.EstaVigente);
        Assert.Equal(Ahora.AddMinutes(15), reserva.CanceladaEn);
        Assert.Null(reserva.FechaCheckIn);
    }
}
