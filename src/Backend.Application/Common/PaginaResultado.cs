namespace Backend.Application.Common;

/// <summary>Página de resultados de una consulta paginada.</summary>
/// <typeparam name="T">Tipo del elemento de la página.</typeparam>
public sealed record PaginaResultado<T>(
    IReadOnlyList<T> Elementos,
    int Total,
    int Pagina,
    int Tamano);

/// <summary>Reglas comunes de paginación de la API.</summary>
public static class Paginacion
{
    /// <summary>Tamaño de página usado cuando el cliente no lo indica.</summary>
    public const int TamanoPorDefecto = 20;

    /// <summary>Tamaño máximo admitido por solicitud.</summary>
    public const int TamanoMaximo = 50;

    /// <summary>Página usada cuando el cliente no la indica.</summary>
    public const int PaginaPorDefecto = 1;

    /// <summary>Normaliza la página solicitada al rango admitido.</summary>
    public static int NormalizarPagina(int pagina) => pagina < 1 ? PaginaPorDefecto : pagina;

    /// <summary>Normaliza el tamaño solicitado al rango admitido.</summary>
    public static int NormalizarTamano(int tamano) => tamano switch
    {
        < 1 => TamanoPorDefecto,
        > TamanoMaximo => TamanoMaximo,
        _ => tamano
    };
}
