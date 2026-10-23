using Backend.Application.Modules.Calendario;
using Backend.Application.Modules.Calendario.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Calendario vigente expuesto a cualquier usuario autenticado para que la SPA anticipe el
/// periodo activo y las franjas no operativas antes de enviar una solicitud.
/// </summary>
[ApiController]
[Route("api/calendario")]
[Authorize]
public sealed class CalendarioController : ControllerBase
{
    private readonly ConsultarCalendarioVigenteHandler _vigente;

    /// <summary>Inicializa el controlador con el caso de uso del calendario vigente.</summary>
    public CalendarioController(ConsultarCalendarioVigenteHandler vigente)
    {
        _vigente = vigente ?? throw new ArgumentNullException(nameof(vigente));
    }

    /// <summary>Devuelve el periodo activo y las franjas no operativas ordenadas.</summary>
    [HttpGet("vigente")]
    [ProducesResponseType(typeof(CalendarioVigenteDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Vigente(CancellationToken cancellationToken)
    {
        var resultado = await _vigente.HandleAsync(cancellationToken);

        return Ok(resultado.Valor);
    }
}
