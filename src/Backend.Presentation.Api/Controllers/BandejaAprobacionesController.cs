using Backend.Application.Common;
using Backend.Application.Modules.Aprobaciones;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Enums;
using Backend.Presentation.Api.Contracts;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Bandeja única de aprobaciones. El docente ve solo las solicitudes de áreas con aprobación
/// docente y el administrativo las de cualquier tipo, con filtros por estado, área y fechas.
/// </summary>
[ApiController]
[Route("api/bandeja-aprobaciones")]
[Authorize(Roles = $"{RolesAplicacion.Docente},{RolesAplicacion.Administrativo}")]
public sealed class BandejaAprobacionesController : ControllerBase
{
    private readonly ConsultarBandejaHandler _bandeja;

    /// <summary>Inicializa el controlador con el caso de uso de la bandeja.</summary>
    public BandejaAprobacionesController(ConsultarBandejaHandler bandeja)
    {
        _bandeja = bandeja ?? throw new ArgumentNullException(nameof(bandeja));
    }

    /// <summary>Lista las solicitudes que el revisor puede resolver con sus filtros.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaResultado<SolicitudBandejaItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? estado,
        [FromQuery] Guid? espacioId,
        [FromQuery] string? desde,
        [FromQuery] string? hasta,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = Paginacion.TamanoPorDefecto,
        CancellationToken cancellationToken = default)
    {
        EstadoSolicitud? filtroEstado = null;

        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!ParseoContratos.TryParseEnum<EstadoSolicitud>(estado, out var parseado))
            {
                return CampoInvalido("estado", "El estado de la solicitud no es válido.");
            }

            filtroEstado = parseado;
        }

        if (!TryParseOpcional(desde, out var inicio))
        {
            return CampoInvalido("desde", "La fecha inicial no tiene un formato válido.");
        }

        if (!TryParseOpcional(hasta, out var fin))
        {
            return CampoInvalido("hasta", "La fecha final no tiene un formato válido.");
        }

        var resultado = await _bandeja.HandleAsync(
            new FiltroBandeja(filtroEstado, espacioId, inicio, fin, pagina, tamano),
            cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(
                resultado,
                HttpContext,
                ProblemasSolicitud.SlugConflicto(resultado.Mensaje));
    }

    private static bool TryParseOpcional(string? texto, out DateTimeOffset? valor)
    {
        valor = null;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(
                texto,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parseado))
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
