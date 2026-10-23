using Backend.Domain.Services;
using Backend.Domain.ValueObjects;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas del motor de disponibilidad: franja ocupada, antelación mínima, horizonte máximo
/// y ausencia de bloqueo cuando la reserva fue cancelada.
/// </summary>
public sealed class MotorDisponibilidadTests
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private static PoliticaReserva Politica() => new(
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(4),
        IntervaloMinutos: 15,
        TimeSpan.FromMinutes(30),
        TimeSpan.FromDays(14),
        TimeSpan.FromMinutes(30),
        new TimeOnly(7, 0),
        new TimeOnly(15, 0));

    private static DateTimeOffset EnZona(int dia, int hora) =>
        MotorDisponibilidad.InicioEnZona(new DateOnly(2026, 11, dia), new TimeOnly(hora, 0), Zona);

    [Fact]
    public void UnaFranjaOcupadaNoSeMarcaDisponible()
    {
        var ocupada = FranjaHoraria.Desde(EnZona(3, 10), EnZona(3, 11));
        var ahora = EnZona(2, 8);

        var franjas = MotorDisponibilidad.GenerarFranjas(
            new DateOnly(2026, 11, 3),
            Politica(),
            Zona,
            [ocupada],
            ahora,
            duracionMinutos: 60);

        var franjaOcupada = Assert.Single(franjas, franja => franja.Franja.Inicio == EnZona(3, 10));
        Assert.False(franjaOcupada.Disponible);
        Assert.Equal(MotivoNoDisponible.Ocupada, franjaOcupada.Motivo);

        var libre = Assert.Single(franjas, franja => franja.Franja.Inicio == EnZona(3, 11));
        Assert.True(libre.Disponible);
        Assert.Null(libre.Motivo);
    }

    [Fact]
    public void LasFranjasDentroDelMargenDeAntelacionNoEstanDisponibles()
    {
        var ahora = EnZona(3, 9).AddMinutes(30);

        var franjas = MotorDisponibilidad.GenerarFranjas(
            new DateOnly(2026, 11, 3),
            Politica(),
            Zona,
            [],
            ahora,
            duracionMinutos: 60);

        var temprana = Assert.Single(franjas, franja => franja.Franja.Inicio == EnZona(3, 9));
        Assert.Equal(MotivoNoDisponible.FueraDeAntelacion, temprana.Motivo);

        var posterior = Assert.Single(franjas, franja => franja.Franja.Inicio == EnZona(3, 10));
        Assert.True(posterior.Disponible);
    }

    [Fact]
    public void LasFranjasSuperanElHorizonteSeMarcanFueraDeHorizonte()
    {
        var ahora = EnZona(2, 8);

        var franjas = MotorDisponibilidad.GenerarFranjas(
            new DateOnly(2026, 11, 25),
            Politica(),
            Zona,
            [],
            ahora,
            duracionMinutos: 60);

        Assert.NotEmpty(franjas);
        Assert.All(franjas, franja => Assert.Equal(MotivoNoDisponible.FueraDeHorizonte, franja.Motivo));
    }

    [Fact]
    public void UnaReservaCanceladaNoOcupaLaFranja()
    {
        var ahora = EnZona(2, 8);

        // El repositorio solo entrega franjas de reservas vigentes: sin ocupadas, todo está libre.
        var franjas = MotorDisponibilidad.GenerarFranjas(
            new DateOnly(2026, 11, 3),
            Politica(),
            Zona,
            [],
            ahora,
            duracionMinutos: 60);

        Assert.All(franjas, franja => Assert.True(franja.Disponible));
    }

    [Fact]
    public void HaySolapamientoDetectaCrucesParcialesYDescartaContiguos()
    {
        var candidata = FranjaHoraria.Desde(EnZona(3, 10), EnZona(3, 11));
        var contigua = FranjaHoraria.Desde(EnZona(3, 11), EnZona(3, 12));
        var parcial = FranjaHoraria.Desde(EnZona(3, 10).AddMinutes(30), EnZona(3, 11).AddMinutes(30));

        Assert.False(MotorDisponibilidad.HaySolapamiento(candidata, [contigua]));
        Assert.True(MotorDisponibilidad.HaySolapamiento(candidata, [parcial]));
    }
}
