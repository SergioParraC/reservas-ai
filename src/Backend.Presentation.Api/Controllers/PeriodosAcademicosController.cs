using Backend.Application.Common;
using Backend.Application.Modules.Calendario;
using Backend.Application.Modules.Calendario.Dtos;
using Backend.Presentation.Api.Contracts;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Administración de periodos académicos. Todas las operaciones exigen el rol
/// <c>Administrativo</c>; la activación reemplaza al periodo vigente en una transacción.
/// </summary>
[ApiController]
[Route("api/periodos-academicos")]
[Authorize(Roles = RolesAplicacion.Administrativo)]
public sealed class PeriodosAcademicosController : ControllerBase
{
    private readonly ListarPeriodosHandler _listar;
    private readonly CrearPeriodoHandler _crear;
    private readonly ActualizarPeriodoHandler _actualizar;
    private readonly ActivarPeriodoHandler _activar;

    /// <summary>Inicializa el controlador con los casos de uso de periodos.</summary>
    public PeriodosAcademicosController(
        ListarPeriodosHandler listar,
        CrearPeriodoHandler crear,
        ActualizarPeriodoHandler actualizar,
        ActivarPeriodoHandler activar)
    {
        _listar = listar ?? throw new ArgumentNullException(nameof(listar));
        _crear = crear ?? throw new ArgumentNullException(nameof(crear));
        _actualizar = actualizar ?? throw new ArgumentNullException(nameof(actualizar));
        _activar = activar ?? throw new ArgumentNullException(nameof(activar));
    }

    /// <summary>Lista todos los periodos registrados, activos e inactivos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PeriodoAcademicoDetalle>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var resultado = await _listar.HandleAsync(cancellationToken);

        return Ok(resultado.Valor);
    }

    /// <summary>Registra un periodo inactivo rechazando rangos invertidos o solapados.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PeriodoAcademicoDetalle), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] SolicitudCrearPeriodo solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var resultado = await _crear.HandleAsync(
            new ComandoCrearPeriodo(solicitud.Nombre, solicitud.FechaInicio, solicitud.FechaFin),
            cancellationToken);

        if (resultado is { Exitoso: true, Valor: { } periodo })
        {
            return Created($"/api/periodos-academicos/{periodo.Id}", periodo);
        }

        return CatalogoProblemas.DesdeResultado(
            resultado,
            HttpContext,
            ProblemasCalendario.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>Edita un periodo conservando su estado y validando la ocupación confirmada.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PeriodoAcademicoDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromBody] SolicitudActualizarPeriodo solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var resultado = await _actualizar.HandleAsync(
            id,
            new ComandoActualizarPeriodo(
                solicitud.Nombre,
                solicitud.FechaInicio,
                solicitud.FechaFin,
                solicitud.RowVersion),
            cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(
                resultado,
                HttpContext,
                ProblemasCalendario.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>Activa el periodo y desactiva al vigente en la misma operación.</summary>
    [HttpPut("{id:guid}/activar")]
    [ProducesResponseType(typeof(PeriodoAcademicoDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _activar.HandleAsync(id, cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(
                resultado,
                HttpContext,
                ProblemasCalendario.SlugConflicto(resultado.Mensaje));
    }
}
