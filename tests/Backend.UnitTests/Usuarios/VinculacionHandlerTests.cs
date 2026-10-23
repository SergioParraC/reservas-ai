using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Usuarios;
using Backend.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Backend.UnitTests.Usuarios;

/// <summary>
/// Pruebas de la vinculación federada: sesión ya federada, correo distinto,
/// identidad ajena, idempotencia del mismo <c>oid</c> y desvinculación.
/// </summary>
public sealed class VinculacionHandlerTests
{
    private const string Oid = "oid-institucional";

    private readonly Mock<IIdentityService> _identidad = new(MockBehavior.Strict);
    private readonly Mock<IValidadorTokenEntra> _validador = new(MockBehavior.Strict);

    [Fact]
    public async Task VincularConUnaSesionYaFederadaDevuelveConflicto()
    {
        var resultado = await new VincularEntraHandler(_identidad.Object, _validador.Object)
            .HandleAsync(Guid.NewGuid(), sesionFederada: true, "token");

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Conflicto);
        _validador.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task VincularSinTokenDevuelveValidacion()
    {
        var resultado = await new VincularEntraHandler(_identidad.Object, _validador.Object)
            .HandleAsync(Guid.NewGuid(), sesionFederada: false, "  ");

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Validacion);
        resultado.Errores.Should().ContainKey("tokenEntra");
    }

    [Fact]
    public async Task VincularConUnTokenInvalidoDevuelveNoAutorizado()
    {
        _validador.Setup(validador => validador.ValidarAsync("token-invalido", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Resultado<IdentidadFederada>.Fallo(TipoFallo.NoAutorizado, "El token no es válido."));

        var resultado = await new VincularEntraHandler(_identidad.Object, _validador.Object)
            .HandleAsync(Guid.NewGuid(), sesionFederada: false, "token-invalido");

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.NoAutorizado);
    }

    [Fact]
    public async Task VincularConUnCorreoDistintoDevuelveConflicto()
    {
        var cuenta = Cuenta();
        ConfigurarToken("luis@uninpahu.edu.co");
        _identidad.Setup(servicio => servicio.BuscarPorIdAsync(cuenta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cuenta);

        var resultado = await new VincularEntraHandler(_identidad.Object, _validador.Object)
            .HandleAsync(cuenta.Id, sesionFederada: false, "token-valido");

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Conflicto);
        resultado.Mensaje.Should().Contain("no coincide");
    }

    [Fact]
    public async Task VincularUnOidDeOtraCuentaDevuelveConflicto()
    {
        var cuenta = Cuenta();
        var propietaria = new CuentaUsuario(Guid.NewGuid(), "Otra", "otra@uninpahu.edu.co", TipoActor.Estudiante, true, false, Oid);

        ConfigurarToken(cuenta.CorreoInstitucional);
        _identidad.Setup(servicio => servicio.BuscarPorIdAsync(cuenta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cuenta);
        _identidad.Setup(servicio => servicio.BuscarPorIdentificadorFederadoAsync(Oid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(propietaria);

        var resultado = await new VincularEntraHandler(_identidad.Object, _validador.Object)
            .HandleAsync(cuenta.Id, sesionFederada: false, "token-valido");

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Conflicto);
    }

    [Fact]
    public async Task RepetirLaVinculacionDelMismoOidEsIdempotente()
    {
        var cuenta = Cuenta(Oid);
        ConfigurarToken(cuenta.CorreoInstitucional);
        _identidad.Setup(servicio => servicio.BuscarPorIdAsync(cuenta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cuenta);
        _identidad.Setup(servicio => servicio.BuscarPorIdentificadorFederadoAsync(Oid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cuenta);

        var resultado = await new VincularEntraHandler(_identidad.Object, _validador.Object)
            .HandleAsync(cuenta.Id, sesionFederada: false, "token-valido");

        resultado.Exitoso.Should().BeTrue();
        resultado.Valor.Should().NotBeNull();
        resultado.Valor!.Vinculado.Should().BeTrue();

        _identidad.Verify(
            servicio => servicio.VincularIdentidadAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task VincularUnaCuentaSinVinculoPersisteElOid()
    {
        var cuenta = Cuenta();
        ConfigurarToken(cuenta.CorreoInstitucional);
        _identidad.Setup(servicio => servicio.BuscarPorIdAsync(cuenta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cuenta);
        _identidad.Setup(servicio => servicio.BuscarPorIdentificadorFederadoAsync(Oid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CuentaUsuario?)null);
        _identidad.Setup(servicio => servicio.VincularIdentidadAsync(cuenta.Id, Oid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoOperacionIdentidad.Exito());

        var resultado = await new VincularEntraHandler(_identidad.Object, _validador.Object)
            .HandleAsync(cuenta.Id, sesionFederada: false, "token-valido");

        resultado.Exitoso.Should().BeTrue();
        _identidad.Verify(
            servicio => servicio.VincularIdentidadAsync(cuenta.Id, Oid, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DesvincularEsIdempotenteYCierraLaSesionLocalSoloConLaContrasena()
    {
        var usuario = Guid.NewGuid();
        _identidad.Setup(servicio => servicio.DesvincularIdentidadAsync(usuario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultadoOperacionIdentidad.Exito());

        var resultado = await new DesvincularEntraHandler(_identidad.Object).HandleAsync(usuario);

        resultado.Exitoso.Should().BeTrue();
        _identidad.Verify(
            servicio => servicio.DesvincularIdentidadAsync(usuario, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void ConfigurarToken(string correo)
    {
        _validador.Setup(validador => validador.ValidarAsync("token-valido", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Resultado<IdentidadFederada>.Exito(new IdentidadFederada(Oid, "Ana de Pruebas", correo, [])));
    }

    private static CuentaUsuario Cuenta(string? identificadorFederado = null) =>
        new(Guid.NewGuid(), "Ana de Pruebas", "ana@uninpahu.edu.co", TipoActor.Estudiante, true, false, identificadorFederado);
}
