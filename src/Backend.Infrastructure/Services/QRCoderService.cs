using System.Text;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using QRCoder;

namespace Backend.Infrastructure.Services;

/// <summary>
/// Emisor vectorial de códigos QR. Usa <see cref="QRCodeGenerator"/> solo para calcular
/// la matriz de módulos y compone el dibujo con elementos SVG propios, sin imágenes
/// de mapa de bits ni dependencias gráficas nativas.
/// </summary>
public sealed class QRCoderService : IQrGenerator
{
    private const double AnchoHojaMilimetros = 210;
    private const double AltoHojaMilimetros = 297;
    private const double MargenHojaMilimetros = 10;
    private const double AnchoEtiquetaMilimetros = 60;
    private const double AltoEtiquetaMilimetros = 40;
    private const int ColumnasHoja = 3;
    private const double LadoQrEtiquetaMilimetros = 34;

    /// <inheritdoc />
    public string GenerarSvg(string contenido, OpcionesQr opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        var lado = Math.Max(10, opciones.LadoMilimetros);
        var documento = new StringBuilder(4096);

        documento.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"")
            .Append(Formato(lado))
            .Append("mm\" height=\"")
            .Append(Formato(lado))
            .Append("mm\" role=\"img\" aria-label=\"Código QR\">");

        AgregarModulos(documento, contenido, 0, 0, lado, opciones.ModuloSilencio);

        documento.Append("</svg>");

        return documento.ToString();
    }

    /// <inheritdoc />
    public string GenerarPlantillaSvg(PlantillaQr plantilla)
    {
        ArgumentNullException.ThrowIfNull(plantilla);

        var capacidad = Math.Max(1, plantilla.EtiquetasPorHoja);
        var filas = (int)Math.Ceiling(capacidad / (double)ColumnasHoja);
        var inicio = (plantilla.Hoja - 1) * capacidad;
        var etiquetas = plantilla.Etiquetas.Skip(inicio).Take(capacidad).ToArray();

        var documento = new StringBuilder(16384);

        documento.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"")
            .Append(Formato(AnchoHojaMilimetros))
            .Append("mm\" height=\"")
            .Append(Formato(AltoHojaMilimetros))
            .Append("mm\" viewBox=\"0 0 ")
            .Append(Formato(AnchoHojaMilimetros))
            .Append(' ')
            .Append(Formato(AltoHojaMilimetros))
            .Append("\" role=\"img\" aria-label=\"Plantilla de etiquetas QR\">");

        documento.Append("<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>");

        for (var indice = 0; indice < etiquetas.Length; indice++)
        {
            var columna = indice % ColumnasHoja;
            var fila = indice / ColumnasHoja;

            if (fila >= filas)
            {
                break;
            }

            var x = MargenHojaMilimetros + (columna * AnchoEtiquetaMilimetros);
            var y = MargenHojaMilimetros + (fila * AltoEtiquetaMilimetros);

            AgregarEtiqueta(documento, etiquetas[indice], x, y);
        }

        documento.Append("</svg>");

        return documento.ToString();
    }

    private static void AgregarEtiqueta(StringBuilder documento, EtiquetaQr etiqueta, double x, double y)
    {
        documento.Append("<g>");
        documento.Append("<rect x=\"").Append(Formato(x)).Append("\" y=\"").Append(Formato(y))
            .Append("\" width=\"").Append(Formato(AnchoEtiquetaMilimetros))
            .Append("\" height=\"").Append(Formato(AltoEtiquetaMilimetros))
            .Append("\" fill=\"#FFFFFF\" stroke=\"#B0B0B0\" stroke-width=\"0.2\"/>");

        AgregarModulos(documento, etiqueta.Codigo, x + 3, y + 3, LadoQrEtiquetaMilimetros, 2);

        var textoX = x + 3 + LadoQrEtiquetaMilimetros + 3;

        documento.Append("<text x=\"").Append(Formato(textoX)).Append("\" y=\"").Append(Formato(y + 9))
            .Append("\" font-family=\"sans-serif\" font-size=\"3.2\" fill=\"#111111\">")
            .Append(Escapar(Truncar(etiqueta.Nombre, 18)))
            .Append("</text>");

        documento.Append("<text x=\"").Append(Formato(textoX)).Append("\" y=\"").Append(Formato(y + 15))
            .Append("\" font-family=\"sans-serif\" font-size=\"2.6\" fill=\"#333333\">")
            .Append(Escapar(Truncar(etiqueta.Ubicacion, 22)))
            .Append("</text>");

        if (!string.IsNullOrWhiteSpace(etiqueta.Detalle))
        {
            documento.Append("<text x=\"").Append(Formato(textoX)).Append("\" y=\"").Append(Formato(y + 20))
                .Append("\" font-family=\"sans-serif\" font-size=\"2.4\" fill=\"#555555\">")
                .Append(Escapar(Truncar(etiqueta.Detalle, 24)))
                .Append("</text>");
        }

        documento.Append("<text x=\"").Append(Formato(textoX)).Append("\" y=\"").Append(Formato(y + 33))
            .Append("\" font-family=\"monospace\" font-size=\"2.6\" fill=\"#111111\">")
            .Append(Escapar(etiqueta.Codigo))
            .Append("</text>");

        documento.Append("</g>");
    }

    private static void AgregarModulos(
        StringBuilder documento,
        string contenido,
        double x,
        double y,
        double lado,
        int moduloSilencio)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            throw new ArgumentException("El contenido del código QR es obligatorio.", nameof(contenido));
        }

        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);

        var matriz = datos.ModuleMatrix;
        var silencio = Math.Max(0, moduloSilencio);
        var totalModulos = matriz.Count + (silencio * 2);
        var escala = lado / totalModulos;
        var trazos = new StringBuilder(matriz.Count * 16);

        for (var fila = 0; fila < matriz.Count; fila++)
        {
            for (var columna = 0; columna < matriz[fila].Length; columna++)
            {
                if (!matriz[fila][columna])
                {
                    continue;
                }

                var moduloX = x + ((columna + silencio) * escala);
                var moduloY = y + ((fila + silencio) * escala);

                trazos.Append('M').Append(Formato(moduloX)).Append(' ').Append(Formato(moduloY))
                    .Append('h').Append(Formato(escala))
                    .Append('v').Append(Formato(escala))
                    .Append('h').Append(Formato(-escala))
                    .Append('z');
            }
        }

        documento.Append("<rect x=\"").Append(Formato(x)).Append("\" y=\"").Append(Formato(y))
            .Append("\" width=\"").Append(Formato(lado)).Append("\" height=\"").Append(Formato(lado))
            .Append("\" fill=\"#FFFFFF\"/>");

        documento.Append("<path d=\"").Append(trazos).Append("\" fill=\"#000000\" shape-rendering=\"crispEdges\"/>");
    }

    private static string Formato(double valor) =>
        valor.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string Truncar(string? texto, int maximo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        return texto.Length <= maximo ? texto : texto[..(maximo - 1)] + "…";
    }

    private static string Escapar(string texto) => System.Net.WebUtility.HtmlEncode(texto);
}
