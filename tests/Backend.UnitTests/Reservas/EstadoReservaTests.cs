using Backend.Domain.Enums;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas del contrato numérico de <see cref="EstadoReserva"/>: los valores persistidos no cambian
/// de significado y el estado liberado por inasistencia conserva el valor <c>3</c>.
/// </summary>
public sealed class EstadoReservaTests
{
    [Theory]
    [InlineData(EstadoReserva.Confirmada, 1)]
    [InlineData(EstadoReserva.Cancelada, 2)]
    [InlineData(EstadoReserva.NoShow, 3)]
    [InlineData(EstadoReserva.EnUso, 4)]
    [InlineData(EstadoReserva.Finalizada, 5)]
    public void LosValoresPersistidosConservanElContrato(EstadoReserva estado, int valor) =>
        Assert.Equal(valor, (int)estado);

    [Fact]
    public void ElEstadoDeInasistenciaSeRenombroSinCambiarSuValor()
    {
        Assert.Equal(EstadoReserva.NoShow, Enum.Parse<EstadoReserva>("NoShow"));
        Assert.Equal(3, (int)EstadoReserva.NoShow);
    }

    [Fact]
    public void ElNombreAnteriorYaNoExiste()
    {
        Assert.DoesNotContain("CanceladaPorNoShow", Enum.GetNames<EstadoReserva>(), StringComparer.Ordinal);
    }

    [Fact]
    public void LosCincoEstadosSonDistintosYDefinidos()
    {
        var valores = Enum.GetValues<EstadoReserva>();

        Assert.Equal(5, valores.Length);
        Assert.All(valores, estado => Assert.True(Enum.IsDefined(estado)));
        Assert.Equal(valores.Length, valores.Distinct().Count());
    }
}
