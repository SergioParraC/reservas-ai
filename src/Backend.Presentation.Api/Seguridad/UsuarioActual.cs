using System.Security.Claims;
using Backend.Application.Interfaces;
using Backend.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;

namespace Backend.Presentation.Api.Seguridad;

/// <summary>
/// Expone la identidad del portador del token ya validado, sin tipos de los esquemas de autenticación.
/// </summary>
public sealed class UsuarioActual : IUsuarioActual
{
    private readonly IHttpContextAccessor _acceso;

    /// <summary>Inicializa el adaptador con el acceso al contexto HTTP.</summary>
    public UsuarioActual(IHttpContextAccessor acceso)
    {
        _acceso = acceso ?? throw new ArgumentNullException(nameof(acceso));
    }

    private ClaimsPrincipal? Principal => _acceso.HttpContext?.User;

    /// <inheritdoc />
    public bool EstaAutenticado => Principal?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public Guid? UsuarioId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTipos.Sub), out var identificador) ? identificador : null;

    /// <inheritdoc />
    public string? CorreoInstitucional => Principal?.FindFirstValue(ClaimTipos.Email);

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTipos.Roles).Select(claim => claim.Value).ToArray() ?? [];
}
