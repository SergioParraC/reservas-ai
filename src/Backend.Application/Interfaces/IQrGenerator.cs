using Backend.Application.Modules.Areas.Dtos;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de emisión vectorial de códigos QR. La implementación usa un codificador
/// de matrices y compone el SVG; la aplicación no conoce la librería concreta.
/// </summary>
public interface IQrGenerator
{
    /// <summary>Genera el documento SVG de un código individual.</summary>
    string GenerarSvg(string contenido, OpcionesQr opciones);

    /// <summary>Genera una hoja A4 con las etiquetas indicadas.</summary>
    string GenerarPlantillaSvg(PlantillaQr plantilla);
}
