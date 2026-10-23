using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Entities;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de escritura del agregado <see cref="Espacio"/>: carga con sus puestos,
/// alta, verificación de unicidad y búsqueda de puestos para la descarga de QR.
/// </summary>
public interface IEspaciosRepository
{
    /// <summary>Carga un área con sus puestos o devuelve <see langword="null"/>.</summary>
    Task<Espacio?> ObtenerAsync(Guid espacioId, CancellationToken cancellationToken = default);

    /// <summary>Carga las áreas indicadas con sus puestos, sin duplicados.</summary>
    Task<IReadOnlyList<Espacio>> ObtenerVariosAsync(IReadOnlyCollection<Guid> espacioIds, CancellationToken cancellationToken = default);

    /// <summary>Busca un puesto por su identificador interno, con el área que lo contiene.</summary>
    Task<Puesto?> ObtenerPuestoAsync(Guid puestoId, CancellationToken cancellationToken = default);

    /// <summary>Indica si ya existe un área con el mismo nombre en la misma ubicación.</summary>
    Task<bool> ExisteNombreUbicacionAsync(string nombre, string ubicacion, Guid? excluirEspacioId, CancellationToken cancellationToken = default);

    /// <summary>Indica si el código QR ya está asignado a un área o a un puesto.</summary>
    Task<bool> ExisteCodigoQrAsync(string codigoQr, CancellationToken cancellationToken = default);

    /// <summary>Agrega un área nueva a la unidad de trabajo.</summary>
    Task AgregarAsync(Espacio espacio, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma los cambios pendientes. Devuelve <see langword="false"/> ante un conflicto
    /// de índice único o de concurrencia optimista, sin filtrar tipos del ORM a la aplicación.
    /// </summary>
    Task<bool> GuardarCambiosAsync(CancellationToken cancellationToken = default);
}
