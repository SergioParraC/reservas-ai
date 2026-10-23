using Backend.Domain.Services;
using Backend.Domain.ValueObjects;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas del barrido de colisiones: cruces, contigüidad, agenda vacía y tope de reporte.
/// </summary>
public sealed class DetectarColisionesTests
{
    private static FranjaHoraria Franja(int horaInicio, int horaFin) =>
        FranjaHoraria.Desde(
            new DateTimeOffset(2026, 8, 4, horaInicio, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 8, 4, horaFin, 0, 0, TimeSpan.FromHours(-5)));

    [Fact]
    public void UnaAgendaVaciaNoProduceColisiones()
    {
        var colisiones = MotorDisponibilidad.DetectarColisiones([Franja(14, 16)], []);

        Assert.Empty(colisiones);
    }

    [Fact]
    public void ElCruceParcialSeReportaConElBloqueOcupado()
    {
        var colisiones = MotorDisponibilidad.DetectarColisiones([Franja(14, 16)], [Franja(15, 17)]);

        var colision = Assert.Single(colisiones);
        Assert.Equal(Franja(14, 16), colision.Candidata);
        Assert.Equal(Franja(15, 17), colision.Ocupada);
    }

    [Fact]
    public void LasFranjasContiguasNoSonColision()
    {
        var colisiones = MotorDisponibilidad.DetectarColisiones([Franja(14, 16)], [Franja(16, 18)]);

        Assert.Empty(colisiones);
    }

    [Fact]
    public void LosBloquesSolapadosSeFusionanAntesDelBarrido()
    {
        var fusionadas = MotorDisponibilidad.Fusionar([Franja(14, 15), Franja(14, 16), Franja(17, 18)]);

        Assert.Equal(2, fusionadas.Count);
        Assert.Equal(Franja(14, 16), fusionadas[0]);
    }

    [Fact]
    public void ElReporteSeAcotaAlTopeIndicado()
    {
        var candidatas = Enumerable.Range(0, 30)
            .Select(indice => Franja(0, 1).Desplazar(TimeSpan.FromDays(indice)))
            .ToArray();

        var ocupadas = candidatas.Select(candidata => candidata.Desplazar(TimeSpan.FromMinutes(30))).ToArray();

        var colisiones = MotorDisponibilidad.DetectarColisiones(candidatas, ocupadas, maximo: 5);

        Assert.Equal(5, colisiones.Count);
    }
}
