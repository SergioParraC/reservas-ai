using System.Data;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador de escritura de reservas sobre EF Core 10. El alta se ejecuta en una transacción
/// serializable con bloqueo de la fila del puesto y del conjunto de reservas del titular, de
/// modo que la verificación de solapamiento y cupo no pueda rebasarse entre solicitudes.
/// </summary>
public sealed class ReservasRepository : IReservasRepository
{
    private const int ReintentosMaximos = 3;

    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa el repositorio con el contexto compartido.</summary>
    public ReservasRepository(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<Reserva?> ObtenerAsync(Guid reservaId, CancellationToken cancellationToken = default) =>
        await _contexto.Reservas.FirstOrDefaultAsync(reserva => reserva.Id == reservaId, cancellationToken);

    /// <inheritdoc />
    public async Task<ResultadoGuardadoReserva> GuardarSiDisponibleAsync(
        Reserva reserva,
        int maximoReservasActivas,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reserva);

        var estrategia = _contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await _contexto.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                // Barrera 1: serializa las altas concurrentes del mismo puesto.
                var puestoExiste = await _contexto.Puestos
                    .FromSqlInterpolated(
                        $"SELECT * FROM [Puestos] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {reserva.PuestoId}")
                    .AsNoTracking()
                    .AnyAsync(cancellationToken);

                if (!puestoExiste)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return ResultadoGuardadoReserva.Conflicto;
                }

                // Barrera 2: re-verifica solapamientos con la agenda ya bloqueada. Tanto una reserva
                // confirmada como una sesión en uso siguen ocupando su franja.
                var ocupadas = await _contexto.Reservas
                    .AsNoTracking()
                    .Where(otra => otra.PuestoId == reserva.PuestoId
                        && (otra.Estado == EstadoReserva.Confirmada || otra.Estado == EstadoReserva.EnUso)
                        && otra.Fin > reserva.Inicio
                        && otra.Inicio < reserva.Fin)
                    .Select(otra => new { otra.Inicio, otra.Fin })
                    .ToListAsync(cancellationToken);

                var vigentes = ocupadas
                    .Select(otra => FranjaHoraria.Desde(otra.Inicio, otra.Fin))
                    .ToArray();

                if (MotorDisponibilidad.HaySolapamiento(reserva.Franja, vigentes))
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return ResultadoGuardadoReserva.FranjaOcupada;
                }

                // Barrera 3: cupo del titular, contado bajo el mismo bloqueo serializable. Quien está
                // usando un puesto sigue teniendo una reserva activa.
                var activas = await _contexto.Reservas
                    .FromSqlInterpolated(
                        $"SELECT * FROM [Reservas] WITH (UPDLOCK, HOLDLOCK) WHERE [UsuarioId] = {reserva.UsuarioId} AND [Estado] IN (1, 4)")
                    .AsNoTracking()
                    .CountAsync(cancellationToken);

                if (activas >= maximoReservasActivas)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return ResultadoGuardadoReserva.CupoAgotado;
                }

                await _contexto.Reservas.AddAsync(reserva, cancellationToken);
                await _contexto.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);

                return ResultadoGuardadoReserva.Guardada;
            }
            catch (DbUpdateException)
            {
                // Índice único filtrado o token de concurrencia: la aplicación lo traduce a 409.
                await transaccion.RollbackAsync(cancellationToken);
                return ResultadoGuardadoReserva.Conflicto;
            }
            catch (ConflictoException)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return ResultadoGuardadoReserva.Conflicto;
            }
        });
    }

    /// <inheritdoc />
    public async Task<bool> GuardarCambiosAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // El RowVersion cambió entre la lectura y la escritura: la aplicación responde 409.
            return false;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reserva>> ListarSinCheckInVencidasAsync(
        DateTimeOffset ahora,
        TimeSpan tolerancia,
        int maximo,
        CancellationToken cancellationToken = default)
    {
        var limite = ahora - tolerancia;

        return await _contexto.Reservas
            .Where(reserva => reserva.Estado == EstadoReserva.Confirmada && reserva.Inicio <= limite)
            .OrderBy(reserva => reserva.Inicio)
            .ThenBy(reserva => reserva.Id)
            .Take(maximo < 1 ? 1 : maximo)
            .ToListAsync(cancellationToken);
    }
}
