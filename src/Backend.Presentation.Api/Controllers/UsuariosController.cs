using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Usuarios;
using Backend.Application.Modules.Usuarios.Dtos;
using Backend.Infrastructure.Identity;
using Backend.Presentation.Api.Contracts;
using Backend.Presentation.Api.Problemas;
using Backend.Presentation.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Autorregistro, vinculación con Entra ID y consola administrativa de cuentas y roles.
/// </summary>
[ApiController]
[Route("api/usuarios")]
public sealed class UsuariosController : ControllerBase
{
    private readonly RegistrarUsuarioHandler _registrar;
    private readonly VincularEntraHandler _vincular;
    private readonly DesvincularEntraHandler _desvincular;
    private readonly ConsultarUsuariosHandler _consultar;
    private readonly CambiarRolHandler _cambiarRol;
    private readonly IUsuarioActual _usuarioActual;

    /// <summary>Inicializa el controlador con los casos de uso del módulo de usuarios.</summary>
    public UsuariosController(
        RegistrarUsuarioHandler registrar,
        VincularEntraHandler vincular,
        DesvincularEntraHandler desvincular,
        ConsultarUsuariosHandler consultar,
        CambiarRolHandler cambiarRol,
        IUsuarioActual usuarioActual)
    {
        _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
        _vincular = vincular ?? throw new ArgumentNullException(nameof(vincular));
        _desvincular = desvincular ?? throw new ArgumentNullException(nameof(desvincular));
        _consultar = consultar ?? throw new ArgumentNullException(nameof(consultar));
        _cambiarRol = cambiarRol ?? throw new ArgumentNullException(nameof(cambiarRol));
        _usuarioActual = usuarioActual ?? throw new ArgumentNullException(nameof(usuarioActual));
    }

    /// <summary>Autorregistro público: la primera cuenta recibe el rol administrativo.</summary>
    [HttpPost("registro")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionTasaExtensions.PoliticaRegistro)]
    [ProducesResponseType(typeof(ResultadoRegistroUsuario), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Registro([FromBody] SolicitudRegistroUsuario solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var resultado = await _registrar.HandleAsync(
            new RegistroCuentaLocal(solicitud.NombreCompleto, solicitud.CorreoInstitucional, solicitud.Contrasena),
            cancellationToken);

        return resultado is { Exitoso: true, Valor: { } registrada }
            ? StatusCode(StatusCodes.Status201Created, registrada)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Vincula la cuenta autenticada con su identidad institucional de Entra ID.</summary>
    [HttpPost("me/vinculacion-entra")]
    [Authorize]
    [ProducesResponseType(typeof(EstadoVinculacion), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Vincular([FromBody] SolicitudVinculacionEntra solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (_usuarioActual.UsuarioId is not { } usuarioId)
        {
            return SinSesion();
        }

        var resultado = await _vincular.HandleAsync(usuarioId, EsSesionFederada(), solicitud.TokenEntra, cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Desvincula la identidad institucional de la cuenta autenticada.</summary>
    [HttpDelete("me/vinculacion-entra")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Desvincular(CancellationToken cancellationToken)
    {
        if (_usuarioActual.UsuarioId is not { } usuarioId)
        {
            return SinSesion();
        }

        var resultado = await _desvincular.HandleAsync(usuarioId, cancellationToken);

        return resultado.Exitoso
            ? NoContent()
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Lista las cuentas registradas; exclusivo del rol administrativo.</summary>
    [HttpGet]
    [Authorize(Roles = RolesAplicacion.Administrativo)]
    [ProducesResponseType(typeof(PaginaResultado<UsuarioAdministrativo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? q,
        [FromQuery] string? rol,
        [FromQuery] bool? vinculado,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 20,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _consultar.HandleAsync(
            new FiltroUsuarios(q, rol, vinculado, pagina, tamano),
            cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Cambia el rol vigente de una cuenta; exclusivo del rol administrativo.</summary>
    [HttpPut("{id:guid}/rol")]
    [Authorize(Roles = RolesAplicacion.Administrativo)]
    [ProducesResponseType(typeof(ResultadoCambioRol), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarRol(Guid id, [FromBody] SolicitudCambiarRol solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (_usuarioActual.UsuarioId is not { } portadorId)
        {
            return SinSesion();
        }

        var resultado = await _cambiarRol.HandleAsync(id, solicitud.Rol, portadorId, cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    private bool EsSesionFederada() =>
        string.Equals(
            User.FindFirst(ClaimTipos.Esquema)?.Value,
            AutenticacionHibridaExtensions.EsquemaEntraId,
            StringComparison.Ordinal);

    private ObjectResult SinSesion() =>
        CatalogoProblemas.Crear(
            StatusCodes.Status401Unauthorized,
            "token-ausente",
            "Token ausente",
            "La solicitud requiere autenticación.",
            HttpContext);
}
