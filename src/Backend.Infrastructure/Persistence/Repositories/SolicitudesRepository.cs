using System.Data;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador de escritura de solicitudes sobre EF Core 10. La decisión se confirma con la
/// ocupación del área y el correo pendiente en una única transacción serializable con bloqueo
/// de la fila del área.
/// </summary>
public sealed class SolicitudesRepository : ISolicitudesRepository
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa el repositorio con el contexto compartido.</summary>
    public SolicitudesRepository(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<SolicitudPrestamo?> ObtenerAsync(Guid solicitudId, CancellationToken cancellationToken = default) =>
        await _contexto.SolicitudesPrestamo.FirstOrDefaultAsync(
            solicitud => solicitud.Id == solicitudId,
            cancellationToken);

    /// <inheritdoc />
    public async Task AgregarAsync(SolicitudPrestamo solicitud, CancellationToken cancellationToken = default) =>
        await _contexto.SolicitudesPrestamo.AddAsync(solicitud, cancellationToken);

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
            return false;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ResultadoDecisionSolicitud> GuardarDecisionAsync(
        SolicitudPrestamo solicitud,
        ReservaEspacio? ocupacion,
        CorreoPendiente? correo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var estrategia = _contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await _contexto.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                // Barrera 1: serializa las decisiones concurrentes sobre la misma área.
                var espacioExiste = await _contexto.Espacios
                    .FromSqlInterpolated(
                        $"SELECT * FROM [Espacios] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {solicitud.EspacioId}")
                    .AsNoTracking()
                    .AnyAsync(cancellationToken);

                if (!espacioExiste)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return ResultadoDecisionSolicitud.Conflicto;
                }

                if (ocupacion is not null)
                {
                    // Barrera 2: re-verifica la agenda del área bajo el mismo bloqueo.
                    var agendaOcupada = await _contexto.ReservasEspacio
                        .AsNoTracking()
                        .AnyAsync(
                            otra => otra.EspacioId == ocupacion.EspacioId
                                && otra.Estado == EstadoOcupacion.Activa
                                && otra.Fin > ocupacion.Inicio
                                && otra.Inicio < ocupacion.Fin,
                            cancellationToken);

                    if (!agendaOcupada)
                    {
                        agendaOcupada = await (
                                from reserva in _contexto.Reservas.AsNoTracking()
                                join puesto in _contexto.Puestos.AsNoTracking() on reserva.PuestoId equals puesto.Id
                                where puesto.EspacioId == ocupacion.EspacioId
                                    && reserva.Estado == EstadoReserva.Confirmada
                                    && reserva.Fin > ocupacion.Inicio
                                    && reserva.Inicio < ocupacion.Fin
                                select reserva.Id)
                            .AnyAsync(cancellationToken);
                    }

                    if (agendaOcupada)
                    {
                        await transaccion.RollbackAsync(cancellationToken);
                        return ResultadoDecisionSolicitud.FranjaNoDisponible;
                    }

                    await _contexto.ReservasEspacio.AddAsync(ocupacion, cancellationToken);
                }

                if (correo is not null)
                {
                    await _contexto.CorreosPendientes.AddAsync(correo, cancellationToken);
                }

                await _contexto.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);

                return ResultadoDecisionSolicitud.Guardada;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return ResultadoDecisionSolicitud.Conflicto;
            }
            catch (DbUpdateException)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return ResultadoDecisionSolicitud.Conflicto;
            }
        });
    }
}
