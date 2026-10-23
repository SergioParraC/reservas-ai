using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cola persistida de correos. El barrido toma un lote con <c>UPDLOCK/READPAST</c> para que dos
/// instancias del despachador no procesen la misma fila en paralelo.
/// </summary>
public sealed class ColaCorreos : IColaCorreos
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa la cola con el contexto compartido.</summary>
    public ColaCorreos(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CorreoPendiente>> TomarPendientesAsync(
        int maximo,
        DateTimeOffset ahora,
        CancellationToken cancellationToken = default)
    {
        if (maximo < 1)
        {
            return [];
        }

        return await _contexto.CorreosPendientes
            .FromSqlInterpolated($"""
                SELECT TOP ({maximo}) *
                FROM [CorreosPendientes] WITH (UPDLOCK, ROWLOCK, READPAST)
                WHERE [Estado] = {(int)EstadoCorreo.Pendiente} AND [ProximoIntentoEn] <= {ahora}
                ORDER BY [ProximoIntentoEn]
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarcarEnviadoAsync(
        Guid correoId,
        DateTimeOffset momento,
        CancellationToken cancellationToken = default) =>
        await _contexto.CorreosPendientes
            .Where(correo => correo.Id == correoId)
            .ExecuteUpdateAsync(
                cambios => cambios
                    .SetProperty(correo => correo.Estado, EstadoCorreo.Enviado)
                    .SetProperty(correo => correo.EnviadoEn, momento)
                    .SetProperty(correo => correo.UltimoError, (string?)null),
                cancellationToken);

    /// <inheritdoc />
    public async Task MarcarFalloAsync(
        Guid correoId,
        string error,
        DateTimeOffset momento,
        CancellationToken cancellationToken = default)
    {
        var correo = await _contexto.CorreosPendientes
            .FirstOrDefaultAsync(fila => fila.Id == correoId, cancellationToken);

        if (correo is null)
        {
            return;
        }

        correo.RegistrarFallo(error, momento);
        await _contexto.SaveChangesAsync(cancellationToken);
    }
}
