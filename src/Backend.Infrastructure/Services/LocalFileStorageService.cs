using Backend.Application.Common;
using Backend.Application.Interfaces;

namespace Backend.Infrastructure.Services;

/// <summary>
/// Almacén local de fotografías bajo <c>Almacen:RutaBase</c>. El nombre del archivo lo
/// genera el servidor y la base de datos solo guarda la ruta relativa resultante.
/// </summary>
public sealed class LocalFileStorageService : IAlmacenArchivos
{
    private static readonly Dictionary<string, string> Extensiones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly string _raiz;

    /// <summary>Inicializa el almacén y crea la raíz si no existe.</summary>
    public LocalFileStorageService(OpcionesAlmacen opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _raiz = Path.GetFullPath(opciones.RutaBase);
    }

    /// <inheritdoc />
    public async Task<Resultado<string>> GuardarAsync(
        Guid espacioId,
        Stream contenido,
        string nombreOriginal,
        string tipoContenido,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        var tipo = tipoContenido ?? string.Empty;

        if (!Extensiones.TryGetValue(tipo, out var extension))
        {
            return Invalido("El tipo de archivo no está permitido. Use JPEG, PNG o WebP.");
        }

        byte[] bytes;

        using (var memoria = new MemoryStream())
        {
            await contenido.CopyToAsync(memoria, cancellationToken);
            bytes = memoria.ToArray();
        }

        if (bytes.Length == 0)
        {
            return Invalido("El archivo está vacío.");
        }

        if (!CoincideConTipo(bytes, tipo))
        {
            return Invalido("El contenido del archivo no corresponde al tipo declarado.");
        }

        var carpetaRelativa = Path.Combine("espacios", espacioId.ToString("N"));
        var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
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
                "No fue posible almacenar la fotografía.",
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["imagen"] = ["No fue posible almacenar la fotografía."]
                });
        }
    }

    /// <inheritdoc />
    public Task EliminarAsync(string rutaRelativa, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
        {
            return Task.CompletedTask;
        }

        try
        {
            var rutaFisica = Path.GetFullPath(Path.Combine(_raiz, rutaRelativa.Replace('/', Path.DirectorySeparatorChar)));

            if (rutaFisica.StartsWith(_raiz, StringComparison.OrdinalIgnoreCase) && File.Exists(rutaFisica))
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

    private static bool CoincideConTipo(byte[] bytes, string tipoContenido) => tipoContenido.ToLowerInvariant() switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
        "image/webp" => bytes.Length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
        _ => false
    };

    private static Resultado<string> Invalido(string mensaje) =>
        Resultado<string>.Fallo(
            TipoFallo.Validacion,
            mensaje,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["imagen"] = [mensaje] });
}
