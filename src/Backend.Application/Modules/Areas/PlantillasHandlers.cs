using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Areas;

/// <summary>Emite el SVG del código QR de un área registrada.</summary>
public sealed class GenerarQrEspacioHandler
{
    private readonly IEspaciosRepository _repositorio;
    private readonly IQrGenerator _generador;

    /// <summary>Inicializa el caso de uso con el repositorio y el generador vectorial.</summary>
    public GenerarQrEspacioHandler(IEspaciosRepository repositorio, IQrGenerator generador)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _generador = generador ?? throw new ArgumentNullException(nameof(generador));
    }

    /// <summary>Genera el documento SVG del área indicada.</summary>
    public async Task<Resultado<string>> HandleAsync(Guid espacioId, CancellationToken cancellationToken = default)
    {
        var espacio = await _repositorio.ObtenerAsync(espacioId, cancellationToken);

        return espacio is null
            ? Resultado<string>.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado)
            : Resultado<string>.Exito(_generador.GenerarSvg(espacio.CodigoQR.Valor, new OpcionesQr()));
    }
}

/// <summary>Emite el SVG del código QR de un puesto registrado.</summary>
public sealed class GenerarQrPuestoHandler
{
    private readonly IEspaciosRepository _repositorio;
    private readonly IQrGenerator _generador;

    /// <summary>Inicializa el caso de uso con el repositorio y el generador vectorial.</summary>
    public GenerarQrPuestoHandler(IEspaciosRepository repositorio, IQrGenerator generador)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _generador = generador ?? throw new ArgumentNullException(nameof(generador));
    }

    /// <summary>Genera el documento SVG del puesto indicado.</summary>
    public async Task<Resultado<string>> HandleAsync(Guid puestoId, CancellationToken cancellationToken = default)
    {
        var puesto = await _repositorio.ObtenerPuestoAsync(puestoId, cancellationToken);

        return puesto is null
            ? Resultado<string>.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.PuestoInexistente)
            : Resultado<string>.Exito(_generador.GenerarSvg(puesto.CodigoQR.Valor, new OpcionesQr()));
    }
}

/// <summary>Compone la plantilla de impresión paginada de las unidades seleccionadas.</summary>
public sealed class GenerarPlantillaQrHandler
{
    private readonly IEspaciosRepository _repositorio;
    private readonly IQrGenerator _generador;
    private readonly OpcionesEspacios _opciones;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos y la política de impresión.</summary>
    public GenerarPlantillaQrHandler(
        IEspaciosRepository repositorio,
        IQrGenerator generador,
        OpcionesEspacios opciones,
        IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _generador = generador ?? throw new ArgumentNullException(nameof(generador));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Genera la hoja solicitada e informa cuántas hojas componen la plantilla.</summary>
    public async Task<Resultado<PlantillaGenerada>> HandleAsync(
        ComandoPlantillaQr comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (comando.EspacioIds is null || comando.EspacioIds.Count == 0)
        {
            return Resultado<PlantillaGenerada>.Fallo(
                TipoFallo.Validacion,
                "Debe indicar al menos un área para la plantilla.",
                MapeoEspacios.Errores("espacioIds", "Debe indicar al menos un área para la plantilla."));
        }

        var identificadores = comando.EspacioIds.Distinct().ToArray();

        if (identificadores.Length > _opciones.MaximoUnidadesPlantilla)
        {
            return Resultado<PlantillaGenerada>.Fallo(
                TipoFallo.Validacion,
                $"La plantilla admite como máximo {_opciones.MaximoUnidadesPlantilla} unidades por solicitud.");
        }

        var espacios = await _repositorio.ObtenerVariosAsync(identificadores, cancellationToken);

        if (espacios.Count == 0)
        {
            return Resultado<PlantillaGenerada>.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado);
        }

        var etiquetas = new List<EtiquetaQr>(espacios.Count);

        foreach (var espacio in espacios.OrderBy(espacio => espacio.Nombre, StringComparer.OrdinalIgnoreCase))
        {
            etiquetas.Add(new EtiquetaQr(espacio.CodigoQR.Valor, espacio.Nombre, espacio.Ubicacion, "Área"));

            if (!comando.IncluirPuestos)
            {
                continue;
            }

            etiquetas.AddRange(espacio.Puestos
                .Where(puesto => puesto.Estado == Domain.Enums.EstadoUnidad.Activo)
                .OrderBy(puesto => puesto.Identificador, StringComparer.OrdinalIgnoreCase)
                .Select(puesto => new EtiquetaQr(
                    puesto.CodigoQR.Valor,
                    $"{espacio.Nombre} · {puesto.Identificador}",
                    espacio.Ubicacion,
                    puesto.DescripcionEquipo)));
        }

        if (etiquetas.Count > _opciones.MaximoUnidadesPlantilla)
        {
            return Resultado<PlantillaGenerada>.Fallo(
                TipoFallo.Validacion,
                $"La plantilla admite como máximo {_opciones.MaximoUnidadesPlantilla} unidades por solicitud.");
        }

        var etiquetasPorHoja = Math.Max(1, _opciones.EtiquetasPorHoja);
        var totalHojas = (int)Math.Ceiling(etiquetas.Count / (double)etiquetasPorHoja);

        if (comando.Hoja < 1 || comando.Hoja > totalHojas)
        {
            return Resultado<PlantillaGenerada>.Fallo(
                TipoFallo.Validacion,
                $"La hoja solicitada no existe. La plantilla tiene {totalHojas} hoja(s).",
                MapeoEspacios.Errores("hoja", $"La plantilla tiene {totalHojas} hoja(s)."));
        }

        var fecha = _reloj.Ahora;
        var fechaTexto = fecha.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var svg = _generador.GenerarPlantillaSvg(new PlantillaQr(etiquetas, comando.Hoja, etiquetasPorHoja, fechaTexto));
        var nombreArchivo = $"plantilla-qr-{fechaTexto}-hoja-{comando.Hoja}.svg";

        return Resultado<PlantillaGenerada>.Exito(new PlantillaGenerada(svg, totalHojas, nombreArchivo));
    }
}
