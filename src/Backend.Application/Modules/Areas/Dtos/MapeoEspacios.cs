using Backend.Application.Common;
using Backend.Domain.Entities;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Areas.Dtos;

/// <summary>
/// Mapeos compartidos entre casos de uso del módulo de áreas y helper de clasificación
/// de los mensajes de dominio en errores por campo.
/// </summary>
public static class MapeoEspacios
{
    /// <summary>Construye la URL pública de la fotografía a partir de la ruta relativa.</summary>
    public static string? UrlFotografia(string? rutaRelativa) =>
        string.IsNullOrWhiteSpace(rutaRelativa)
            ? null
            : $"{RutasPublicas.Media}/{rutaRelativa.Replace('\\', '/').TrimStart('/')}";

    /// <summary>Proyecta un puesto al contrato administrativo.</summary>
    public static PuestoDetalle ADetalle(Puesto puesto)
    {
        ArgumentNullException.ThrowIfNull(puesto);

        return new PuestoDetalle(
            puesto.Id,
            puesto.Identificador,
            puesto.DescripcionEquipo,
            puesto.Estado,
            puesto.CodigoQR.Valor,
            puesto.CreadoEn);
    }

    /// <summary>Proyecta un área completa al contrato administrativo.</summary>
    public static EspacioDetalle ADetalle(Espacio espacio)
    {
        ArgumentNullException.ThrowIfNull(espacio);

        return new EspacioDetalle(
            espacio.Id,
            espacio.Nombre,
            espacio.Ubicacion,
            espacio.Categoria,
            espacio.Aforo,
            espacio.TipoAprobacion,
            espacio.Estado,
            espacio.CodigoQR.Valor,
            UrlFotografia(espacio.FotografiaRuta),
            espacio.CreadoEn,
            espacio.ActualizadoEn,
            Convert.ToBase64String(espacio.RowVersion),
            espacio.Puestos
                .OrderBy(puesto => puesto.Identificador, StringComparer.OrdinalIgnoreCase)
                .Select(ADetalle)
                .ToArray(),
            espacio.TipoModalidadPrestamo,
            espacio.TipoElementoPrestamo);
    }

    /// <summary>Proyecta un área al resumen del listado administrativo.</summary>
    public static EspacioResumen AResumen(Espacio espacio)
    {
        ArgumentNullException.ThrowIfNull(espacio);

        return new EspacioResumen(
            espacio.Id,
            espacio.Nombre,
            espacio.Ubicacion,
            espacio.Categoria,
            espacio.Aforo,
            espacio.TipoAprobacion,
            espacio.Estado,
            espacio.CodigoQR.Valor,
            UrlFotografia(espacio.FotografiaRuta),
            espacio.PuestosActivos,
            espacio.CreadoEn,
            espacio.ActualizadoEn,
            Convert.ToBase64String(espacio.RowVersion),
            espacio.TipoModalidadPrestamo,
            espacio.TipoElementoPrestamo);
    }

    /// <summary>Construye el diccionario de errores por campo que consume el contrato RFC 7807.</summary>
    public static IDictionary<string, string[]> Errores(string campo, string mensaje) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal) { [campo] = [mensaje] };

    /// <summary>Determina el campo del cuerpo al que corresponde un mensaje de dominio.</summary>
    public static string CampoDe(string mensaje) => mensaje switch
    {
        MensajesEspacio.NombreRequerido or MensajesEspacio.NombreDemasiadoLargo => "nombre",
        MensajesEspacio.UbicacionRequerida or MensajesEspacio.UbicacionDemasiadoLarga => "ubicacion",
        MensajesEspacio.CategoriaInvalida => "categoria",
        MensajesEspacio.TipoAprobacionInvalido => "tipoAprobacion",
        MensajesEspacio.ModalidadInvalida or MensajesEspacio.CombinacionPrestamoInvalida => "tipoModalidadPrestamo",
        MensajesEspacio.ElementoInvalido => "tipoElementoPrestamo",
        MensajesEspacio.EstadoInvalido => "estado",
        MensajesEspacio.PuestoIdentificadorRequerido or MensajesEspacio.PuestoIdentificadorDemasiadoLargo => "identificador",
        MensajesEspacio.DescripcionEquipoDemasiadoLarga => "descripcionEquipo",
        MensajesEspacio.CodigoQrInvalido => "codigoQR",
        _ => "aforo"
    };
}
