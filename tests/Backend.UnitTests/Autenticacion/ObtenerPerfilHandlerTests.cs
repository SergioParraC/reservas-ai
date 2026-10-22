using Backend.Application.Autenticacion;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Backend.UnitTests.Autenticacion;

/// <summary>
/// Pruebas del caso de uso de consulta de perfil: solo datos de negocio,
/// sin contraseñas, hashes ni sellos de seguridad del almacén de identidad.
/// </summary>
public sealed class ObtenerPerfilHandlerTests
{
    private readonly Mock<IIdentityService> _identidad = new(MockBehavior.Strict);

    [Fact]
    public async Task DevuelveNombreCorreoTipoDeActorYRoles()
    {
        var cuenta = CuentaDocente();

        _identidad.Setup(x => x.BuscarPorIdAsync(cuenta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cuenta);
        _identidad.Setup(x => x.ObtenerRolesAsync(cuenta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(["Docente"]);

        var perfil = await new ObtenerPerfilHandler(_identidad.Object).HandleAsync(cuenta.Id);

        perfil.Should().NotBeNull();
        perfil!.Id.Should().Be(cuenta.Id);
        perfil.NombreCompleto.Should().Be("Luis Docente");
        perfil.CorreoInstitucional.Should().Be("luis@universidad.edu");
        perfil.TipoActor.Should().Be(TipoActor.Docente);
        perfil.Roles.Should().Equal("Docente");
    }

    [Fact]
    public void ElPerfilNoExponeDatosDeSeguridadDelAlmacenDeIdentidad()
    {
        var propiedades = typeof(PerfilUsuario).GetProperties().Select(propiedad => propiedad.Name).ToArray();

        propiedades.Should().BeEquivalentTo(["Id", "NombreCompleto", "CorreoInstitucional", "TipoActor", "Roles"]);
        propiedades.Should().NotContain(nombre =>
            nombre.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Hash", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Stamp", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Bloque", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DevuelveNuloCuandoLaCuentaNoExiste()
    {
        var usuarioId = Guid.NewGuid();

        _identidad.Setup(x => x.BuscarPorIdAsync(usuarioId, It.IsAny<CancellationToken>())).ReturnsAsync((CuentaUsuario?)null);

        var perfil = await new ObtenerPerfilHandler(_identidad.Object).HandleAsync(usuarioId);

        perfil.Should().BeNull();
    }

    [Fact]
    public async Task DevuelveNuloCuandoLaCuentaEstaInactiva()
    {
        var cuenta = CuentaDocente() with { Activo = false };

        _identidad.Setup(x => x.BuscarPorIdAsync(cuenta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cuenta);

        var perfil = await new ObtenerPerfilHandler(_identidad.Object).HandleAsync(cuenta.Id);

        perfil.Should().BeNull();
        _identidad.Verify(x => x.ObtenerRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CuentaUsuario CuentaDocente() =>
        new(Guid.NewGuid(), "Luis Docente", "luis@universidad.edu", TipoActor.Docente, Activo: true, Bloqueada: false);
}
