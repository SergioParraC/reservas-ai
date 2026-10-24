using System.Globalization;
using Backend.Application.Common;
using Backend.Application.Modules.Reservas;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Disponibilidad horaria de un área completa (series, solicitudes aprobadas y reservas de sus
/// puestos). La consulta exige sesión autenticada y no revela la agenda de otros usuarios.
/// </summary>
[ApiController]
[Route("api/espacios")]
[Authorize]
public sealed class DisponibilidadEspacioController : ControllerBase
{
    private readonly ConsultarDisponibilidadEspacioHandler _disponibilidad;

    /// <summary>Inicializa el controlador con el caso de uso de disponibilidad del área.</summary>
    public DisponibilidadEspacioController(ConsultarDisponibilidadEspacioHandler disponibilidad)
    {
        _disponibilidad = disponibilidad ?? throw new ArgumentNullException(nameof(disponibilidad));
    }

    /// <summary>Consulta las franjas del área con su disponibilidad.</summary>
    [HttpGet("{id:guid}/disponibilidad")]
    [ProducesResponseType(typeof(DisponibilidadEspacio), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Disponibilidad(
        Guid id,
        [FromQuery] string? fecha,
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        [FromQuery] int? duracionMinutos,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(
                fecha,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dia))
        {
            return CampoInvalido("fecha", "La fecha es obligatoria y debe usar el formato yyyy-MM-dd.");
        }

        if (!TryParseHora(desde, out var horaDesde))
        {
            return CampoInvalido("desde", "La hora inicial debe usar el formato HH:mm.");
        }

        if (!TryParseHora(hasta, out var horaHasta))
        {
            return CampoInvalido("hasta", "La hora final debe usar el formato HH:mm.");
        }

        if (duracionMinutos is <= 0)
        {
            return CampoInvalido("duracionMinutos", "La duración debe ser mayor que cero.");
        }

        var resultado = await _disponibilidad.HandleAsync(
            id,
            new FiltroDisponibilidad(dia, horaDesde, horaHasta, duracionMinutos),
            cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(
                resultado,
                HttpContext,
                ProblemasSolicitud.SlugConflicto(resultado.Mensaje));
    }

    private static bool TryParseHora(string? texto, out TimeOnly? valor)
    {
        valor = null;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return true;
        }

        if (!TimeOnly.TryParseExact(texto, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parseada))
        {
            return false;
        }

        valor = parseada;
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
