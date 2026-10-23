using System.Data;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador de escritura de series semestrales sobre EF Core 10. El alta bloquea la fila del
/// área, re-verifica el barrido de colisiones y escribe la serie con todas sus ocurrencias en
/// una única transacción serializable.
/// </summary>
public sealed class ReservasRecurrentesRepository : IReservasRecurrentesRepository
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa el repositorio con el contexto compartido.</summary>
    public ReservasRecurrentesRepository(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<ResultadoPersistenciaSerie> GuardarSerieAsync(
        ReservaRecurrente serie,
        IReadOnlyList<FranjaHoraria> ocurrencias,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serie);
        ArgumentNullException.ThrowIfNull(ocurrencias);

        if (ocurrencias.Count == 0)
        {
            return new ResultadoPersistenciaSerie(ResultadoGuardadoSerie.Colision, []);
        }

        var estrategia = _contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await _contexto.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                // Barrera 1: serializa las altas concurrentes sobre la misma área.
                var espacioExiste = await _contexto.Espacios
                    .FromSqlInterpolated(
                        $"SELECT * FROM [Espacios] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {serie.EspacioId}")
                    .AsNoTracking()
                    .AnyAsync(cancellationToken);

                if (!espacioExiste)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return new ResultadoPersistenciaSerie(ResultadoGuardadoSerie.Conflicto, []);
                }

                // Barrera 2: re-verifica la agenda del área bajo el mismo bloqueo.
                var ocupadas = await ObtenerOcupadasAsync(
                    serie.EspacioId,
                    ocurrencias[0].Inicio,
                    ocurrencias[^1].Fin,
                    cancellationToken);

                var colisiones = MotorDisponibilidad.DetectarColisiones(ocurrencias, ocupadas);

                if (colisiones.Count > 0)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return new ResultadoPersistenciaSerie(ResultadoGuardadoSerie.Colision, colisiones);
                }

                await _contexto.ReservasRecurrentes.AddAsync(serie, cancellationToken);

                foreach (var ocurrencia in ocurrencias)
                {
                    await _contexto.ReservasEspacio.AddAsync(
                        ReservaEspacio.Crear(
                            serie.EspacioId,
                            serie.DocenteId,
                            OrigenOcupacion.Recurrente,
                            serie.Id,
                            ocurrencia,
                            serie.CreadaEn),
                        cancellationToken);
                }

                await _contexto.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);

                return new ResultadoPersistenciaSerie(ResultadoGuardadoSerie.Guardada, []);
            }
            catch (Exception excepcion) when (excepcion is DbUpdateException or ConflictoException)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return new ResultadoPersistenciaSerie(ResultadoGuardadoSerie.Conflicto, []);
            }
        });
    }

    /// <inheritdoc />
    public async Task<ReservaRecurrente?> ObtenerAsync(Guid serieId, CancellationToken cancellationToken = default) =>
        await _contexto.ReservasRecurrentes.FirstOrDefaultAsync(serie => serie.Id == serieId, cancellationToken);

    /// <inheritdoc />
    public async Task<PaginaResultado<SerieResumen>> ListarPropiasAsync(
        Guid docenteId,
        int pagina,
        int tamano,
        CancellationToken cancellationToken = default)
    {
        var consulta =
            from serie in _contexto.ReservasRecurrentes.AsNoTracking()
            join espacio in _contexto.Espacios.AsNoTracking() on serie.EspacioId equals espacio.Id
            where serie.DocenteId == docenteId
            select new { Serie = serie, EspacioNombre = espacio.Nombre };

        var total = await consulta.CountAsync(cancellationToken);

        var elementos = await consulta
            .OrderByDescending(fila => fila.Serie.CreadaEn)
            .ThenByDescending(fila => fila.Serie.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(fila => new SerieResumen(
                fila.Serie.Id,
                fila.Serie.EspacioId,
                fila.EspacioNombre,
                fila.Serie.Materia,
                fila.Serie.TipoRecurrencia,
                fila.Serie.DiaSemana,
                fila.Serie.HoraInicio,
                fila.Serie.HoraFin,
                fila.Serie.PeriodoInicio,
                fila.Serie.PeriodoFin,
                _contexto.ReservasEspacio.Count(ocupacion =>
                    ocupacion.Origen == OrigenOcupacion.Recurrente
                    && ocupacion.OrigenId == fila.Serie.Id
                    && ocupacion.Estado == EstadoOcupacion.Activa),
                fila.Serie.CreadaEn))
            .ToListAsync(cancellationToken);

        return new PaginaResultado<SerieResumen>(elementos, total, pagina, tamano);
    }

    private async Task<IReadOnlyList<FranjaHoraria>> ObtenerOcupadasAsync(
        Guid espacioId,
        DateTimeOffset desde,
        DateTimeOffset hasta,
        CancellationToken cancellationToken)
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
            .Select(fila => FranjaHoraria.Desde(fila.Inicio, fila.Fin))
            .ToArray();
    }
}