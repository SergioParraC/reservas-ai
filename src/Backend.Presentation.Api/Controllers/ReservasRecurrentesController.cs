using Backend.Application.Common;
using Backend.Application.Modules.Reservas;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Enums;
using Backend.Presentation.Api.Contracts;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Series semestrales de área completa. Solo el rol <c>Docente</c> puede radicarlas y consultar
/// las propias; la serie se confirma como un todo o se rechaza íntegra.
/// </summary>
[ApiController]
[Route("api/reservas/recurrentes")]
[Authorize(Roles = RolesAplicacion.Docente)]
public sealed class ReservasRecurrentesController : ControllerBase
{
    private readonly CrearSerieHandler _crear;
    private readonly ConsultarSeriesHandler _consultar;

    /// <summary>Inicializa el controlador con los casos de uso de series.</summary>
    public ReservasRecurrentesController(CrearSerieHandler crear, ConsultarSeriesHandler consultar)
    {
        _crear = crear ?? throw new ArgumentNullException(nameof(crear));
        _consultar = consultar ?? throw new ArgumentNullException(nameof(consultar));
    }

    /// <summary>Radica la serie completa omitiendo los días no operativos del calendario.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SerieDetalle), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] SolicitudCrearSerie solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!TryLeerComando(solicitud, out var comando, out var error))
        {
            return error!;
        }

        var resultado = await _crear.HandleAsync(comando!, cancellationToken);

        if (resultado is { Exitoso: true, Valor: { } serie })
        {
            return Created($"/api/reservas/recurrentes/{serie.Id}", serie);
        }

        return CatalogoProblemas.DesdeResultado(
            resultado,
            HttpContext,
            ProblemasSolicitud.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>Lista las series del docente autenticado, de la más reciente a la más antigua.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(PaginaResultado<SerieResumen>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MisSeries(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = Paginacion.TamanoPorDefecto,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _consultar.HandleAsync(pagina, tamano, cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    private bool TryLeerComando(
        SolicitudCrearSerie solicitud,
        out ComandoCrearSerie? comando,
        out ObjectResult? error)
    {
        comando = null;
        error = null;

        if (!ParseoContratos.TryParseEnum<TipoRecurrencia>(solicitud.TipoRecurrencia, out var tipo))
        {
            error = CampoInvalido("tipoRecurrencia", "El tipo de recurrencia no es válido.");
            return false;
        }

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

        var fechas = new List<DateOnly>();

        foreach (var texto in solicitud.FechasFijas ?? [])
        {
            if (!ParseoContratos.TryParseFecha(texto, out var fecha))
            {
                error = CampoInvalido("fechasFijas", "Cada fecha fija debe tener el formato yyyy-MM-dd.");
                return false;
            }

            fechas.Add(fecha);
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

        comando = new ComandoCrearSerie(
            solicitud.EspacioId,
            tipo,
            dia,
            fechas,
            horaInicio,
            horaFin,
            solicitud.PeriodoInicio,
            solicitud.PeriodoFin,
            solicitud.Materia);

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
