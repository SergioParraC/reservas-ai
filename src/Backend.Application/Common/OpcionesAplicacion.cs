namespace Backend.Application.Common;

/// <summary>
/// Política institucional del catálogo físico. Se enlaza desde configuración en infraestructura
/// y se inyecta como objeto plano para no arrastrar el framework de opciones a la aplicación.
/// </summary>
public sealed class OpcionesEspacios
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Espacios";

    /// <summary>Aforo máximo permitido para cualquier área.</summary>
    public int AforoMaximo { get; set; } = 500;

    /// <summary>Cantidad de etiquetas QR por hoja A4.</summary>
    public int EtiquetasPorHoja { get; set; } = 18;

    /// <summary>Tamaño máximo admitido para la fotografía de un área.</summary>
    public long TamanoMaximoImagenBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Cantidad máxima de unidades que puede incluir una plantilla QR.</summary>
    public int MaximoUnidadesPlantilla { get; set; } = 200;
}

/// <summary>Opciones del almacén de archivos de las fotografías.</summary>
public sealed class OpcionesAlmacen
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Almacen";

    /// <summary>Raíz física del almacén; por omisión el volumen de contenedor.</summary>
    public string RutaBase { get; set; } = "/var/lib/reservas";
}

/// <summary>Política del autorregistro de cuentas.</summary>
public sealed class OpcionesIdentidad
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Identidad";

    /// <summary>Indica si el formulario público de registro está habilitado.</summary>
    public bool PermitirAutorregistro { get; set; } = true;

    /// <summary>Dominios de correo institucional admitidos.</summary>
    public IReadOnlyList<string> DominiosCorreo { get; set; } = [];

    /// <summary>Texto original de la clave <c>Identidad:DominioCorreoInstitucional</c>.</summary>
    /// <remarks>
    /// Admite uno o varios dominios separados por comas. El prefijo <c>@</c> es opcional:
    /// <c>uninpahu.edu.co</c> y <c>@uninpahu.edu.co</c> se consideran equivalentes.
    /// </remarks>
    public string DominioCorreoInstitucional { get; set; } = string.Empty;

    /// <summary>Normaliza la lista de dominios admitidos.</summary>
    public IReadOnlyList<string> ObtenerDominios()
    {
        if (DominiosCorreo.Count > 0)
        {
            return DominiosCorreo;
        }

        return DominioCorreoInstitucional
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}

/// <summary>Rutas públicas estables de la API.</summary>
public static class RutasPublicas
{
    /// <summary>Prefijo del almacén estático de fotografías.</summary>
    public const string Media = "/media";
}
