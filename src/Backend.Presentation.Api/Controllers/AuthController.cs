using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Presentation.Api.Contracts;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Endpoints de autenticación local: inicio de sesión y consulta del perfil del portador.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly LoginLocalHandler _login;
    private readonly ObtenerPerfilHandler _perfil;
    private readonly IUsuarioActual _usuarioActual;

    /// <summary>Inicializa el controlador con los casos de uso de autenticación.</summary>
    public AuthController(LoginLocalHandler login, ObtenerPerfilHandler perfil, IUsuarioActual usuarioActual)
    {
        _login = login ?? throw new ArgumentNullException(nameof(login));
        _perfil = perfil ?? throw new ArgumentNullException(nameof(perfil));
        _usuarioActual = usuarioActual ?? throw new ArgumentNullException(nameof(usuarioActual));
    }

    /// <summary>Inicia sesión con credenciales locales y devuelve el token emitido.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenEmitido), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] SolicitudLogin solicitud, CancellationToken cancellationToken)
    {
        var resultado = await _login.HandleAsync(
            new CredencialesLocales(solicitud.CorreoInstitucional, solicitud.Contrasena),
            cancellationToken);

        return resultado is { Exitoso: true, Token: { } token }
            ? Ok(token)
            : CatalogoProblemas.DesdeError(resultado.Error, HttpContext);
    }

    /// <summary>Devuelve el perfil del portador del token vigente.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(PerfilUsuario), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (_usuarioActual.UsuarioId is not { } usuarioId)
        {
            return CatalogoProblemas.DesdeError(ErrorAutenticacion.TokenInvalido, HttpContext);
        }

        var perfil = await _perfil.HandleAsync(usuarioId, cancellationToken);

        return perfil is null
            ? CatalogoProblemas.DesdeError(ErrorAutenticacion.PerfilInexistente, HttpContext)
            : Ok(perfil);
    }
}
