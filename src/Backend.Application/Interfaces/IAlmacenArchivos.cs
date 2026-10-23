using Backend.Application.Common;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto del almacén de fotografías. La base de datos solo guarda la ruta relativa
/// que devuelve este puerto; el archivo vive en el volumen administrado.
/// </summary>
public interface IAlmacenArchivos
{
    /// <summary>Guarda el archivo y devuelve su ruta relativa dentro del almacén.</summary>
    Task<Resultado<string>> GuardarAsync(
        Guid espacioId,
        Stream contenido,
        string nombreOriginal,
        string tipoContenido,
        CancellationToken cancellationToken = default);

    /// <summary>Elimina un archivo del almacén; un archivo ausente no es un error.</summary>
    Task EliminarAsync(string rutaRelativa, CancellationToken cancellationToken = default);
}
