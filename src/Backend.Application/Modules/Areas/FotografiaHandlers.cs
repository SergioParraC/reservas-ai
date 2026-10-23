using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Areas;

/// <summary>Carga la fotografía de un área en el almacén y persiste solo su ruta relativa.</summary>
public sealed class SubirFotografiaHandler
{
    private static readonly string[] TiposPermitidos = ["image/jpeg", "image/png", "image/webp"];

    private readonly IEspaciosRepository _repositorio;
    private readonly IAlmacenArchivos _almacen;
    private readonly OpcionesEspacios _opciones;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos y los límites configurados.</summary>
    public SubirFotografiaHandler(
        IEspaciosRepository repositorio,
        IAlmacenArchivos almacen,
        OpcionesEspacios opciones,
        IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Valida los límites del archivo, lo guarda y actualiza la ruta del área.</summary>
    public async Task<Resultado<string>> HandleAsync(
        Guid espacioId,
        Stream? contenido,
        string nombreOriginal,
        string tipoContenido,
        long longitud,
        CancellationToken cancellationToken = default)
    {
        var espacio = await _repositorio.ObtenerAsync(espacioId, cancellationToken);

        if (espacio is null)
        {
            return Resultado<string>.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado);
        }

        if (contenido is null || longitud <= 0)
        {
            return Resultado<string>.Fallo(
                TipoFallo.Validacion,
                "Debe adjuntar una imagen.",
                MapeoEspacios.Errores("imagen", "Debe adjuntar una imagen."));
        }

        if (!TiposPermitidos.Contains(tipoContenido, StringComparer.OrdinalIgnoreCase))
        {
            return Resultado<string>.Fallo(
                TipoFallo.Validacion,
                "El tipo de archivo no está permitido. Use JPEG, PNG o WebP.",
                MapeoEspacios.Errores("imagen", "El tipo de archivo no está permitido. Use JPEG, PNG o WebP."));
        }

        if (longitud > _opciones.TamanoMaximoImagenBytes)
        {
            return Resultado<string>.Fallo(
                TipoFallo.Validacion,
                $"La imagen no puede superar los {_opciones.TamanoMaximoImagenBytes} bytes.",
                MapeoEspacios.Errores("imagen", $"La imagen no puede superar los {_opciones.TamanoMaximoImagenBytes} bytes."));
        }

        var guardado = await _almacen.GuardarAsync(espacioId, contenido, nombreOriginal, tipoContenido, cancellationToken);

        if (!guardado.Exitoso || guardado.Valor is null)
        {
            return Resultado<string>.Fallo(
                guardado.TipoFallo,
                guardado.Mensaje ?? "No fue posible guardar la imagen.",
                guardado.Errores.ToDictionary(par => par.Key, par => par.Value, StringComparer.Ordinal));
        }

        var anterior = espacio.FotografiaRuta;
        espacio.EstablecerFotografia(guardado.Valor, _reloj.Ahora);

        if (!await _repositorio.GuardarCambiosAsync(cancellationToken))
        {
            await _almacen.EliminarAsync(guardado.Valor, cancellationToken);
            return Resultado<string>.Fallo(TipoFallo.Conflicto, "No fue posible actualizar la fotografía del área.");
        }

        if (!string.IsNullOrWhiteSpace(anterior))
        {
            await _almacen.EliminarAsync(anterior, cancellationToken);
        }

        return Resultado<string>.Exito(MapeoEspacios.UrlFotografia(guardado.Valor) ?? string.Empty);
    }
}

/// <summary>Retira la fotografía del área y elimina el archivo del almacén.</summary>
public sealed class EliminarFotografiaHandler
{
    private readonly IEspaciosRepository _repositorio;
    private readonly IAlmacenArchivos _almacen;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos.</summary>
    public EliminarFotografiaHandler(IEspaciosRepository repositorio, IAlmacenArchivos almacen, IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Elimina la fotografía; la ausencia de archivo no es un error.</summary>
    public async Task<Resultado> HandleAsync(Guid espacioId, CancellationToken cancellationToken = default)
    {
        var espacio = await _repositorio.ObtenerAsync(espacioId, cancellationToken);

        if (espacio is null)
        {
            return Resultado.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado);
        }

        var ruta = espacio.FotografiaRuta;

        if (string.IsNullOrWhiteSpace(ruta))
        {
            return Resultado.Exito();
        }

        espacio.QuitarFotografia(_reloj.Ahora);

        if (!await _repositorio.GuardarCambiosAsync(cancellationToken))
        {
            return Resultado.Fallo(TipoFallo.Conflicto, "No fue posible eliminar la fotografía del área.");
        }

        await _almacen.EliminarAsync(ruta, cancellationToken);

        return Resultado.Exito();
    }
}
