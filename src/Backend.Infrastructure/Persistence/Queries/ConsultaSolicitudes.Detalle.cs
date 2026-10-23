using Backend.Application.Interfaces;
using Backend.Application.Modules.Solicitudes.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Queries;

/// <summary>Proyección del detalle de una solicitud con los datos del área y del solicitante.</summary>
public sealed partial class ConsultaSolicitudes
{
    /// <inheritdoc />
    public async Task<SolicitudDetalle?> ObtenerAsync(Guid solicitudId, CancellationToken cancellationToken = default)
    {
        var fila = await (
                from solicitud in _contexto.SolicitudesPrestamo.AsNoTracking()
                join espacio in _contexto.Espacios.AsNoTracking() on solicitud.EspacioId equals espacio.Id
                join usuario in _contexto.Users.AsNoTracking() on solicitud.SolicitanteId equals usuario.Id
                where solicitud.Id == solicitudId
                select new
                {
                    Solicitud = solicitud,
                    EspacioNombre = espacio.Nombre,
                    espacio.TipoAprobacion,
                    espacio.TipoModalidadPrestamo,
                    Usuario = usuario
                })
            .FirstOrDefaultAsync(cancellationToken);

        return fila is null
            ? null
            : new SolicitudDetalle(
                fila.Solicitud.Id,
                fila.Solicitud.EspacioId,
                fila.EspacioNombre,
                fila.TipoAprobacion,
                fila.TipoModalidadPrestamo,
                fila.Solicitud.SolicitanteId,
                fila.Usuario.NombreCompleto,
                fila.Usuario.Email ?? string.Empty,
                fila.Solicitud.Inicio,
                fila.Solicitud.Fin,
                fila.Solicitud.Materia,
                fila.Solicitud.DocenteResponsable,
                fila.Solicitud.Justificacion,
                fila.Solicitud.Estado,
                fila.Solicitud.RadicadaEn,
                fila.Solicitud.RevisadaEn,
                fila.Solicitud.RevisorId,
                fila.Solicitud.MotivoRechazo,
                fila.Solicitud.ArchivoNombreOriginal,
                fila.Solicitud.ArchivoTamanoBytes,
                fila.Solicitud.ArchivoRuta);
    }
}
