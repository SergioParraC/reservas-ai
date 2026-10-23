using System.Security.Cryptography;
using System.Text;
using Backend.Domain.Exceptions;

namespace Backend.Domain.ValueObjects;

/// <summary>
/// Identificador opaco e inmutable de una unidad física impreso en su placa QR.
/// Se genera una sola vez con 128 bits aleatorios y se codifica en Crockford Base32
/// para que pueda transcribirse sin ambigüedad.
/// </summary>
public sealed record CodigoQR
{
    /// <summary>Prefijo institucional fijo del código.</summary>
    public const string Prefijo = "QR-";

    /// <summary>Longitud total del código: prefijo más 26 caracteres Base32.</summary>
    public const int Longitud = 29;

    /// <summary>Alfabeto Crockford Base32: sin las letras I, L, O ni U.</summary>
    public const string Alfabeto = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private const int CaracteresBase32 = 26;

    private CodigoQR(string valor) => Valor = valor;

    /// <summary>Código completo, por ejemplo <c>QR-7T3K9M2XQ4ZP8V6BN1C5HJ0WRS</c>.</summary>
    public string Valor { get; }

    /// <summary>Genera un código nuevo a partir de 128 bits criptográficamente seguros.</summary>
    public static CodigoQR Generar()
    {
        var datos = RandomNumberGenerator.GetBytes(16);
        var texto = new StringBuilder(CaracteresBase32);
        var acumulado = 0;
        var bits = 0;

        foreach (var octeto in datos)
        {
            acumulado = (acumulado << 8) | octeto;
            bits += 8;

            while (bits >= 5)
            {
                bits -= 5;
                texto.Append(Alfabeto[(acumulado >> bits) & 0b11111]);
            }
        }

        if (bits > 0)
        {
            texto.Append(Alfabeto[(acumulado << (5 - bits)) & 0b11111]);
        }

        return new CodigoQR(Prefijo + texto);
    }

    /// <summary>Rehidrata y valida un código ya persistido o recibido.</summary>
    public static CodigoQR Desde(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException(MensajesEspacio.CodigoQrInvalido);
        }

        var normalizado = valor.Trim().ToUpperInvariant();

        if (normalizado.Length != Longitud
            || !normalizado.StartsWith(Prefijo, StringComparison.Ordinal)
            || normalizado[Prefijo.Length..].Any(caracter => !Alfabeto.Contains(caracter, StringComparison.Ordinal)))
        {
            throw new DomainException(MensajesEspacio.CodigoQrInvalido);
        }

        return new CodigoQR(normalizado);
    }

    /// <inheritdoc />
    public override string ToString() => Valor;
}
