using Backend.Application.Interfaces;
using Backend.Application.Modules.Asistencia.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Queries;

/// <summary>
/// Lectura del check-in: reserva, titular, estado y código QR estático del puesto, proyectados
/// sin cargar agregados completos. El código se compara en el servidor y no se devuelve al cliente.
/// </summary>
public sealed class ConsultaCheckIn : IConsultaCheckIn
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa la consulta con el contexto de lectura.</summary>
    public ConsultaCheckIn(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<ReservaParaCheckIn?> ObtenerAsync(
        Guid reservaId,
        CancellationToken cancellationToken = default)
    {
        var fila = await (
                from reserva in _contexto.Reservas.AsNoTracking()
                join puesto in _contexto.Puestos.AsNoTracking() on reserva.PuestoId equals puesto.Id
                join espacio in _contexto.Espacios.AsNoTracking() on puesto.EspacioId equals espacio.Id
                join titular in _contexto.Users.AsNoTracking() on reserva.UsuarioId equals titular.Id
                where reserva.Id == reservaId
                select new
                {
                    ReservaId = reserva.Id,
                    PuestoId = puesto.Id,
                    puesto.Identificador,
                    EspacioId = espacio.Id,
                    espacio.Nombre,
                    TitularId = reserva.UsuarioId,
                    Titular = titular.NombreCompleto,
                    reserva.Inicio,
                    reserva.Fin,
                    reserva.FinPlanificado,
                    reserva.Estado,
                    reserva.FechaCheckIn,
                    reserva.FechaCheckOut,
                    CodigoQr = puesto.CodigoQR
                })
            .FirstOrDefaultAsync(cancellationToken);

        return fila is null
            ? null
            : new ReservaParaCheckIn(
                fila.ReservaId,
                fila.PuestoId,
                fila.Identificador,
                fila.EspacioId,
                fila.Nombre,
                fila.TitularId,
                fila.Titular,
                fila.Inicio,
                fila.Fin,
                fila.FinPlanificado,
                fila.Estado,
                fila.FechaCheckIn,
                fila.FechaCheckOut,
                fila.CodigoQr.Valor);
    }
}
