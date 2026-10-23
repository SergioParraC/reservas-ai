using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Queries;

/// <summary>
/// Proyecciones de lectura de solicitudes con <c>AsNoTracking()</c>: historial propio,
/// bandeja por competencia y detalle con los datos del área y del solicitante.
/// </summary>
public sealed partial class ConsultaSolicitudes : IConsultaSolicitudes
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa la consulta con el contexto de lectura.</summary>
    public ConsultaSolicitudes(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<PaginaResultado<SolicitudResumen>> ListarPropiasAsync(
        Guid usuarioId,
        FiltroSolicitudesPropias filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta =
            from solicitud in _contexto.SolicitudesPrestamo.AsNoTracking()
            join espacio in _contexto.Espacios.AsNoTracking() on solicitud.EspacioId equals espacio.Id
            where solicitud.SolicitanteId == usuarioId
            select new { Solicitud = solicitud, EspacioNombre = espacio.Nombre };

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(fila => fila.Solicitud.Estado == estado);
        }

        var total = await consulta.CountAsync(cancellationToken);

        var elementos = await consulta
            .OrderByDescending(fila => fila.Solicitud.RadicadaEn)
            .ThenByDescending(fila => fila.Solicitud.Id)
            .Skip((filtro.Pagina - 1) * filtro.Tamano)
            .Take(filtro.Tamano)
            .Select(fila => new SolicitudResumen(
                fila.Solicitud.Id,
                fila.Solicitud.EspacioId,
                fila.EspacioNombre,
                fila.Solicitud.Inicio,
                fila.Solicitud.Fin,
                fila.Solicitud.Estado,
                fila.Solicitud.MotivoRechazo,
                fila.Solicitud.RadicadaEn))
            .ToListAsync(cancellationToken);

        return new PaginaResultado<SolicitudResumen>(elementos, total, filtro.Pagina, filtro.Tamano);
    }

    /// <inheritdoc />
    public async Task<PaginaResultado<SolicitudBandejaItem>> ListarBandejaAsync(
        FiltroBandeja filtro,
        IReadOnlyCollection<TipoAprobacion> competencias,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(competencias);

        var consulta =
            from solicitud in _contexto.SolicitudesPrestamo.AsNoTracking()
            join espacio in _contexto.Espacios.AsNoTracking() on solicitud.EspacioId equals espacio.Id
            join usuario in _contexto.Users.AsNoTracking() on solicitud.SolicitanteId equals usuario.Id
            where competencias.Contains(espacio.TipoAprobacion)
            select new
            {
                Solicitud = solicitud,
                EspacioNombre = espacio.Nombre,
                Usuario = usuario
            };

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(fila => fila.Solicitud.Estado == estado);
        }

        if (filtro.EspacioId is { } espacioId)
        {
            consulta = consulta.Where(fila => fila.Solicitud.EspacioId == espacioId);
        }

        if (filtro.Desde is { } desde)
        {
            consulta = consulta.Where(fila => fila.Solicitud.RadicadaEn >= desde);
        }

        if (filtro.Hasta is { } hasta)
        {
            consulta = consulta.Where(fila => fila.Solicitud.RadicadaEn <= hasta);
        }

        var total = await consulta.CountAsync(cancellationToken);

        var elementos = await consulta
            .OrderByDescending(fila => fila.Solicitud.RadicadaEn)
            .ThenByDescending(fila => fila.Solicitud.Id)
            .Skip((filtro.Pagina - 1) * filtro.Tamano)
            .Take(filtro.Tamano)
            .Select(fila => new SolicitudBandejaItem(
                fila.Solicitud.Id,
                fila.Solicitud.EspacioId,
                fila.EspacioNombre,
                fila.Solicitud.SolicitanteId,
                fila.Usuario.NombreCompleto,
                fila.Usuario.Email ?? string.Empty,
                fila.Solicitud.Inicio,
                fila.Solicitud.Fin,
                fila.Solicitud.Materia,
                fila.Solicitud.Estado,
                fila.Solicitud.RadicadaEn))
            .ToListAsync(cancellationToken);

        return new PaginaResultado<SolicitudBandejaItem>(elementos, total, filtro.Pagina, filtro.Tamano);
    }

    /// <inheritdoc />
    public async Task<int> ContarActivasAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await _contexto.SolicitudesPrestamo
            .AsNoTracking()
            .CountAsync(
                solicitud => solicitud.SolicitanteId == usuarioId
                    && (solicitud.Estado == EstadoSolicitud.Radicado || solicitud.Estado == EstadoSolicitud.EnRevision),
                cancellationToken);
}
