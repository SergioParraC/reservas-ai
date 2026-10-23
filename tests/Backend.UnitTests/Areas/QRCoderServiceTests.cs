using System.Text.RegularExpressions;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Infrastructure.Services;
using FluentAssertions;
using QRCoder;

namespace Backend.UnitTests.Areas;

/// <summary>
/// Pruebas del emisor vectorial: ausencia de mapas de bits, coincidencia exacta de módulos
/// oscuros con el codificador y paginación de la plantilla A4.
/// </summary>
public sealed class QRCoderServiceTests
{
    private readonly QRCoderService _servicio = new();

    [Fact]
    public void ElSvgIndividualNoContieneImagenesNiDatosEmbebidos()
    {
        var svg = _servicio.GenerarSvg("QR-0123456789ABCDEFGHJKMNPQRS", new OpcionesQr());

        svg.Should().StartWith("<svg");
        svg.Should().Contain("mm");
        svg.Should().Contain("<path");
        svg.Should().NotContain("<image");
        svg.Should().NotContain("data:");
        svg.Should().NotContain("xlink:href");
        svg.Should().NotContain("href=");
    }

    [Fact]
    public void LosModulosOscurosDelSvgCoincidenConElCodificador()
    {
        const string contenido = "QR-7T3K9M2XQ4ZP8V6BN1C5HJ0WRS";

        var svg = _servicio.GenerarSvg(contenido, new OpcionesQr(LadoMilimetros: 40, ModuloSilencio: 4));

        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);

        var modulosOscuros = datos.ModuleMatrix.Sum(fila => fila.Cast<bool>().Count(modulo => modulo));
        var trazos = Regex.Match(svg, "<path d=\"(?<d>[^\"]+)\"").Groups["d"].Value;

        Regex.Matches(trazos, "M").Count.Should().Be(modulosOscuros);
    }

    [Fact]
    public void LaPlantillaPaginaLasEtiquetasSegunLaCapacidad()
    {
        var etiquetas = Enumerable.Range(1, 25)
            .Select(indice => new EtiquetaQr($"QR-{indice:00000000000000000000000000}", $"Unidad {indice}", "Bloque B", "Área"))
            .ToArray();

        var primera = _servicio.GenerarPlantillaSvg(new PlantillaQr(etiquetas, 1, 18, "20261022"));
        var segunda = _servicio.GenerarPlantillaSvg(new PlantillaQr(etiquetas, 2, 18, "20261022"));

        Regex.Matches(primera, "<path").Count.Should().Be(18);
        Regex.Matches(segunda, "<path").Count.Should().Be(7);
        primera.Should().Contain("Unidad 1");
        segunda.Should().Contain("Unidad 25");
    }

    [Fact]
    public void LaPlantillaEscapaElTextoDeLasEtiquetas()
    {
        var etiquetas = new[] { new EtiquetaQr("QR-0123456789ABCDEFGHJKMNPQRS", "<script>", "Bloque & B", null) };

        var svg = _servicio.GenerarPlantillaSvg(new PlantillaQr(etiquetas, 1, 18, "20261022"));

        svg.Should().NotContain("<script>");
        svg.Should().Contain("&lt;script&gt;");
        svg.Should().Contain("Bloque &amp; B");
    }
}
