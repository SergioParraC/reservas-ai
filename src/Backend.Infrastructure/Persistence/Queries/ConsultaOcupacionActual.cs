using Backend.Application.Interfaces;
using Backend.Application.Modules.Monitoreo.Dtos;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Queries;

/// <summary>
/// Foto instantánea de la ocupación: áreas con su aforo, puestos activos y las reservas que
/// importan al mapa (en uso dentro de su franja, sesión ya vencida y pendiente de check-in dentro
/// de la ventana). La agregación se resuelve en la capa de aplicación.
/// </summary>
public sealed class ConsultaOcupacionActual : IConsultaOcupacionActual
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa la consulta con el contexto de lectura.</summary>
    public ConsultaOcupacionActual(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<OcupacionInstantanea> ObtenerAsync(
        DateTimeOffset ahora,
        TimeSpan tolerancia,
        CancellationToken cancellationToken = default)
    {
        var areas = await _contexto.Espacios
            .AsNoTracking()
            .OrderBy(espacio => espacio.Nombre)
            .Select(espacio => new AreaOcupable(espacio.Id, espacio.Nombre, espacio.Aforo))
            .ToListAsync(cancellationToken);

        var puestos = await (
                from puesto in _contexto.Puestos.AsNoTracking()
                where puesto.Estado == EstadoUnidad.Activo
                orderby puesto.Identificador
                select new PuestoOcupable(puesto.EspacioId, puesto.Id, puesto.Identificador))
            .ToListAsync(cancellationToken);

        var desdeVentana = ahora - tolerancia;
        var hastaVentana = ahora + tolerancia;

        var reservas = await (
                from reserva in _contexto.Reservas.AsNoTracking()
                join puesto in _contexto.Puestos.AsNoTracking() on reserva.PuestoId equals puesto.Id
                join titular in _contexto.Users.AsNoTracking() on reserva.UsuarioId equals titular.Id
                where puesto.Estado == EstadoUnidad.Activo
                    && ((reserva.Estado == EstadoReserva.EnUso && reserva.Fin <= ahora)
                        || (reserva.Estado == EstadoReserva.EnUso && reserva.Inicio <= ahora && reserva.Fin > ahora)
                        || (reserva.Estado == EstadoReserva.Confirmada
                            && reserva.Inicio >= desdeVentana
                            && reserva.Inicio <= hastaVentana))
                select new ReservaOcupacionActual(
                    reserva.Id,
                    puesto.EspacioId,
                    puesto.Id,
                    reserva.Estado,
                    reserva.Inicio,
                    reserva.Fin,
                    reserva.FechaCheckIn,
                    titular.NombreCompleto))
            .ToListAsync(cancellationToken);

        return new OcupacionInstantanea(areas, puestos, reservas);
    }
}
