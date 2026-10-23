using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Usuarios;
using Backend.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Backend.UnitTests.Usuarios;

/// <summary>
/// Pruebas del cambio de rol: autoedición, rol no soportado, cuenta inexistente,
/// último administrador activo y respuesta con renovación de sesión.
/// </summary>
public sealed class CambiarRolHandlerTests
{
    private readonly Mock<IIdentityService> _identidad = new(MockBehavior.Strict);

    [Fact]
    public async Task CambiarElPropioRolDevuelveConflictoSinConsultarElAlmacen()
    {
        var identificador = Guid.NewGuid();

        var resultado = await new CambiarRolHandler(_identidad.Object)
            .HandleAsync(identificador, RolesAplicacion.Docente, identificador);

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Conflicto);
        _identidad.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnRolNoSoportadoDevuelveValidacion()
    {
        var resultado = await new CambiarRolHandler(_identidad.Object)
            .HandleAsync(Guid.NewGuid(), "Superusuario", Guid.NewGuid());

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Validacion);
        resultado.Errores.Should().ContainKey("rol");
    }

    [Fact]
    public async Task UnaCuentaInexistenteDevuelveNoEncontrado()
    {
        var objetivo = Guid.NewGuid();
        _identidad.Setup(servicio => servicio.BuscarPorIdAsync(objetivo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CuentaUsuario?)null);

        var resultado = await new CambiarRolHandler(_identidad.Object)
            .HandleAsync(objetivo, RolesAplicacion.Docente, Guid.NewGuid());

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.NoEncontrado);
    }

    [Fact]
    public async Task DegradarAlUltimoAdministradorDevuelveConflicto()
    {
        var objetivo = Cuenta(RolesAplicacion.Administrativo);
        _identidad.Setup(servicio => servicio.BuscarPorIdAsync(objetivo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(objetivo);
        _identidad.Setup(servicio => servicio.ObtenerRolesAsync(objetivo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([RolesAplicacion.Administrativo]);
        _identidad.Setup(servicio => servicio.ContarAdministradoresActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var resultado = await new CambiarRolHandler(_identidad.Object)
            .HandleAsync(objetivo.Id, RolesAplicacion.Docente, Guid.NewGuid());

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Conflicto);
        resultado.Mensaje.Should().Contain("administrador activo");

        _identidad.Verify(
            servicio => servicio.CambiarRolAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CambiarElRolDeUnaCuentaDevuelveRequiereRenovacion()
    {
        var objetivo = Cuenta(RolesAplicacion.Estudiante);
        _identidad.Setup(servicio => servicio.BuscarPorIdAsync(objetivo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(objetivo);
        _identidad.Setup(servicio => servicio.ObtenerRolesAsync(objetivo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([RolesAplicacion.Estudiante]);
        _identidad.Setup(servicio => servicio.CambiarRolAsync(objetivo.Id, RolesAplicacion.Docente, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoOperacionIdentidad.Exito());

        var resultado = await new CambiarRolHandler(_identidad.Object)
            .HandleAsync(objetivo.Id, "docente", Guid.NewGuid());

        resultado.Exitoso.Should().BeTrue();
        resultado.Valor.Should().NotBeNull();
        resultado.Valor!.Rol.Should().Be(RolesAplicacion.Docente);
        resultado.Valor.RequiereRenovacion.Should().BeTrue();
    }

    private static CuentaUsuario Cuenta(string rol)
    {
        var tipoActor = string.Equals(rol, RolesAplicacion.Administrativo, StringComparison.Ordinal)
            ? TipoActor.Administrativo
            : string.Equals(rol, RolesAplicacion.Docente, StringComparison.Ordinal)
                ? TipoActor.Docente
                : TipoActor.Estudiante;

        return new CuentaUsuario(Guid.NewGuid(), "Ana de Pruebas", "ana@uninpahu.edu.co", tipoActor, true, false);
    }
}
