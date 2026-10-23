using Backend.Application.Common;
using Backend.Application.Modules.Calendario;
using Backend.Application.Modules.Calendario.Dtos;
using Backend.Presentation.Api.Contracts;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Administración de franjas no operativas globales. Todas las operaciones exigen el rol
/// <c>Administrativo</c> y la baja es lógica.
/// </summary>
[ApiController]
[Route("api/franjas-no-operativas")]
[Authorize(Roles = RolesAplicacion.Administrativo)]
public sealed class FranjasNoOperativasController : ControllerBase
{
    private readonly ListarFranjasHandler _listar;
    private readonly CrearFranjaHandler _crear;
    private readonly ActualizarFranjaHandler _actualizar;
    private readonly EliminarFranjaHandler _eliminar;

    /// <summary>Inicializa el controlador con los casos de uso de franjas no operativas.</summary>
    public FranjasNoOperativasController(
        ListarFranjasHandler listar,
        CrearFranjaHandler crear,
        ActualizarFranjaHandler actualizar,
        EliminarFranjaHandler eliminar)
    {
        _listar = listar ?? throw new ArgumentNullException(nameof(listar));
        _crear = crear ?? throw new ArgumentNullException(nameof(crear));
        _actualizar = actualizar ?? throw new ArgumentNullException(nameof(actualizar));
        _eliminar = eliminar ?? throw new ArgumentNullException(nameof(eliminar));
    }

    /// <summary>Lista las franjas no operativas vigentes ordenadas por día y hora.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FranjaNoOperativaDetalle>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var resultado = await _listar.HandleAsync(cancellationToken);

        return Ok(resultado.Valor);
    }

    /// <summary>Registra una franja no operativa rechazando duplicados.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FranjaNoOperativaDetalle), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] SolicitudCrearFranjaNoOperativa solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!TryLeerComando(solicitud, out var comando, out var error))
        {
            return error!;
        }

        var resultado = await _crear.HandleAsync(comando!, cancellationToken);

        if (resultado is { Exitoso: true, Valor: { } franja })
        {
            return Created($"/api/franjas-no-operativas/{franja.Id}", franja);
        }

        return CatalogoProblemas.DesdeResultado(
            resultado,
            HttpContext,
            ProblemasCalendario.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>Edita una franja no operativa conservando su vigencia.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(FranjaNoOperativaDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromBody] SolicitudActualizarFranjaNoOperativa solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!TryLeerComando(solicitud, out var comando, out var error))
        {
            return error!;
        }

        var resultado = await _actualizar.HandleAsync(
            id,
            new ComandoActualizarFranjaNoOperativa(
                comando!.DiaSemana,
                comando.FechaEspecifica,
                comando.HoraInicio,
                comando.HoraFin,
                comando.EsFeriado,
                comando.Motivo,
                solicitud.RowVersion),
            cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(
                resultado,
                HttpContext,
                ProblemasCalendario.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>Da de baja lógica la franja indicada.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _eliminar.HandleAsync(id, cancellationToken);

        return resultado.Exitoso
            ? NoContent()
            : CatalogoProblemas.DesdeResultado(
                resultado,
                HttpContext,
                ProblemasCalendario.SlugConflicto(resultado.Mensaje));
    }

    private bool TryLeerComando(
        SolicitudCrearFranjaNoOperativa solicitud,
        out ComandoCrearFranjaNoOperativa? comando,
        out ObjectResult? error)
    {
        comando = null;
        error = null;

        DayOfWeek? dia = null;

        if (!string.IsNullOrWhiteSpace(solicitud.DiaSemana))
        {
            if (!ParseoContratos.TryParseEnum<DayOfWeek>(solicitud.DiaSemana, out var diaParseado))
            {
                error = CampoInvalido("diaSemana", "El día de la semana no es válido.");
                return false;
            }

            dia = diaParseado;
        }

        if (!ParseoContratos.TryParseHora(solicitud.HoraInicio, out var horaInicio))
        {
            error = CampoInvalido("horaInicio", "La hora de inicio no tiene un formato válido (HH:mm).");
            return false;
        }

        if (!ParseoContratos.TryParseHora(solicitud.HoraFin, out var horaFin))
        {
            error = CampoInvalido("horaFin", "La hora de fin no tiene un formato válido (HH:mm).");
            return false;
        }

        comando = new ComandoCrearFranjaNoOperativa(
            dia,
            solicitud.FechaEspecifica,
            horaInicio,
            horaFin,
            solicitud.EsFeriado,
            solicitud.Motivo);

        return true;
    }

    private ObjectResult CampoInvalido(string campo, string mensaje) =>
        CatalogoProblemas.Crear(
            StatusCodes.Status400BadRequest,
            "solicitud-invalida",
            "Solicitud inválida",
            mensaje,
            HttpContext,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [campo] = [mensaje] });
}
