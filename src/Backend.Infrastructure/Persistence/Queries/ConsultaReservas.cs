using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Queries;

/// <summary>
/// Proyecciones de lectura del módulo de reservas con <c>AsNoTracking()</c>: puesto reservable,
/// ocupación de una ventana y agenda del titular.
/// </summary>
public sealed class ConsultaReservas : IConsultaReservas
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa la consulta con el contexto de lectura.</summary>
    public ConsultaReservas(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<PuestoReservable?> ObtenerPuestoAsync(
        Guid puestoId,
        CancellationToken cancellationToken = default)
    {
        var fila = await (
                from puesto in _contexto.Puestos.AsNoTracking()
                join espacio in _contexto.Espacios.AsNoTracking() on puesto.EspacioId equals espacio.Id
                where puesto.Id == puestoId
                select new
                {
                    PuestoId = puesto.Id,
                    puesto.Identificador,
                    EstadoPuesto = puesto.Estado,
                    EspacioId = espacio.Id,
                    EspacioNombre = espacio.Nombre,
                    EstadoEspacio = espacio.Estado,
                    espacio.TipoAprobacion
                })
            .FirstOrDefaultAsync(cancellationToken);

        return fila is null
            ? null
            : new PuestoReservable(
                fila.PuestoId,
                fila.Identificador,
                fila.EstadoPuesto,
                fila.EspacioId,
                fila.EspacioNombre,
                fila.EstadoEspacio,
                fila.TipoAprobacion);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IntervaloOcupado>> ObtenerOcupadasAsync(
        Guid puestoId,
        DateTimeOffset desde,
        DateTimeOffset hasta,
        CancellationToken cancellationToken = default)
    {
        var filas = await _contexto.Reservas
            .AsNoTracking()
            .Where(reserva => reserva.PuestoId == puestoId
                && reserva.Estado == EstadoReserva.Confirmada
                && reserva.Fin > desde
                && reserva.Inicio < hasta)
            .OrderBy(reserva => reserva.Inicio)
            .Select(reserva => new { reserva.Inicio, reserva.Fin })
            .ToListAsync(cancellationToken);

        return filas
            .Select(fila => new IntervaloOcupado(fila.Inicio, fila.Fin))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<PaginaResultado<ReservaResumen>> ListarPropiasAsync(
        Guid usuarioId,
        FiltroReservasPropias filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta =
            from reserva in _contexto.Reservas.AsNoTracking()
            join puesto in _contexto.Puestos.AsNoTracking() on reserva.PuestoId equals puesto.Id
            join espacio in _contexto.Espacios.AsNoTracking() on puesto.EspacioId equals espacio.Id
            where reserva.UsuarioId == usuarioId
            select new { Reserva = reserva, Puesto = puesto, Espacio = espacio };

        if (filtro.Desde is { } desde)
        {
            consulta = consulta.Where(fila => fila.Reserva.Inicio >= desde);
        }

        if (filtro.Hasta is { } hasta)
        {
            consulta = consulta.Where(fila => fila.Reserva.Inicio <= hasta);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(fila => fila.Reserva.Estado == estado);
        }

        var total = await consulta.CountAsync(cancellationToken);

        var elementos = await consulta
            .OrderBy(fila => fila.Reserva.Inicio)
            .ThenBy(fila => fila.Reserva.Id)
            .Skip((filtro.Pagina - 1) * filtro.Tamano)
            .Take(filtro.Tamano)
            .Select(fila => new ReservaResumen(
                fila.Reserva.Id,
                fila.Reserva.PuestoId,
                fila.Puesto.Identificador,
                fila.Espacio.Nombre,
                fila.Reserva.Inicio,
                fila.Reserva.Fin,
                fila.Reserva.Estado,
                fila.Reserva.CreadaEn,
                fila.Reserva.CanceladaEn,
                false))
            .ToListAsync(cancellationToken);

        return new PaginaResultado<ReservaResumen>(elementos, total, filtro.Pagina, filtro.Tamano);
    }
}
