namespace Backend.Application.Common;

/// <summary>
/// Clase de fallo de un caso de uso. La presentación la traduce al código HTTP del contrato.
/// </summary>
public enum TipoFallo
{
    /// <summary>La operación se completó.</summary>
    Ninguno = 0,

    /// <summary>Los datos de la solicitud no cumplen una regla de validación.</summary>
    Validacion = 1,

    /// <summary>La operación choca con el estado actual del recurso.</summary>
    Conflicto = 2,

    /// <summary>El recurso solicitado no existe.</summary>
    NoEncontrado = 3,

    /// <summary>La operación está deshabilitada por configuración o por permisos del recurso.</summary>
    NoPermitido = 4,

    /// <summary>La credencial presentada no es válida.</summary>
    NoAutorizado = 5,

    /// <summary>Fallo inesperado del servidor.</summary>
    ErrorInterno = 6
}

/// <summary>Resultado de un caso de uso que no devuelve valor.</summary>
public sealed class Resultado
{
    private Resultado(bool exitoso, TipoFallo tipoFallo, string? mensaje, IReadOnlyDictionary<string, string[]> errores)
    {
        Exitoso = exitoso;
        TipoFallo = tipoFallo;
        Mensaje = mensaje;
        Errores = errores;
    }

    /// <summary>Indica si la operación se completó.</summary>
    public bool Exitoso { get; }

    /// <summary>Clase de fallo cuando <see cref="Exitoso"/> es <see langword="false"/>.</summary>
    public TipoFallo TipoFallo { get; }

    /// <summary>Mensaje legible del fallo o del resultado.</summary>
    public string? Mensaje { get; }

    /// <summary>Errores por campo, con las mismas claves que el cuerpo de la solicitud.</summary>
    public IReadOnlyDictionary<string, string[]> Errores { get; }

    /// <summary>Crea un resultado exitoso.</summary>
    public static Resultado Exito() => new(true, TipoFallo.Ninguno, null, new Dictionary<string, string[]>());

    /// <summary>Crea un resultado fallido.</summary>
    public static Resultado Fallo(TipoFallo tipoFallo, string mensaje, IDictionary<string, string[]>? errores = null)
    {
        if (tipoFallo == TipoFallo.Ninguno)
        {
            throw new ArgumentOutOfRangeException(nameof(tipoFallo), tipoFallo, "Un resultado fallido requiere una clase distinta de Ninguno.");
        }

        return new Resultado(false, tipoFallo, mensaje, Congelar(errores));
    }

    internal static IReadOnlyDictionary<string, string[]> Congelar(IDictionary<string, string[]>? errores) =>
        errores is null || errores.Count == 0
            ? new Dictionary<string, string[]>()
            : errores.ToDictionary(par => par.Key, par => par.Value, StringComparer.Ordinal);
}

/// <summary>Resultado de un caso de uso que devuelve un valor.</summary>
/// <typeparam name="T">Tipo del valor devuelto.</typeparam>
public sealed class Resultado<T>
{
    private Resultado(bool exitoso, T? valor, TipoFallo tipoFallo, string? mensaje, IReadOnlyDictionary<string, string[]> errores)
    {
        Exitoso = exitoso;
        Valor = valor;
        TipoFallo = tipoFallo;
        Mensaje = mensaje;
        Errores = errores;
    }

    /// <summary>Indica si la operación se completó.</summary>
    public bool Exitoso { get; }

    /// <summary>Valor devuelto cuando la operación es exitosa.</summary>
    public T? Valor { get; }

    /// <summary>Clase de fallo cuando <see cref="Exitoso"/> es <see langword="false"/>.</summary>
    public TipoFallo TipoFallo { get; }

    /// <summary>Mensaje legible del fallo.</summary>
    public string? Mensaje { get; }

    /// <summary>Errores por campo.</summary>
    public IReadOnlyDictionary<string, string[]> Errores { get; }

    /// <summary>Crea un resultado exitoso con su valor.</summary>
    public static Resultado<T> Exito(T valor) =>
        new(true, valor, TipoFallo.Ninguno, null, new Dictionary<string, string[]>());

    /// <summary>Crea un resultado fallido.</summary>
    public static Resultado<T> Fallo(TipoFallo tipoFallo, string mensaje, IDictionary<string, string[]>? errores = null)
    {
        if (tipoFallo == TipoFallo.Ninguno)
        {
            throw new ArgumentOutOfRangeException(nameof(tipoFallo), tipoFallo, "Un resultado fallido requiere una clase distinta de Ninguno.");
        }

        return new Resultado<T>(false, default, tipoFallo, mensaje, Resultado.Congelar(errores));
    }
}
