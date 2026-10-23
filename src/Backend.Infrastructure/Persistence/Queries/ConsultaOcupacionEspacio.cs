using Backend.Application.Interfaces;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Queries;

/// <summary>
/// Proyección de la agenda única del área: filas de <c>ReservasEspacio</c> y reservas puntuales
/// confirmadas de sus puestos, leídas juntas para validar colisiones.
/// </summary>
public sealed class ConsultaOcupacionEspacio : IConsultaOcupacionEspacio
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa la consulta con el contexto de lectura.</summary>
    public ConsultaOcupacionEspacio(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IntervaloOcupacion>> ObtenerAsync(
        Guid espacioId,
        DateTimeOffset desde,
        DateTimeOffset hasta,
        CancellationToken cancellationToken = default)
    {
        var area = await _contexto.ReservasEspacio
            .AsNoTracking()
            .Where(ocupacion => ocupacion.EspacioId == espacioId
                && ocupacion.Estado == EstadoOcupacion.Activa
                && ocupacion.Fin > desde
                && ocupacion.Inicio < hasta)
            .Select(ocupacion => new { ocupacion.Inicio, ocupacion.Fin })
            .ToListAsync(cancellationToken);

        var puestos = await (
                from reserva in _contexto.Reservas.AsNoTracking()
                join puesto in _contexto.Puestos.AsNoTracking() on reserva.PuestoId equals puesto.Id
                where puesto.EspacioId == espacioId
                    && reserva.Estado == EstadoReserva.Confirmada
                    && reserva.Fin > desde
                    && reserva.Inicio < hasta
                select new { reserva.Inicio, reserva.Fin })
            .ToListAsync(cancellationToken);

        return area
            .Concat(puestos)
            .Select(fila => new IntervaloOcupacion(fila.Inicio, fila.Fin))
            .OrderBy(intervalo => intervalo.Inicio)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> HayOcupacionFuturaAsync(
        Guid espacioId,
        DateTimeOffset desde,
        CancellationToken cancellationToken = default)
    {
        if (await _contexto.ReservasEspacio.AsNoTracking().AnyAsync(
                ocupacion => ocupacion.EspacioId == espacioId
                    && ocupacion.Estado == EstadoOcupacion.Activa
                    && ocupacion.Fin > desde,
                cancellationToken))
        {
            return true;
        }

        return await (
                from reserva in _contexto.Reservas.AsNoTracking()
                join puesto in _contexto.Puestos.AsNoTracking() on reserva.PuestoId equals puesto.Id
                where puesto.EspacioId == espacioId
                    && reserva.Estado == EstadoReserva.Confirmada
                    && reserva.Fin > desde
                select reserva.Id)
            .AnyAsync(cancellationToken);
    }
}
