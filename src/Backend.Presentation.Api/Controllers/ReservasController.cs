using System.Globalization;
using Backend.Application.Common;
using Backend.Application.Modules.Asistencia;
using Backend.Application.Modules.Asistencia.Dtos;
using Backend.Application.Modules.Reservas;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Enums;
using Backend.Presentation.Api.Contracts;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Reservas puntuales de puestos de cómputo: alta autoconfirmada, agenda del titular,
/// cancelación dentro del margen, check-in con el QR del puesto y check-out con liberación del
/// remanente. Todos los endpoints exigen sesión autenticada y solo el titular opera su reserva.
/// </summary>
[ApiController]
[Route("api/reservas")]
[Authorize]
public sealed class ReservasController : ControllerBase
{
    private readonly CrearReservaHandler _crear;
    private readonly ConsultarMisReservasHandler _misReservas;
    private readonly CancelarReservaHandler _cancelar;
    private readonly RegistrarCheckInHandler _checkIn;
    private readonly RegistrarCheckOutHandler _checkOut;

    /// <summary>Inicializa el controlador con los casos de uso de reservas y de asistencia.</summary>
    public ReservasController(
        CrearReservaHandler crear,
        ConsultarMisReservasHandler misReservas,
        CancelarReservaHandler cancelar,
        RegistrarCheckInHandler checkIn,
        RegistrarCheckOutHandler checkOut)
    {
        _crear = crear ?? throw new ArgumentNullException(nameof(crear));
        _misReservas = misReservas ?? throw new ArgumentNullException(nameof(misReservas));
        _cancelar = cancelar ?? throw new ArgumentNullException(nameof(cancelar));
        _checkIn = checkIn ?? throw new ArgumentNullException(nameof(checkIn));
        _checkOut = checkOut ?? throw new ArgumentNullException(nameof(checkOut));
    }

    /// <summary>Registra la reserva confirmada de inmediato si la franja está libre.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReservaDetalle), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] SolicitudCrearReserva solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var resultado = await _crear.HandleAsync(
            new ComandoCrearReserva(solicitud.PuestoId, solicitud.Inicio, solicitud.Fin),
            cancellationToken);

        if (resultado is { Exitoso: true, Valor: { } detalle })
        {
            return Created($"/api/reservas/{detalle.Id}", detalle);
        }

        return CatalogoProblemas.DesdeResultado(
            resultado,
            HttpContext,
            ProblemasSolicitud.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>Lista las reservas del titular autenticado, ordenadas por inicio ascendente.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(PaginaResultado<ReservaResumen>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MisReservas(
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        [FromQuery] string? estado,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = Paginacion.TamanoPorDefecto,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseOpcional(desde, out var inicio))
        {
            return CampoInvalido("desde", "La fecha inicial no tiene un formato válido.");
        }

        if (!TryParseOpcional(hasta, out var fin))
        {
            return CampoInvalido("hasta", "La fecha final no tiene un formato válido.");
        }

        EstadoReserva? filtroEstado = null;

        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!ParseoContratos.TryParseEnum<EstadoReserva>(estado, out var parseado))
            {
                return CampoInvalido("estado", "El estado de la reserva no es válido.");
            }

            filtroEstado = parseado;
        }

        var resultado = await _misReservas.HandleAsync(
            new FiltroReservasPropias(inicio, fin, filtroEstado, pagina, tamano),
            cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Cancela una reserva propia dentro del margen permitido.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _cancelar.HandleAsync(id, cancellationToken);

        if (resultado.Exitoso)
        {
            return NoContent();
        }

        return CatalogoProblemas.DesdeResultado(
            resultado,
            HttpContext,
            ProblemasSolicitud.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>
    /// Confirma la presencia con el QR del puesto dentro de la ventana de tolerancia configurada y
    /// deja la reserva en <c>EnUso</c>.
    /// </summary>
    [HttpPost("{id:guid}/check-in")]
    [ProducesResponseType(typeof(ReservaDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn(
        Guid id,
        [FromBody] SolicitudCheckIn solicitud,
        CancellationToken cancellationToken)
    {
        if (solicitud is null || string.IsNullOrWhiteSpace(solicitud.CodigoQr))
        {
            return CampoInvalido("codigoQr", "El código QR del puesto es obligatorio.");
        }

        var resultado = await _checkIn.HandleAsync(
            new ComandoRegistrarCheckIn(id, solicitud.CodigoQr.Trim()),
            cancellationToken);

        if (resultado is { Exitoso: true, Valor: { } detalle })
        {
            return Ok(detalle);
        }

        return CatalogoProblemas.DesdeResultado(
            resultado,
            HttpContext,
            ProblemasReserva.SlugAsistencia(resultado.Mensaje));
    }

    /// <summary>
    /// Registra la salida desde <c>EnUso</c> en cualquier instante. El fin de la franja se acorta
    /// al momento de salida y el remanente vuelve a estar disponible para otro usuario.
    /// </summary>
    [HttpPost("{id:guid}/check-out")]
    [ProducesResponseType(typeof(ReservaDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckOut(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _checkOut.HandleAsync(id, cancellationToken);

        if (resultado is { Exitoso: true, Valor: { } detalle })
        {
            return Ok(detalle);
        }

        return CatalogoProblemas.DesdeResultado(
            resultado,
            HttpContext,
            ProblemasReserva.SlugAsistencia(resultado.Mensaje));
    }

    private static bool TryParseOpcional(string? texto, out DateTimeOffset? valor)
    {
        valor = null;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parseado))
        {
            return false;
        }

        valor = parseado;
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

