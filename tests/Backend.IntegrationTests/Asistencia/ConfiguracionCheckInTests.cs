using Backend.IntegrationTests.Infraestructura;

namespace Backend.IntegrationTests.Asistencia;

/// <summary>
/// Arranque de la API con la configuración de check-in: una tolerancia fuera del rango admitido
/// impide construir el host, de modo que el servicio nunca quede con una ventana indefinida.
/// </summary>
public sealed class ConfiguracionCheckInTests
{
    private const string Cadena = "Server=localhost,1433;Database=ReservasConfigCheckIn;User Id=sa;Password=Reservas.Segura.2026!;TrustServerCertificate=True;Encrypt=True";

    [Fact]
    public void UnaToleranciaDeCeroImpideElArranqueDeLaApi()
    {
        var fabrica = FabricaApiPruebas.Crear(
            Cadena,
            sobrescribir: new Dictionary<string, string?>
            {
                ["Reservas:VentanaToleranciaCheckInMinutos"] = "0"
            },
            entorno: "Production");

        var excepcion = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());

        Assert.Contains("VentanaToleranciaCheckInMinutos", excepcion.ToString(), StringComparison.Ordinal);
        Assert.Contains("1 y 720", excepcion.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnaCadenciaDeBarridoInvalidaImpideElArranqueDeLaApi()
    {
        var fabrica = FabricaApiPruebas.Crear(
            Cadena,
            sobrescribir: new Dictionary<string, string?>
            {
                ["CheckIn:MinutosIntervaloBarrido"] = "0"
            },
            entorno: "Production");

        var excepcion = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());

        Assert.Contains("MinutosIntervaloBarrido", excepcion.ToString(), StringComparison.Ordinal);
    }
}
