using System.Text.Json;
using Backend.Application.Autenticacion;
using Backend.Domain.Enums;
using Backend.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Backend.UnitTests.Seguridad;

/// <summary>
/// Pruebas de emisión del access token local: contrato de claims, vigencia absoluta
/// y validación de la configuración de firma.
/// </summary>
public sealed class JwtTokenServiceTests
{
    private const string ClaveValida = "clave-de-pruebas-suficientemente-larga-para-hmac-256";
    private const string Emisor = "https://api.reservas-espacios.edu";
    private const string Audiencia = "reservas-espacios-spa";

    private static readonly DateTimeOffset EmitidoEn = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly string[] ClaimsEsperados =
        ["sub", "jti", "email", "name", "roles", "iss", "aud", "iat", "exp"];

    [Fact]
    public void EmiteUnTokenConLosClaimsYLaExpiracionEsperados()
    {
        var id = Guid.NewGuid();
        var perfil = new PerfilUsuario(
            id,
            "Ana Administrativa",
            "ana@universidad.edu",
            TipoActor.Administrativo,
            ["Administrativo", "Docente"]);

        var token = CrearServicio().Emitir(perfil, EmitidoEn);

        using var payload = LeerPayload(token.Token);
        var raiz = payload.RootElement;

        raiz.EnumerateObject().Select(propiedad => propiedad.Name)
            .Should().BeEquivalentTo(ClaimsEsperados);

        raiz.GetProperty("sub").GetString().Should().Be(id.ToString());
        raiz.GetProperty("email").GetString().Should().Be("ana@universidad.edu");
        raiz.GetProperty("name").GetString().Should().Be("Ana Administrativa");
        raiz.GetProperty("iss").GetString().Should().Be(Emisor);
        raiz.GetProperty("aud").GetString().Should().Be(Audiencia);

        raiz.GetProperty("roles").EnumerateArray().Select(rol => rol.GetString())
            .Should().BeEquivalentTo(["Administrativo", "Docente"]);

        Guid.TryParse(raiz.GetProperty("jti").GetString(), out var jti).Should().BeTrue();
        jti.Should().NotBe(Guid.Empty);

        raiz.GetProperty("iat").GetInt64().Should().Be(EmitidoEn.ToUnixTimeSeconds());
        raiz.GetProperty("exp").GetInt64().Should().Be(EmitidoEn.AddMinutes(60).ToUnixTimeSeconds());

        token.TipoToken.Should().Be(TokenEmitido.EsquemaBearer);
        token.ExpiraEn.Should().Be(EmitidoEn.AddMinutes(60));
        token.Roles.Should().BeEquivalentTo(["Administrativo", "Docente"]);
    }

    [Fact]
    public void RespetaLaExpiracionConfiguradaEnMinutos()
    {
        var opciones = OpcionesValidas();
        opciones.ExpiraMinutos = 15;

        var token = new JwtTokenService(Options.Create(opciones))
            .Emitir(new PerfilUsuario(Guid.NewGuid(), "Luis Docente", "luis@universidad.edu", TipoActor.Docente, ["Docente"]), EmitidoEn);

        using var payload = LeerPayload(token.Token);

        payload.RootElement.GetProperty("exp").GetInt64()
            .Should().Be(EmitidoEn.AddMinutes(15).ToUnixTimeSeconds());
        token.ExpiraEn.Should().Be(EmitidoEn.AddMinutes(15));
    }

    [Fact]
    public void RechazaUnaClaveDeFirmaDemasiadoCorta()
    {
        var opciones = OpcionesValidas();
        opciones.Key = "corta";

        var crear = () => new JwtTokenService(Options.Create(opciones));

        crear.Should().Throw<InvalidOperationException>().WithMessage("*Jwt:Key*");
    }

    private static JwtTokenService CrearServicio() => new(Options.Create(OpcionesValidas()));

    private static JwtOptions OpcionesValidas() => new()
    {
        Key = ClaveValida,
        Issuer = Emisor,
        Audience = Audiencia,
        ExpiraMinutos = JwtOptions.ExpiraMinutosPorDefecto
    };

    private static JsonDocument LeerPayload(string token)
    {
        var segmentos = token.Split('.');
        segmentos.Should().HaveCount(3);

        return JsonDocument.Parse(Base64UrlEncoder.Decode(segmentos[1]));
    }
}
