using Backend.Application.Common;
using Microsoft.Extensions.Configuration;

namespace Backend.UnitTests.Asistencia;

/// <summary>
/// Pruebas de la configuración del check-in: la tolerancia es única y viene de
/// <c>Reservas:VentanaToleranciaCheckInMinutos</c>, mientras <c>CheckIn</c> solo define la cadencia,
/// el lote y el interruptor del barrido.
/// </summary>
public sealed class OpcionesCheckInTests
{
    private static OpcionesReservas EnlazarReservas(string? tolerancia) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reservas:VentanaToleranciaCheckInMinutos"] = tolerancia
            })
            .Build()
            .GetSection(OpcionesReservas.Seccion)
            .Get<OpcionesReservas>() ?? new OpcionesReservas();

    private static OpcionesCheckIn EnlazarCheckIn(IReadOnlyDictionary<string, string?> valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores)
            .Build()
            .GetSection(OpcionesCheckIn.Seccion)
            .Get<OpcionesCheckIn>() ?? new OpcionesCheckIn();

    [Fact]
    public void LaToleranciaPorOmisionEsDeQuinceMinutos()
    {
        var opciones = new OpcionesReservas();

        Assert.Equal(15, opciones.VentanaToleranciaCheckInMinutos);
        Assert.Equal(TimeSpan.FromMinutes(15), opciones.ObtenerVentanaTolerancia());
    }

    [Theory]
    [InlineData("10", 10)]
    [InlineData("15", 15)]
    [InlineData("20", 20)]
    public void LaToleranciaSeTomaDeLaConfiguracionSinRecompilar(string valor, int minutos)
    {
        var opciones = EnlazarReservas(valor);

        Assert.Equal(minutos, opciones.VentanaToleranciaCheckInMinutos);
        Assert.Equal(TimeSpan.FromMinutes(minutos), opciones.ObtenerVentanaTolerancia());
        opciones.Validar();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("721")]
    [InlineData("-5")]
    public void UnaToleranciaFueraDelRangoFallaElArranque(string valor)
    {
        var opciones = EnlazarReservas(valor);

        var excepcion = Assert.Throws<InvalidOperationException>(opciones.Validar);

        Assert.Contains("VentanaToleranciaCheckInMinutos", excepcion.Message, StringComparison.Ordinal);
        Assert.Contains("1 y 720", excepcion.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(720)]
    public void LosExtremosDelRangoSonValidos(int minutos)
    {
        var opciones = new OpcionesReservas { VentanaToleranciaCheckInMinutos = minutos };

        opciones.Validar();

        Assert.Equal(TimeSpan.FromMinutes(minutos), opciones.ObtenerVentanaTolerancia());
    }

    [Fact]
    public void LasOpcionesDelBarridoTienenLosValoresPorOmision()
    {
        var opciones = new OpcionesCheckIn();

        Assert.Equal(5, opciones.MinutosIntervaloBarrido);
        Assert.Equal(200, opciones.MaximoReservasPorBarrido);
        Assert.True(opciones.Habilitado);
        Assert.Equal(TimeSpan.FromMinutes(5), opciones.ObtenerIntervaloBarrido());
        opciones.Validar();
    }

    [Fact]
    public void LasOpcionesDelBarridoSeEnlazanDesdeLaSeccionCheckIn()
    {
        var opciones = EnlazarCheckIn(new Dictionary<string, string?>
        {
            ["CheckIn:MinutosIntervaloBarrido"] = "10",
            ["CheckIn:MaximoReservasPorBarrido"] = "50",
            ["CheckIn:Habilitado"] = "false"
        });

        Assert.Equal(10, opciones.MinutosIntervaloBarrido);
        Assert.Equal(50, opciones.MaximoReservasPorBarrido);
        Assert.False(opciones.Habilitado);
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(1441, 200)]
    [InlineData(5, 0)]
    [InlineData(5, 1001)]
    public void UnaCadenciaOFueraDeRangoFallaElArranque(int minutos, int maximo)
    {
        var opciones = new OpcionesCheckIn
        {
            MinutosIntervaloBarrido = minutos,
            MaximoReservasPorBarrido = maximo
        };

        var excepcion = Assert.Throws<InvalidOperationException>(opciones.Validar);

        Assert.Contains("CheckIn:", excepcion.Message, StringComparison.Ordinal);
    }
}
