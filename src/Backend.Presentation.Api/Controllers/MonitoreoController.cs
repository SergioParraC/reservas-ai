using Backend.Application.Common;
using Backend.Application.Modules.Monitoreo;
using Backend.Application.Modules.Monitoreo.Dtos;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Mapa de ocupación institucional de solo lectura. Exige rol <c>Administrativo</c>: los datos de
/// ocupación en vivo no se exponen a estudiantes ni a sesiones anónimas.
/// </summary>
[ApiController]
[Route("api/monitoreo")]
[Authorize(Roles = RolesAplicacion.Administrativo)]
public sealed class MonitoreoController : ControllerBase
{
    private readonly ConsultarOcupacionHandler _ocupacion;

    /// <summary>Inicializa el controlador con el caso de uso del mapa de ocupación.</summary>
    public MonitoreoController(ConsultarOcupacionHandler ocupacion)
    {
        _ocupacion = ocupacion ?? throw new ArgumentNullException(nameof(ocupacion));
    }

    /// <summary>Devuelve los totales y el desglose por área en el instante de la consulta.</summary>
    [HttpGet("ocupacion-actual")]
    [ProducesResponseType(typeof(OcupacionActual), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> OcupacionActual(CancellationToken cancellationToken)
    {
        var resultado = await _ocupacion.HandleAsync(cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }
}
