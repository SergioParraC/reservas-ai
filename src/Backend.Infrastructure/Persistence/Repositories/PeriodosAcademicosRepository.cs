using System.Data;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador de escritura del calendario institucional sobre EF Core 10. La activación se
/// ejecuta en una transacción serializable que desactiva al periodo vigente y activa al nuevo.
/// </summary>
public sealed class PeriodosAcademicosRepository : IPeriodosAcademicosRepository
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa el repositorio con el contexto compartido.</summary>
    public PeriodosAcademicosRepository(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<PeriodoAcademico?> ObtenerAsync(Guid periodoId, CancellationToken cancellationToken = default) =>
        await _contexto.PeriodosAcademicos.FirstOrDefaultAsync(periodo => periodo.Id == periodoId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ExisteSolapeAsync(
        DateOnly fechaInicio,
        DateOnly fechaFin,
        Guid? excluirPeriodoId,
        CancellationToken cancellationToken = default) =>
        await _contexto.PeriodosAcademicos
            .AsNoTracking()
            .AnyAsync(
                periodo => (excluirPeriodoId == null || periodo.Id != excluirPeriodoId)
                    && periodo.FechaInicio <= fechaFin
                    && fechaInicio <= periodo.FechaFin,
                cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ExisteOcupacionFueraDeAsync(
        DateTimeOffset inicio,
        DateTimeOffset finExclusivo,
        CancellationToken cancellationToken = default)
    {
        var ocupacionDeArea = await _contexto.ReservasEspacio
            .AsNoTracking()
            .AnyAsync(
                ocupacion => ocupacion.Estado == EstadoOcupacion.Activa
                    && (ocupacion.Inicio < inicio || ocupacion.Fin > finExclusivo),
                cancellationToken);

        if (ocupacionDeArea)
        {
            return true;
        }

        var solicitudFuera = await _contexto.SolicitudesPrestamo
            .AsNoTracking()
            .AnyAsync(
                solicitud => (solicitud.Estado == EstadoSolicitud.Radicado || solicitud.Estado == EstadoSolicitud.EnRevision)
                    && (solicitud.Inicio < inicio || solicitud.Fin > finExclusivo),
                cancellationToken);

        if (solicitudFuera)
        {
            return true;
        }

        return await _contexto.Reservas
            .AsNoTracking()
            .AnyAsync(
                reserva => reserva.Estado == EstadoReserva.Confirmada
                    && (reserva.Inicio < inicio || reserva.Fin > finExclusivo),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task AgregarAsync(PeriodoAcademico periodo, CancellationToken cancellationToken = default) =>
        await _contexto.PeriodosAcademicos.AddAsync(periodo, cancellationToken);

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
    public async Task<bool> ActivarAsync(
        Guid periodoId,
        DateTimeOffset momento,
        CancellationToken cancellationToken = default)
    {
        var estrategia = _contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await _contexto.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

            try
            {
                var periodo = await _contexto.PeriodosAcademicos
                    .FromSqlInterpolated(
                        $"SELECT * FROM [PeriodosAcademicos] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {periodoId}")
                    .FirstOrDefaultAsync(cancellationToken);

                if (periodo is null)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return false;
                }

                await _contexto.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [PeriodosAcademicos] SET [EstadoActivo] = 0 WHERE [EstadoActivo] = 1 AND [Id] <> {periodoId}",
                    cancellationToken);

                periodo.Activar(momento);
                await _contexto.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);

                return true;
            }
            catch (Exception excepcion) when (excepcion is DbUpdateException or InvalidOperationException)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return false;
            }
        });
    }
}
