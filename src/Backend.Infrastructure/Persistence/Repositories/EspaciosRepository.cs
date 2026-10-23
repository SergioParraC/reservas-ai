using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Adaptador de escritura del agregado de áreas sobre EF Core 10. Es el único punto
/// que conoce <c>DbSet</c> y traduce los conflictos de persistencia a un resultado booleano.
/// </summary>
public sealed class EspaciosRepository : IEspaciosRepository
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa el repositorio con el contexto compartido.</summary>
    public EspaciosRepository(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<Espacio?> ObtenerAsync(Guid espacioId, CancellationToken cancellationToken = default) =>
        await _contexto.Espacios
            .Include(espacio => espacio.Puestos)
            .FirstOrDefaultAsync(espacio => espacio.Id == espacioId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Espacio>> ObtenerVariosAsync(
        IReadOnlyCollection<Guid> espacioIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(espacioIds);

        if (espacioIds.Count == 0)
        {
            return [];
        }

        var identificadores = espacioIds.Distinct().ToArray();

        return await _contexto.Espacios
            .AsNoTracking()
            .Include(espacio => espacio.Puestos)
            .Where(espacio => identificadores.Contains(espacio.Id))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Puesto?> ObtenerPuestoAsync(Guid puestoId, CancellationToken cancellationToken = default) =>
        await _contexto.Puestos
            .AsNoTracking()
            .FirstOrDefaultAsync(puesto => puesto.Id == puestoId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExisteNombreUbicacionAsync(
        string nombre,
        string ubicacion,
        Guid? excluirEspacioId,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.Espacios.AsNoTracking()
            .Where(espacio =>
                EF.Functions.Collate(espacio.Nombre, "Latin1_General_CI_AI") == nombre
                && EF.Functions.Collate(espacio.Ubicacion, "Latin1_General_CI_AI") == ubicacion);

        if (excluirEspacioId is { } excluido)
        {
            consulta = consulta.Where(espacio => espacio.Id != excluido);
        }

        return consulta.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExisteCodigoQrAsync(string codigoQr, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoQr))
        {
            return false;
        }

        var codigo = CodigoQR.Desde(codigoQr);

        var enAreas = await _contexto.Espacios.AsNoTracking()
            .AnyAsync(espacio => espacio.CodigoQR == codigo, cancellationToken);

        if (enAreas)
        {
            return true;
        }

        return await _contexto.Puestos.AsNoTracking()
            .AnyAsync(puesto => puesto.CodigoQR == codigo, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AgregarAsync(Espacio espacio, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(espacio);

        await _contexto.Espacios.AddAsync(espacio, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> GuardarCambiosAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Índice único o token de concurrencia: el caso de uso lo traduce a 409.
            return false;
        }
    }
}
