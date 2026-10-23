using System.Text;
using Backend.Application.Common;
using Backend.Infrastructure.Services;

namespace Backend.UnitTests.Infraestructura;

/// <summary>
/// Pruebas del almacén de pre-informes: PDF válido, PDF truncado, archivo sobredimensionado,
/// tipo declarado falso y confinamiento de rutas.
/// </summary>
public sealed class FileStorageServiceTests : IDisposable
{
    private readonly string _raiz;

    public FileStorageServiceTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), $"reservas-docs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_raiz);
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }
    }

    private FileStorageService Servicio(long maximo = 5 * 1024 * 1024) => new(
        new OpcionesAlmacen { RutaBase = _raiz },
        new OpcionesSolicitudes { TamanoMaximoPdfBytes = maximo });

    private static MemoryStream Pdf(string cola = "%%EOF") =>
        new(Encoding.ASCII.GetBytes($"%PDF-1.4 contenido del pre-informe {cola}"));

    [Fact]
    public async Task UnPdfValidoSeGuardaBajoLaCarpetaDeSolicitudes()
    {
        var servicio = Servicio();
        var solicitudId = Guid.NewGuid();

        var resultado = await servicio.GuardarPdfAsync(solicitudId, Pdf(), "informe.pdf", 1024);

        Assert.True(resultado.Exitoso);
        Assert.StartsWith($"solicitudes/{solicitudId:N}/", resultado.Valor, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_raiz, resultado.Valor!.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task UnPdfSinMarcaDeCierreSeRechaza()
    {
        var resultado = await Servicio().GuardarPdfAsync(Guid.NewGuid(), Pdf("sin cierre"), "informe.pdf", 100);

        Assert.False(resultado.Exitoso);
        Assert.Equal(TipoFallo.TipoNoSoportado, resultado.TipoFallo);
        Assert.True(resultado.Errores.ContainsKey("archivo"));
    }

    [Fact]
    public async Task UnArchivoQueNoEsPdfSeRechazaAunqueSeDeclareComoPdf()
    {
        var contenido = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x25, 0x25, 0x45, 0x4F, 0x46]);

        var resultado = await Servicio().GuardarPdfAsync(Guid.NewGuid(), contenido, "informe.pdf", 12);

        Assert.False(resultado.Exitoso);
        Assert.Equal(TipoFallo.TipoNoSoportado, resultado.TipoFallo);
    }

    [Fact]
    public async Task UnArchivoQueSuperaElMaximoSeRechazaSinEscribir()
    {
        var resultado = await Servicio(maximo: 1024)
            .GuardarPdfAsync(Guid.NewGuid(), Pdf(), "informe.pdf", 6 * 1024 * 1024);

        Assert.False(resultado.Exitoso);
        Assert.Equal(TipoFallo.ArchivoDemasiadoGrande, resultado.TipoFallo);
        Assert.Empty(Directory.GetDirectories(_raiz));
    }

    [Fact]
    public async Task ElBorradoCompensatorioEliminaElArchivo()
    {
        var servicio = Servicio();
        var guardado = await servicio.GuardarPdfAsync(Guid.NewGuid(), Pdf(), "informe.pdf", 100);

        await servicio.EliminarAsync(guardado.Valor!);

        Assert.False(File.Exists(Path.Combine(_raiz, guardado.Valor!.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task LaAperturaDeUnaRutaFueraDelAlmacenSeRechaza()
    {
        var resultado = await Servicio().AbrirAsync("../../etc/passwd");

        Assert.False(resultado.Exitoso);
        Assert.Equal(TipoFallo.NoEncontrado, resultado.TipoFallo);
    }
}
