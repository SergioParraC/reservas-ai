using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Infrastructure.Persistence.Queries;

/// <summary>
/// Proyección de lectura del calendario institucional con caché en memoria por
/// <c>Calendario:MinutosCacheCalendario</c>. La caché guarda el periodo y las franjas sin
/// seguimiento de EF y se purga en cada mutación.
/// </summary>
public sealed class ConsultaCalendario : IConsultaCalendario
{
    private const string ClavePeriodos = "calendario:periodos";
    private const string ClaveFranjas = "calendario:franjas";

    private readonly ApplicationDbContext _contexto;
    private readonly IMemoryCache _cache;
    private readonly OpcionesCalendario _opciones;

    /// <summary>Inicializa la consulta con el contexto, la caché y la política del calendario.</summary>
    public ConsultaCalendario(
        ApplicationDbContext contexto,
        IMemoryCache cache,
        OpcionesCalendario opciones)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
    }

    /// <inheritdoc />
    public async Task<PeriodoAcademico?> ObtenerPeriodoActivoAsync(CancellationToken cancellationToken = default)
    {
        var periodos = await CargarPeriodosAsync(cancellationToken);

        return periodos.FirstOrDefault(periodo => periodo.EstadoActivo);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PeriodoAcademico>> ListarPeriodosAsync(CancellationToken cancellationToken = default) =>
        await CargarPeriodosAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<FranjaNoOperativa>> ListarFranjasAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(ClaveFranjas, out IReadOnlyList<FranjaNoOperativa>? enCache) && enCache is not null)
        {
            return enCache;
        }

        var franjas = await _contexto.FranjasNoOperativas
            .AsNoTracking()
            .Where(franja => franja.Activa)
            .OrderBy(franja => franja.HoraInicio)
            .ToListAsync(cancellationToken);

        var resultado = franjas.ToArray();
        _cache.Set(ClaveFranjas, (IReadOnlyList<FranjaNoOperativa>)resultado, _opciones.ObtenerVigenciaCache());

        return resultado;
    }

    /// <inheritdoc />
    public async Task<CalendarioInstitucional> ObtenerCalendarioAsync(
        DateOnly desde,
        DateOnly hasta,
        TimeZoneInfo zona,
        CancellationToken cancellationToken = default)
    {
        var periodo = await ObtenerPeriodoActivoAsync(cancellationToken);

        if (periodo is null && !_opciones.PeriodoActivoRequerido)
        {
            // Configuración sin exigencia de periodo: la ventana queda abierta y solo aplican
            // las franjas no operativas.
            periodo = PeriodoAcademico.Crear(
                "Sin restricción de periodo",
                new DateOnly(1900, 1, 1),
                new DateOnly(2999, 12, 31),
                DateTimeOffset.UnixEpoch);
            periodo.Activar(DateTimeOffset.UnixEpoch);
        }

        var franjas = await ListarFranjasAsync(cancellationToken);

        return CalendarioInstitucional.Crear(periodo, franjas, desde, hasta, zona);
    }

    /// <inheritdoc />
    public void Invalidar()
    {
        _cache.Remove(ClavePeriodos);
        _cache.Remove(ClaveFranjas);
    }

    private async Task<IReadOnlyList<PeriodoAcademico>> CargarPeriodosAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(ClavePeriodos, out IReadOnlyList<PeriodoAcademico>? enCache) && enCache is not null)
        {
            return enCache;
        }

        var periodos = await _contexto.PeriodosAcademicos
            .AsNoTracking()
            .OrderBy(periodo => periodo.FechaInicio)
            .ToListAsync(cancellationToken);

        var resultado = periodos.ToArray();
        _cache.Set(ClavePeriodos, (IReadOnlyList<PeriodoAcademico>)resultado, _opciones.ObtenerVigenciaCache());

        return resultado;
    }
}
