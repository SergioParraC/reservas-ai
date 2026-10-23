using System.Text;
using Backend.Application.Common;
using Backend.Application.Modules.Areas;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>Descarga del código QR individual de un puesto registrado.</summary>
[ApiController]
[Route("api/puestos")]
[Authorize(Roles = RolesAplicacion.Administrativo)]
public sealed class PuestosController : ControllerBase
{
    private const string TipoSvg = "image/svg+xml";
    private const string CacheInmutable = "public, max-age=31536000, immutable";

    private readonly GenerarQrPuestoHandler _qr;

    /// <summary>Inicializa el controlador con el caso de uso de emisión del QR.</summary>
    public PuestosController(GenerarQrPuestoHandler qr)
    {
        _qr = qr ?? throw new ArgumentNullException(nameof(qr));
    }

    /// <summary>Devuelve el SVG del puesto; una unidad inactiva conserva su código original.</summary>
    [HttpGet("{puestoId:guid}/qr.svg")]
    [Produces(TipoSvg)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Descargar(Guid puestoId, CancellationToken cancellationToken)
    {
        var resultado = await _qr.HandleAsync(puestoId, cancellationToken);

        if (resultado is not { Exitoso: true, Valor: { } svg })
        {
            return CatalogoProblemas.DesdeResultado(resultado, HttpContext);
        }

        Response.Headers.CacheControl = CacheInmutable;

        return File(Encoding.UTF8.GetBytes(svg), TipoSvg);
    }
}
