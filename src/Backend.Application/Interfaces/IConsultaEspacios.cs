using Backend.Application.Common;
using Backend.Application.Modules.Areas.Dtos;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de lectura de áreas: proyecciones paginadas del listado administrativo
/// y del catálogo público, sin cargar el agregado completo.
/// </summary>
public interface IConsultaEspacios
{
    /// <summary>Lista áreas con filtros administrativos, incluidas las inactivas.</summary>
    Task<PaginaResultado<EspacioResumen>> ListarAdministrativoAsync(FiltroEspaciosAdmin filtro, CancellationToken cancellationToken = default);

    /// <summary>Obtiene el detalle administrativo de un área con sus puestos.</summary>
    Task<EspacioDetalle?> ObtenerAdministrativoAsync(Guid espacioId, CancellationToken cancellationToken = default);

    /// <summary>Consulta el catálogo anónimo de áreas activas.</summary>
    Task<PaginaResultado<EspacioCatalogo>> ConsultarCatalogoAsync(FiltroCatalogo filtro, CancellationToken cancellationToken = default);

    /// <summary>Obtiene el detalle público de un área activa.</summary>
    Task<EspacioPublico?> ObtenerPublicoAsync(Guid espacioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los datos mínimos del área que necesitan las series y las solicitudes:
    /// estado, competencia de aprobación y modalidad de préstamo.
    /// </summary>
    Task<EspacioParaReserva?> ObtenerParaReservaAsync(Guid espacioId, CancellationToken cancellationToken = default);
}
