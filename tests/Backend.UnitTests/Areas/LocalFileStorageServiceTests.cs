using Backend.Application.Common;
using Backend.Infrastructure.Services;
using FluentAssertions;

namespace Backend.UnitTests.Areas;

/// <summary>
/// Pruebas del almacén local: ruta relativa generada por el servidor, verificación de
/// <c>magic bytes</c>, borrado idempotente y rechazo de contenido incoherente.
/// </summary>
public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "reservas-pruebas-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GuardarUnPngDevuelveLaRutaRelativaYBorraElArchivo()
    {
        var servicio = CrearServicio();
        var espacioId = Guid.NewGuid();
        using var contenido = new MemoryStream(ContenidoPng());

        var resultado = await servicio.GuardarAsync(espacioId, contenido, "foto.png", "image/png");

        resultado.Exitoso.Should().BeTrue();
        resultado.Valor.Should().NotBeNull();
        resultado.Valor.Should().StartWith($"espacios/{espacioId:N}/");
        resultado.Valor.Should().EndWith(".png");

        var rutaFisica = Path.Combine(_raiz, resultado.Valor!.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(rutaFisica).Should().BeTrue();

        await servicio.EliminarAsync(resultado.Valor);
        File.Exists(rutaFisica).Should().BeFalse();

        // El borrado repetido no es un error.
        await servicio.EliminarAsync(resultado.Valor);
    }

    [Fact]
    public async Task GuardarRechazaUnContenidoQueNoCoincideConElTipoDeclarado()
    {
        var servicio = CrearServicio();
        using var contenido = new MemoryStream([0x3C, 0x68, 0x74, 0x6D, 0x6C, 0x3E]);

        var resultado = await servicio.GuardarAsync(Guid.NewGuid(), contenido, "foto.png", "image/png");

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Validacion);
    }

    [Fact]
    public async Task GuardarRechazaUnTipoNoPermitido()
    {
        var servicio = CrearServicio();
        using var contenido = new MemoryStream(ContenidoPng());

        var resultado = await servicio.GuardarAsync(Guid.NewGuid(), contenido, "foto.gif", "image/gif");

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Validacion);
    }

    /// <summary>Elimina el directorio temporal de la prueba.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }
    }

    private LocalFileStorageService CrearServicio() => new(new OpcionesAlmacen { RutaBase = _raiz });

    private static byte[] ContenidoPng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];
}
