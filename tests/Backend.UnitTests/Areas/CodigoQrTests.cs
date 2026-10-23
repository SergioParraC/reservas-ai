using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;
using FluentAssertions;

namespace Backend.UnitTests.Areas;

/// <summary>
/// Pruebas del identificador QR: formato Crockford Base32, longitud fija,
/// ausencia de colisiones en una muestra amplia y rechazo de valores inválidos.
/// </summary>
public sealed class CodigoQrTests
{
    [Fact]
    public void GenerarProduceElFormatoInstitucionalDe29Caracteres()
    {
        var codigo = CodigoQR.Generar();

        codigo.Valor.Should().StartWith(CodigoQR.Prefijo);
        codigo.Valor.Should().HaveLength(CodigoQR.Longitud);
        codigo.Valor[CodigoQR.Prefijo.Length..]
            .All(caracter => CodigoQR.Alfabeto.Contains(caracter, StringComparison.Ordinal))
            .Should().BeTrue();
        codigo.Valor.Should().NotContainAny("I", "L", "O", "U");
    }

    [Fact]
    public void DiezMilCodigosGeneradosNoColisionan()
    {
        var codigos = Enumerable.Range(0, 10_000).Select(_ => CodigoQR.Generar().Valor).ToArray();

        codigos.Distinct(StringComparer.Ordinal).Should().HaveCount(codigos.Length);
    }

    [Fact]
    public void DesdeRehidrataUnCodigoValidoYNormalizaMinusculas()
    {
        var original = CodigoQR.Generar();

        var rehidratado = CodigoQR.Desde(original.Valor.ToLowerInvariant());

        rehidratado.Should().Be(original);
        rehidratado.Valor.Should().Be(original.Valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("QR-123")]
    [InlineData("XX-0123456789ABCDEFGHJKMNPQRS")]
    [InlineData("QR-0123456789ABCDEFGHJKMNPQRL")]
    public void DesdeRechazaValoresFueraDelFormato(string valor)
    {
        var accion = () => CodigoQR.Desde(valor);

        accion.Should().Throw<DomainException>().WithMessage(MensajesEspacio.CodigoQrInvalido);
    }

    [Fact]
    public void DosInstanciasConElMismoValorSonIguales()
    {
        var original = CodigoQR.Generar();

        (original == CodigoQR.Desde(original.Valor)).Should().BeTrue();
        original.GetHashCode().Should().Be(CodigoQR.Desde(original.Valor).GetHashCode());
    }
}
