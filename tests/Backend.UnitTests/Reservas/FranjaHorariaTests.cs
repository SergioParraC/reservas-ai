using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas del value object de franja: solapamiento con intervalo semiabierto, duración,
/// alineación, jornada y horizonte de reserva.
/// </summary>
public sealed class FranjaHorariaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));

    private static PoliticaReserva Politica() => new(
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(4),
        IntervaloMinutos: 15,
        TimeSpan.FromMinutes(30),
        TimeSpan.FromDays(14),
        TimeSpan.FromMinutes(30),
        new TimeOnly(7, 0),
        new TimeOnly(21, 0));

    [Fact]
    public void FranjasContiguasNoSeSolapan()
    {
        var primera = FranjaHoraria.Desde(
            new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 11, 3, 11, 0, 0, TimeSpan.FromHours(-5)));
        var segunda = FranjaHoraria.Desde(
            new DateTimeOffset(2026, 11, 3, 11, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.FromHours(-5)));

        Assert.False(primera.SeSolapaCon(segunda));
        Assert.False(segunda.SeSolapaCon(primera));
    }

    [Fact]
    public void FranjasParcialesSeSolapan()
    {
        var reservada = FranjaHoraria.Desde(
            new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.FromHours(-5)));
        var candidata = FranjaHoraria.Desde(
            new DateTimeOffset(2026, 11, 3, 11, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 11, 3, 13, 0, 0, TimeSpan.FromHours(-5)));

        Assert.True(reservada.SeSolapaCon(candidata));
    }

    [Fact]
    public void DuracionFueraDeRangoLanzaDomainException()
    {
        Assert.Throws<DomainException>(() => FranjaHoraria.Crear(
            new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 11, 3, 10, 15, 0, TimeSpan.FromHours(-5)),
            Politica(),
            Ahora));
    }

    [Fact]
    public void InicioNoAlineadoLanzaDomainException()
    {
        Assert.Throws<DomainException>(() => FranjaHoraria.Crear(
            new DateTimeOffset(2026, 11, 3, 10, 7, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 11, 3, 11, 7, 0, TimeSpan.FromHours(-5)),
            Politica(),
            Ahora));
    }

    [Fact]
    public void FranjaFueraDeJornadaLanzaDomainException()
    {
        Assert.Throws<DomainException>(() => FranjaHoraria.Crear(
            new DateTimeOffset(2026, 11, 3, 6, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 11, 3, 7, 0, 0, TimeSpan.FromHours(-5)),
            Politica(),
            Ahora));
    }

    [Fact]
    public void InicioDentroDelMargenDeAntelacionLanzaDomainException()
    {
        Assert.Throws<DomainException>(() => FranjaHoraria.Crear(
            Ahora,
            Ahora.AddHours(1),
            Politica(),
            Ahora));
    }

    [Fact]
    public void InicioFueraDelHorizonteLanzaDomainException()
    {
        Assert.Throws<DomainException>(() => FranjaHoraria.Crear(
            new DateTimeOffset(2026, 11, 23, 10, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 11, 23, 11, 0, 0, TimeSpan.FromHours(-5)),
            Politica(),
            Ahora));
    }
}
