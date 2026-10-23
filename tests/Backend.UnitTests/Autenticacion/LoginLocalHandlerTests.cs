using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Backend.UnitTests.Autenticacion;

/// <summary>
/// Pruebas del caso de uso de inicio de sesión local: cada estado de credenciales
/// debe producir un resultado distinto y nunca un token cuando el acceso se rechaza.
/// </summary>
public sealed class LoginLocalHandlerTests
{
    private static readonly DateTimeOffset MomentoActual = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IIdentityService> _identidad = new(MockBehavior.Strict);
    private readonly Mock<ITokenService> _tokens = new(MockBehavior.Strict);
    private readonly Mock<IRelojSistema> _reloj = new(MockBehavior.Strict);

    [Fact]
    public async Task CredencialesValidasEmiteTokenConRolesYRegistraUltimoAcceso()
    {
        var cuenta = CuentaAdministrativa();
        var credenciales = new CredencialesLocales(cuenta.CorreoInstitucional, "Clave.2026");
        var token = TokenEmitido.Bearer("jwt-simulado", MomentoActual.AddMinutes(60), ["Administrativo"]);

        _identidad.Setup(x => x.VerificarCredencialesAsync(credenciales, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoCredenciales.Exito(cuenta));
        _identidad.Setup(x => x.ObtenerRolesAsync(cuenta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Administrativo"]);
        _identidad.Setup(x => x.ActualizarUltimoAccesoAsync(cuenta.Id, MomentoActual, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _reloj.SetupGet(x => x.Ahora).Returns(MomentoActual);
        _tokens.Setup(x => x.Emitir(It.IsAny<PerfilUsuario>(), MomentoActual)).Returns(token);

        var resultado = await CrearHandler().HandleAsync(credenciales);

        resultado.Exitoso.Should().BeTrue();
        resultado.Error.Should().Be(ErrorAutenticacion.Ninguno);
        resultado.Token.Should().Be(token);
        resultado.Perfil.Should().NotBeNull();
        resultado.Perfil!.Id.Should().Be(cuenta.Id);
        resultado.Perfil.CorreoInstitucional.Should().Be(cuenta.CorreoInstitucional);
        resultado.Perfil.TipoActor.Should().Be(TipoActor.Administrativo);
        resultado.Perfil.Roles.Should().Equal("Administrativo");

        _identidad.Verify(x => x.ActualizarUltimoAccesoAsync(cuenta.Id, MomentoActual, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ContrasenaIncorrectaDevuelveCredencialesInvalidasSinEmitirToken()
    {
        var credenciales = new CredencialesLocales("ana@uninpahu.edu.co", "clave-erronea");

        _identidad.Setup(x => x.VerificarCredencialesAsync(credenciales, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoCredenciales.Fallo(EstadoCredenciales.CredencialesInvalidas));

        var resultado = await CrearHandler().HandleAsync(credenciales);

        resultado.Exitoso.Should().BeFalse();
        resultado.Error.Should().Be(ErrorAutenticacion.CredencialesInvalidas);
        resultado.Token.Should().BeNull();
        resultado.Perfil.Should().BeNull();

        _identidad.Verify(x => x.ObtenerRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _identidad.Verify(
            x => x.ActualizarUltimoAccesoAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _tokens.Verify(x => x.Emitir(It.IsAny<PerfilUsuario>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task UsuarioInexistenteDevuelveElMismoErrorGenericoQueUnaContrasenaIncorrecta()
    {
        var credenciales = new CredencialesLocales("desconocido@uninpahu.edu.co", "cualquier-clave");

        _identidad.Setup(x => x.VerificarCredencialesAsync(credenciales, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoCredenciales.Fallo(EstadoCredenciales.CredencialesInvalidas));

        var resultado = await CrearHandler().HandleAsync(credenciales);

        resultado.Exitoso.Should().BeFalse();
        resultado.Error.Should().Be(ErrorAutenticacion.CredencialesInvalidas);
        resultado.Token.Should().BeNull();

        _tokens.Verify(x => x.Emitir(It.IsAny<PerfilUsuario>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task CuentaInactivaDevuelveCuentaInactivaSinEmitirToken()
    {
        var credenciales = new CredencialesLocales("inactiva@uninpahu.edu.co", "Clave.2026");

        _identidad.Setup(x => x.VerificarCredencialesAsync(credenciales, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoCredenciales.Fallo(EstadoCredenciales.CuentaInactiva));

        var resultado = await CrearHandler().HandleAsync(credenciales);

        resultado.Exitoso.Should().BeFalse();
        resultado.Error.Should().Be(ErrorAutenticacion.CuentaInactiva);
        resultado.Token.Should().BeNull();

        _tokens.Verify(x => x.Emitir(It.IsAny<PerfilUsuario>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task CuentaBloqueadaDevuelveCuentaBloqueadaSinEmitirToken()
    {
        var credenciales = new CredencialesLocales("bloqueada@uninpahu.edu.co", "Clave.2026");

        _identidad.Setup(x => x.VerificarCredencialesAsync(credenciales, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoCredenciales.Fallo(EstadoCredenciales.CuentaBloqueada));

        var resultado = await CrearHandler().HandleAsync(credenciales);

        resultado.Exitoso.Should().BeFalse();
        resultado.Error.Should().Be(ErrorAutenticacion.CuentaBloqueada);
        resultado.Token.Should().BeNull();

        _tokens.Verify(x => x.Emitir(It.IsAny<PerfilUsuario>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    private static CuentaUsuario CuentaAdministrativa() =>
        new(Guid.NewGuid(), "Ana Administrativa", "ana@uninpahu.edu.co", TipoActor.Administrativo, Activo: true, Bloqueada: false);

    private LoginLocalHandler CrearHandler() => new(_identidad.Object, _tokens.Object, _reloj.Object);
}
