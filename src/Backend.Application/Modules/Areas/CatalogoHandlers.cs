using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Areas;

/// <summary>Consulta pública del catálogo de áreas activas.</summary>
public sealed class ConsultarCatalogoHandler
{
    private readonly IConsultaEspacios _consulta;

    /// <summary>Inicializa el caso de uso con el puerto de lectura.</summary>
    public ConsultarCatalogoHandler(IConsultaEspacios consulta)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    /// <summary>Ejecuta la consulta anónima con filtros combinables y paginación.</summary>
    public Task<PaginaResultado<EspacioCatalogo>> HandleAsync(FiltroCatalogo filtro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var normalizado = filtro with
        {
            Q = string.IsNullOrWhiteSpace(filtro.Q) ? null : filtro.Q.Trim(),
            Categoria = filtro.Categoria is { } categoria && Enum.IsDefined(categoria) ? categoria : null,
            AforoMinimo = filtro.AforoMinimo is < 1 ? null : filtro.AforoMinimo,
            AforoMaximo = filtro.AforoMaximo is < 1 ? null : filtro.AforoMaximo,
            Pagina = Paginacion.NormalizarPagina(filtro.Pagina),
            Tamano = Paginacion.NormalizarTamano(filtro.Tamano)
        };

        return _consulta.ConsultarCatalogoAsync(normalizado, cancellationToken);
    }
}

/// <summary>Detalle público de un área activa.</summary>
public sealed class ObtenerEspacioPublicoHandler
{
    private readonly IConsultaEspacios _consulta;

    /// <summary>Inicializa el caso de uso con el puerto de lectura.</summary>
    public ObtenerEspacioPublicoHandler(IConsultaEspacios consulta)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    /// <summary>Devuelve el detalle público o un fallo de recurso inexistente.</summary>
    public async Task<Resultado<EspacioPublico>> HandleAsync(Guid espacioId, CancellationToken cancellationToken = default)
    {
        var espacio = await _consulta.ObtenerPublicoAsync(espacioId, cancellationToken);

        return espacio is null
            ? Resultado<EspacioPublico>.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado)
            : Resultado<EspacioPublico>.Exito(espacio);
    }
}
