using Backend.Application.Common;
using Backend.Application.Modules.Areas;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Enums;
using Backend.Presentation.Api.Contracts;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>Catálogo público de áreas activas; no requiere autenticación.</summary>
[ApiController]
[Route("api/catalogo")]
[AllowAnonymous]
public sealed class CatalogoController : ControllerBase
{
    private readonly ConsultarCatalogoHandler _consultar;
    private readonly ObtenerEspacioPublicoHandler _obtener;

    /// <summary>Inicializa el controlador con los casos de uso de lectura pública.</summary>
    public CatalogoController(ConsultarCatalogoHandler consultar, ObtenerEspacioPublicoHandler obtener)
    {
        _consultar = consultar ?? throw new ArgumentNullException(nameof(consultar));
        _obtener = obtener ?? throw new ArgumentNullException(nameof(obtener));
    }

    /// <summary>Lista áreas activas con filtros de texto, categoría y rango de aforo.</summary>
    [HttpGet("espacios")]
    [ProducesResponseType(typeof(PaginaResultado<EspacioCatalogo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? q,
        [FromQuery] string? categoria,
        [FromQuery] int? aforoMinimo,
        [FromQuery] int? aforoMaximo,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 20,
        CancellationToken cancellationToken = default)
    {
        var filtro = new FiltroCatalogo(
            q,
            ParseoContratos.TryParseEnum<CategoriaEspacio>(categoria, out var valor) ? valor : null,
            aforoMinimo,
            aforoMaximo,
            pagina,
            tamano);

        return Ok(await _consultar.HandleAsync(filtro, cancellationToken));
    }

    /// <summary>Obtiene el detalle público de un área activa.</summary>
    [HttpGet("espacios/{id:guid}")]
    [ProducesResponseType(typeof(EspacioPublico), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _obtener.HandleAsync(id, cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }
}
