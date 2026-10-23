using Backend.Application.Common;
using Backend.Application.Modules.Aprobaciones;
using Backend.Application.Modules.Solicitudes;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Enums;
using Backend.Presentation.Api.Contracts;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Radicación y consulta de solicitudes de laboratorio con pre-informe PDF. La descarga exige
/// sesión y competencia sobre la solicitud; el archivo nunca se publica bajo <c>/media</c>.
/// </summary>
[ApiController]
[Route("api/solicitudes")]
[Authorize]
public sealed class SolicitudesController : ControllerBase
{
    private readonly RadicarSolicitudHandler _radicar;
    private readonly ConsultarSolicitudesHandler _consultar;
    private readonly DescargarPreinformeHandler _descargar;
    private readonly CambiarEstadoSolicitudHandler _cambiarEstado;

    /// <summary>Inicializa el controlador con los casos de uso de solicitudes.</summary>
    public SolicitudesController(
        RadicarSolicitudHandler radicar,
        ConsultarSolicitudesHandler consultar,
        DescargarPreinformeHandler descargar,
        CambiarEstadoSolicitudHandler cambiarEstado)
    {
        _radicar = radicar ?? throw new ArgumentNullException(nameof(radicar));
        _consultar = consultar ?? throw new ArgumentNullException(nameof(consultar));
        _descargar = descargar ?? throw new ArgumentNullException(nameof(descargar));
        _cambiarEstado = cambiarEstado ?? throw new ArgumentNullException(nameof(cambiarEstado));
    }

    /// <summary>Radica la solicitud con su pre-informe en estado <c>Radicado</c>.</summary>
    [HttpPost]
    [Authorize(Roles = $"{RolesAplicacion.Estudiante},{RolesAplicacion.Docente}")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(SolicitudRadicada), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> Radicar(
        [FromForm] SolicitudRadicarSolicitud solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        await using var flujo = solicitud.Archivo?.OpenReadStream();

        var resultado = await _radicar.HandleAsync(
            new ComandoRadicarSolicitud(
                solicitud.EspacioId,
                solicitud.Inicio,
                solicitud.Fin,
                solicitud.Materia,
                solicitud.DocenteResponsable,
                solicitud.Justificacion,
                flujo,
                solicitud.Archivo?.FileName,
                solicitud.Archivo?.ContentType,
                solicitud.Archivo?.Length ?? 0),
            cancellationToken);

        if (resultado is { Exitoso: true, Valor: { } radicada })
        {
            return Created($"/api/solicitudes/{radicada.Id}", radicada);
        }

        return CatalogoProblemas.DesdeResultado(
            resultado,
            HttpContext,
            ProblemasSolicitud.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>Lista las solicitudes del solicitante autenticado, de la más reciente a la más antigua.</summary>
    [HttpGet("me")]
    [Authorize(Roles = $"{RolesAplicacion.Estudiante},{RolesAplicacion.Docente}")]
    [ProducesResponseType(typeof(PaginaResultado<SolicitudResumen>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MisSolicitudes(
        [FromQuery] string? estado,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = Paginacion.TamanoPorDefecto,
        CancellationToken cancellationToken = default)
    {
        EstadoSolicitud? filtro = null;

        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!ParseoContratos.TryParseEnum<EstadoSolicitud>(estado, out var parseado))
            {
                return CampoInvalido("estado", "El estado de la solicitud no es válido.");
            }

            filtro = parseado;
        }

        var resultado = await _consultar.HandleAsync(filtro, pagina, tamano, cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Descarga el pre-informe solo para su autor y los revisores competentes.</summary>
    [HttpGet("{id:guid}/preinforme")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DescargarPreinforme(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _descargar.HandleAsync(id, cancellationToken);

        if (resultado is not { Exitoso: true, Valor: { } archivo })
        {
            return CatalogoProblemas.DesdeResultado(
                resultado,
                HttpContext,
                ProblemasSolicitud.SlugConflicto(resultado.Mensaje));
        }

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";

        return File(archivo.Contenido, archivo.TipoContenido, archivo.NombreOriginal);
    }

    /// <summary>Cambia el estado de una solicitud de la competencia del revisor.</summary>
    [HttpPut("{id:guid}/estado")]
    [Authorize(Roles = $"{RolesAplicacion.Docente},{RolesAplicacion.Administrativo}")]
    [ProducesResponseType(typeof(SolicitudDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(
        Guid id,
        [FromBody] SolicitudCambiarEstado solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!ParseoContratos.TryParseEnum<EstadoSolicitud>(solicitud.Estado, out var estado))
        {
            return CampoInvalido("estado", "El estado de la solicitud no es válido.");
        }

        var resultado = await _cambiarEstado.HandleAsync(
            new ComandoCambiarEstado(id, estado, solicitud.Motivo),
            cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(
                resultado,
                HttpContext,
                ProblemasSolicitud.SlugConflicto(resultado.Mensaje));
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
