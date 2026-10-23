using Backend.Application.Common;
using Backend.Application.Interfaces;

namespace Backend.Infrastructure.Services;

/// <summary>
/// Almacén local de pre-informes bajo <c>Almacen:RutaBase/solicitudes</c>. Valida tamaño y firma
/// del PDF, genera el nombre del archivo y confina toda ruta al directorio del documento.
/// </summary>
public sealed class FileStorageService : IAlmacenDocumentos
{
    private const string CarpetaSolicitudes = "solicitudes";
    private const string TipoPdf = "application/pdf";
    private static readonly byte[] FirmaPdf = [0x25, 0x50, 0x44, 0x46, 0x2D];
    private static readonly byte[] FirmaFinPdf = [0x25, 0x25, 0x45, 0x4F, 0x46];

    private readonly string _raiz;
    private readonly long _tamanoMaximo;

    /// <summary>Inicializa el almacén con la raíz configurada y el límite del pre-informe.</summary>
    public FileStorageService(OpcionesAlmacen almacen, OpcionesSolicitudes solicitudes)
    {
        ArgumentNullException.ThrowIfNull(almacen);
        ArgumentNullException.ThrowIfNull(solicitudes);

        _raiz = Path.GetFullPath(almacen.RutaBase);
        _tamanoMaximo = solicitudes.TamanoMaximoPdfBytes;
    }

    /// <inheritdoc />
    public async Task<Resultado<string>> GuardarPdfAsync(
        Guid solicitudId,
        Stream contenido,
        string nombreOriginal,
        long longitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        if (longitud > _tamanoMaximo)
        {
            return DemasiadoGrande(_tamanoMaximo);
        }

        if (string.IsNullOrWhiteSpace(nombreOriginal))
        {
            return Invalido("El archivo del pre-informe es obligatorio.");
        }

        byte[] bytes;

        using (var memoria = new MemoryStream())
        {
            await contenido.CopyToAsync(memoria, cancellationToken);
            bytes = memoria.ToArray();
        }

        if (bytes.Length == 0)
        {
            return Invalido("El archivo del pre-informe está vacío.");
        }

        if (bytes.Length > _tamanoMaximo)
        {
            return DemasiadoGrande(_tamanoMaximo);
        }

        if (!EsPdfValido(bytes))
        {
            return Invalido("El archivo del pre-informe no es un PDF válido.");
        }

        var carpetaRelativa = Path.Combine(CarpetaSolicitudes, solicitudId.ToString("N"));
        var nombreArchivo = $"{Guid.NewGuid():N}.pdf";
        var rutaRelativa = $"{carpetaRelativa.Replace('\\', '/')}/{nombreArchivo}";
        var rutaFisica = Path.Combine(_raiz, carpetaRelativa, nombreArchivo);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(rutaFisica)!);
            await File.WriteAllBytesAsync(rutaFisica, bytes, cancellationToken);

            return Resultado<string>.Exito(rutaRelativa);
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            return Resultado<string>.Fallo(
                TipoFallo.ErrorInterno,
                "No fue posible almacenar el pre-informe.",
                Errores("No fue posible almacenar el pre-informe."));
        }
    }

    /// <inheritdoc />
    public Task<Resultado<ArchivoDocumento>> AbrirAsync(
        string rutaRelativa,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolverRuta(rutaRelativa, out var rutaFisica) || !File.Exists(rutaFisica))
        {
            return Task.FromResult(NoEncontrado());
        }

        try
        {
            var longitud = new FileInfo(rutaFisica).Length;
            var flujo = new FileStream(
                rutaFisica,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            return Task.FromResult(Resultado<ArchivoDocumento>.Exito(
                new ArchivoDocumento(flujo, TipoPdf, longitud)));
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(NoEncontrado());
        }
    }

    /// <inheritdoc />
    public Task EliminarAsync(string rutaRelativa, CancellationToken cancellationToken = default)
    {
        if (!TryResolverRuta(rutaRelativa, out var rutaFisica))
        {
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(rutaFisica))
            {
                File.Delete(rutaFisica);
            }
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            // Borrado best-effort: un archivo huérfano no invalida la operación de negocio.
        }

        return Task.CompletedTask;
    }

    private bool TryResolverRuta(string rutaRelativa, out string rutaFisica)
    {
        rutaFisica = string.Empty;

        if (string.IsNullOrWhiteSpace(rutaRelativa))
        {
            return false;
        }

        var normalizada = rutaRelativa.Replace('\\', '/').TrimStart('/');

        if (!normalizada.StartsWith($"{CarpetaSolicitudes}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidata = Path.GetFullPath(Path.Combine(_raiz, normalizada.Replace('/', Path.DirectorySeparatorChar)));

        if (!candidata.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        rutaFisica = candidata;
        return true;
    }

    private static bool EsPdfValido(byte[] bytes)
    {
        if (bytes.Length < FirmaPdf.Length + FirmaFinPdf.Length)
        {
            return false;
        }

        if (!bytes.AsSpan(0, FirmaPdf.Length).SequenceEqual(FirmaPdf))
        {
            return false;
        }

        var cola = bytes.AsSpan(Math.Max(0, bytes.Length - 1024));

        return cola.LastIndexOf(FirmaFinPdf) >= 0;
    }

    private static Resultado<string> DemasiadoGrande(long maximo) =>
        Resultado<string>.Fallo(
            TipoFallo.ArchivoDemasiadoGrande,
            $"El archivo adjunto supera el tamaño máximo de {maximo / 1024d / 1024d:0} MB.",
            Errores("El archivo adjunto supera el tamaño máximo permitido."));

    private static Resultado<string> Invalido(string mensaje) =>
        Resultado<string>.Fallo(TipoFallo.TipoNoSoportado, mensaje, Errores(mensaje));

    private static Resultado<ArchivoDocumento> NoEncontrado() =>
        Resultado<ArchivoDocumento>.Fallo(
            TipoFallo.NoEncontrado,
            "El pre-informe de la solicitud no está disponible.");

    private static Dictionary<string, string[]> Errores(string mensaje) =>
        new(StringComparer.Ordinal) { ["archivo"] = [mensaje] };
}
