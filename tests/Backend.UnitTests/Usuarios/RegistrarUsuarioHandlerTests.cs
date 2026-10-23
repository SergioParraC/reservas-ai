using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Usuarios;
using Backend.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Backend.UnitTests.Usuarios;

/// <summary>
/// Pruebas del autorregistro: política de dominio, correo duplicado, autorregistro
/// deshabilitado, contraseña rechazada por Identity y rol devuelto por el almacén.
/// </summary>
public sealed class RegistrarUsuarioHandlerTests
{
    private readonly Mock<IIdentityService> _identidad = new(MockBehavior.Strict);

    [Fact]
    public async Task ElPrimerRegistroRecibeElRolAdministrativoSinEmitirToken()
    {
        var cuenta = new CuentaUsuario(Guid.NewGuid(), "Ana Administrativa", "ana@uninpahu.edu.co", TipoActor.Administrativo, true, false);

        _identidad.Setup(servicio => servicio.BuscarPorCorreoAsync("ana@uninpahu.edu.co", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CuentaUsuario?)null);
        _identidad.Setup(servicio => servicio.RegistrarCuentaLocalAsync(It.IsAny<RegistroCuentaLocal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoCreacionCuenta.Exito(cuenta, RolesAplicacion.Administrativo));

        var resultado = await CrearHandler().HandleAsync(
            new RegistroCuentaLocal("Ana Administrativa", "ana@uninpahu.edu.co", "Clave.Segura.2026"));

        resultado.Exitoso.Should().BeTrue();
        resultado.Valor.Should().NotBeNull();
        resultado.Valor!.Rol.Should().Be(RolesAplicacion.Administrativo);
        resultado.Valor.Id.Should().Be(cuenta.Id);
    }

    [Fact]
    public async Task UnCorreoDeOtroDominioSeRechazaConValidacion()
    {
        var resultado = await CrearHandler().HandleAsync(
            new RegistroCuentaLocal("Ana", "ana@gmail.com", "Clave.Segura.2026"));

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Validacion);
        resultado.Errores.Should().ContainKey("correoInstitucional");

        _identidad.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnDominioConfiguradoConArrobaAceptaElCorreoInstitucional()
    {
        var cuenta = new CuentaUsuario(Guid.NewGuid(), "Ana", "ana@uninpahu.edu.co", TipoActor.Estudiante, true, false);

        _identidad.Setup(servicio => servicio.BuscarPorCorreoAsync("ana@uninpahu.edu.co", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CuentaUsuario?)null);
        _identidad.Setup(servicio => servicio.RegistrarCuentaLocalAsync(It.IsAny<RegistroCuentaLocal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoCreacionCuenta.Exito(cuenta, RolesAplicacion.Estudiante));

        var handler = new RegistrarUsuarioHandler(
            _identidad.Object,
            new OpcionesIdentidad
            {
                PermitirAutorregistro = true,
                DominioCorreoInstitucional = " @UNINPAHU.edu.co "
            });

        var resultado = await handler.HandleAsync(
            new RegistroCuentaLocal("Ana", "ana@uninpahu.edu.co", "Clave.Segura.2026"));

        resultado.Exitoso.Should().BeTrue();
    }

    [Fact]
    public async Task UnCorreoYaRegistradoDevuelveConflicto()
    {
        _identidad.Setup(servicio => servicio.BuscarPorCorreoAsync("ana@uninpahu.edu.co", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CuentaUsuario(Guid.NewGuid(), "Ana", "ana@uninpahu.edu.co", TipoActor.Estudiante, true, false));

        var resultado = await CrearHandler().HandleAsync(
            new RegistroCuentaLocal("Ana", "ana@uninpahu.edu.co", "Clave.Segura.2026"));

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Conflicto);
    }

    [Fact]
    public async Task ElAutorregistroDeshabilitadoDevuelveNoPermitido()
    {
        var handler = new RegistrarUsuarioHandler(
            _identidad.Object,
            new OpcionesIdentidad { PermitirAutorregistro = false, DominioCorreoInstitucional = "uninpahu.edu.co" });

        var resultado = await handler.HandleAsync(
            new RegistroCuentaLocal("Ana", "ana@uninpahu.edu.co", "Clave.Segura.2026"));

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.NoPermitido);
    }

    [Fact]
    public async Task UnaContrasenaRechazadaPorIdentitySeTraduceAValidacion()
    {
        _identidad.Setup(servicio => servicio.BuscarPorCorreoAsync("ana@uninpahu.edu.co", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CuentaUsuario?)null);
        _identidad.Setup(servicio => servicio.RegistrarCuentaLocalAsync(It.IsAny<RegistroCuentaLocal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoCreacionCuenta.Fallo(["Passwords must have at least one non alphanumeric character."]));

        var resultado = await CrearHandler().HandleAsync(
            new RegistroCuentaLocal("Ana", "ana@uninpahu.edu.co", "clave"));

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Validacion);
        resultado.Errores.Should().ContainKey("contrasena");
        resultado.Mensaje.Should().Contain("non alphanumeric");
    }

    private RegistrarUsuarioHandler CrearHandler() =>
        new(_identidad.Object, new OpcionesIdentidad
        {
            PermitirAutorregistro = true,
            DominioCorreoInstitucional = "uninpahu.edu.co"
        });
}
