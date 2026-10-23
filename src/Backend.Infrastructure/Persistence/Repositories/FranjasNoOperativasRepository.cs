using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

/// <summary>Adaptador de escritura de las franjas no operativas globales.</summary>
public sealed class FranjasNoOperativasRepository : IFranjasNoOperativasRepository
{
    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa el repositorio con el contexto compartido.</summary>
    public FranjasNoOperativasRepository(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<FranjaNoOperativa?> ObtenerAsync(Guid franjaId, CancellationToken cancellationToken = default) =>
        await _contexto.FranjasNoOperativas.FirstOrDefaultAsync(franja => franja.Id == franjaId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ExisteDuplicadaAsync(
        DayOfWeek? diaSemana,
        DateOnly? fechaEspecifica,
        TimeOnly horaInicio,
        TimeOnly horaFin,
        Guid? excluirFranjaId,
        CancellationToken cancellationToken = default) =>
        await _contexto.FranjasNoOperativas
            .AsNoTracking()
            .AnyAsync(
                franja => franja.Activa
                    && (excluirFranjaId == null || franja.Id != excluirFranjaId)
                    && franja.DiaSemana == diaSemana
                    && franja.FechaEspecifica == fechaEspecifica
                    && franja.HoraInicio == horaInicio
                    && franja.HoraFin == horaFin,
                cancellationToken);

    /// <inheritdoc />
    public async Task AgregarAsync(FranjaNoOperativa franja, CancellationToken cancellationToken = default) =>
        await _contexto.FranjasNoOperativas.AddAsync(franja, cancellationToken);

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
}
