using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Presentation.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>Endpoints administrativos protegidos por el rol <c>Administrativo</c>.</summary>
[ApiController]
[Route("api/administracion")]
[Authorize(Roles = RolesAplicacion.Administrativo)]
public sealed class AdministracionController : ControllerBase
{
    private readonly IUsuarioActual _usuarioActual;

    /// <summary>Inicializa el controlador con la identidad del portador.</summary>
    public AdministracionController(IUsuarioActual usuarioActual)
    {
        _usuarioActual = usuarioActual ?? throw new ArgumentNullException(nameof(usuarioActual));
    }

    /// <summary>Verifica que el portador tenga el rol administrativo y devuelve la confirmación.</summary>
    [HttpGet("verificacion")]
    [ProducesResponseType(typeof(RespuestaVerificacion), StatusCodes.Status200OK)]
    public IActionResult Verificacion() =>
        Ok(new RespuestaVerificacion("Acceso administrativo concedido.", _usuarioActual.CorreoInstitucional));
}
