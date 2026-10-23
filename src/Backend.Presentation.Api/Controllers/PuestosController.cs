using System.Globalization;
using System.Text;
using Backend.Application.Common;
using Backend.Application.Modules.Areas;
using Backend.Application.Modules.Reservas;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>Disponibilidad horaria de un puesto y descarga de su código QR individual.</summary>
[ApiController]
[Route("api/puestos")]
[Authorize]
public sealed class PuestosController : ControllerBase
{
    private const string TipoSvg = "image/svg+xml";
    private const string CacheInmutable = "public, max-age=31536000, immutable";

    private readonly GenerarQrPuestoHandler _qr;
    private readonly ConsultarDisponibilidadHandler _disponibilidad;

    /// <summary>Inicializa el controlador con los casos de uso del puesto.</summary>
    public PuestosController(GenerarQrPuestoHandler qr, ConsultarDisponibilidadHandler disponibilidad)
    {
        _qr = qr ?? throw new ArgumentNullException(nameof(qr));
        _disponibilidad = disponibilidad ?? throw new ArgumentNullException(nameof(disponibilidad));
    }

    /// <summary>Consulta las franjas del puesto con su disponibilidad; exige sesión autenticada.</summary>
    [HttpGet("{id:guid}/disponibilidad")]
    [ProducesResponseType(typeof(DisponibilidadPuesto), StatusCodes.Status200OK)]
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
                ProblemasReserva.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>Devuelve el SVG del puesto; una unidad inactiva conserva su código original.</summary>
    [HttpGet("{puestoId:guid}/qr.svg")]
    [Authorize(Roles = RolesAplicacion.Administrativo)]
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

