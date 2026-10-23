using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Queries;

/// <summary>
/// Proyecciones de lectura del catálogo físico con <c>AsNoTracking()</c> y comparación
/// explícita <c>Latin1_General_CI_AI</c> para ignorar mayúsculas y acentos.
/// </summary>
public sealed class ConsultaEspacios : IConsultaEspacios
{
    private const string Collation = "Latin1_General_CI_AI";

    private readonly ApplicationDbContext _contexto;

    /// <summary>Inicializa la consulta con el contexto de lectura.</summary>
    public ConsultaEspacios(ApplicationDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    /// <inheritdoc />
    public async Task<PaginaResultado<EspacioResumen>> ListarAdministrativoAsync(
        FiltroEspaciosAdmin filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = _contexto.Espacios.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Q))
        {
            var texto = filtro.Q.Trim();
            consulta = consulta.Where(espacio =>
                EF.Functions.Collate(espacio.Nombre, Collation).Contains(texto)
                || EF.Functions.Collate(espacio.Ubicacion, Collation).Contains(texto));
        }

        if (filtro.Categoria is { } categoria)
        {
            consulta = consulta.Where(espacio => espacio.Categoria == categoria);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(espacio => espacio.Estado == estado);
        }

        var total = await consulta.CountAsync(cancellationToken);

        var filas = await consulta
            .OrderBy(espacio => espacio.Nombre)
            .ThenBy(espacio => espacio.Ubicacion)
            .Skip((filtro.Pagina - 1) * filtro.Tamano)
            .Take(filtro.Tamano)
            .Select(espacio => new
            {
                espacio.Id,
                espacio.Nombre,
                espacio.Ubicacion,
                espacio.Categoria,
                espacio.Aforo,
                espacio.TipoAprobacion,
                espacio.Estado,
                CodigoQr = espacio.CodigoQR,
                espacio.FotografiaRuta,
                PuestosActivos = espacio.Puestos.Count(puesto => puesto.Estado == EstadoUnidad.Activo),
                espacio.CreadoEn,
                espacio.ActualizadoEn,
                espacio.RowVersion,
                espacio.TipoModalidadPrestamo,
                espacio.TipoElementoPrestamo
            })
            .ToListAsync(cancellationToken);

        var elementos = filas
            .Select(fila => new EspacioResumen(
                fila.Id,
                fila.Nombre,
                fila.Ubicacion,
                fila.Categoria,
                fila.Aforo,
                fila.TipoAprobacion,
                fila.Estado,
                fila.CodigoQr.Valor,
                MapeoEspacios.UrlFotografia(fila.FotografiaRuta),
                fila.PuestosActivos,
                fila.CreadoEn,
                fila.ActualizadoEn,
                Convert.ToBase64String(fila.RowVersion),
                fila.TipoModalidadPrestamo,
                fila.TipoElementoPrestamo))
            .ToArray();

        return new PaginaResultado<EspacioResumen>(elementos, total, filtro.Pagina, filtro.Tamano);
    }

    /// <inheritdoc />
    public async Task<EspacioDetalle?> ObtenerAdministrativoAsync(Guid espacioId, CancellationToken cancellationToken = default)
    {
        var espacio = await _contexto.Espacios
            .AsNoTracking()
            .Include(candidato => candidato.Puestos)
            .FirstOrDefaultAsync(candidato => candidato.Id == espacioId, cancellationToken);

        return espacio is null ? null : MapeoEspacios.ADetalle(espacio);
    }

    /// <inheritdoc />
    public async Task<PaginaResultado<EspacioCatalogo>> ConsultarCatalogoAsync(
        FiltroCatalogo filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = _contexto.Espacios.AsNoTracking()
            .Where(espacio => espacio.Estado == EstadoUnidad.Activo);

        if (!string.IsNullOrWhiteSpace(filtro.Q))
        {
            var texto = filtro.Q.Trim();
            consulta = consulta.Where(espacio =>
                EF.Functions.Collate(espacio.Nombre, Collation).Contains(texto)
                || EF.Functions.Collate(espacio.Ubicacion, Collation).Contains(texto));
        }

        if (filtro.Categoria is { } categoria)
        {
            consulta = consulta.Where(espacio => espacio.Categoria == categoria);
        }

        if (filtro.AforoMinimo is { } aforoMinimo)
        {
            consulta = consulta.Where(espacio => espacio.Aforo >= aforoMinimo);
        }

        if (filtro.AforoMaximo is { } aforoMaximo)
        {
            consulta = consulta.Where(espacio => espacio.Aforo <= aforoMaximo);
        }

        var total = await consulta.CountAsync(cancellationToken);

        var filas = await consulta
            .OrderBy(espacio => espacio.Nombre)
            .ThenBy(espacio => espacio.Ubicacion)
            .Skip((filtro.Pagina - 1) * filtro.Tamano)
            .Take(filtro.Tamano)
            .Select(espacio => new
            {
                espacio.Id,
                espacio.Nombre,
                espacio.Ubicacion,
                espacio.Categoria,
                espacio.Aforo,
                espacio.TipoAprobacion,
                espacio.FotografiaRuta,
                espacio.TipoModalidadPrestamo,
                espacio.TipoElementoPrestamo
            })
            .ToListAsync(cancellationToken);

        var elementos = filas
            .Select(fila => new EspacioCatalogo(
                fila.Id,
                fila.Nombre,
                fila.Ubicacion,
                fila.Categoria,
                fila.Aforo,
                fila.TipoAprobacion,
                MapeoEspacios.UrlFotografia(fila.FotografiaRuta),
                fila.TipoModalidadPrestamo,
                fila.TipoElementoPrestamo))
            .ToArray();

        return new PaginaResultado<EspacioCatalogo>(elementos, total, filtro.Pagina, filtro.Tamano);
    }

    /// <inheritdoc />
    public async Task<EspacioPublico?> ObtenerPublicoAsync(Guid espacioId, CancellationToken cancellationToken = default)
    {
        var fila = await _contexto.Espacios.AsNoTracking()
            .Where(espacio => espacio.Id == espacioId && espacio.Estado == EstadoUnidad.Activo)
            .Select(espacio => new
            {
                espacio.Id,
                espacio.Nombre,
                espacio.Ubicacion,
                espacio.Categoria,
                espacio.Aforo,
                espacio.TipoAprobacion,
                espacio.FotografiaRuta,
                espacio.TipoModalidadPrestamo,
                espacio.TipoElementoPrestamo,
                PuestosActivos = espacio.Puestos.Count(puesto => puesto.Estado == EstadoUnidad.Activo),
                Puestos = espacio.Puestos
                    .Where(puesto => puesto.Estado == EstadoUnidad.Activo)
                    .OrderBy(puesto => puesto.Identificador)
                    .Select(puesto => new PuestoPublico(
                        puesto.Id,
                        puesto.Identificador,
                        puesto.DescripcionEquipo,
                        puesto.Estado))
                    .ToArray()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return fila is null
            ? null
            : new EspacioPublico(
                fila.Id,
                fila.Nombre,
                fila.Ubicacion,
                fila.Categoria,
                fila.Aforo,
                fila.TipoAprobacion,
                MapeoEspacios.UrlFotografia(fila.FotografiaRuta),
                fila.PuestosActivos,
                fila.Puestos,
                fila.TipoModalidadPrestamo,
                fila.TipoElementoPrestamo);
    }

    /// <inheritdoc />
    public async Task<EspacioParaReserva?> ObtenerParaReservaAsync(
        Guid espacioId,
        CancellationToken cancellationToken = default)
    {
        var fila = await _contexto.Espacios
            .AsNoTracking()
            .Where(espacio => espacio.Id == espacioId)
            .Select(espacio => new
            {
                espacio.Id,
                espacio.Nombre,
                espacio.Estado,
                espacio.TipoAprobacion,
                espacio.TipoModalidadPrestamo,
                espacio.TipoElementoPrestamo
            })
            .FirstOrDefaultAsync(cancellationToken);

        return fila is null
            ? null
            : new EspacioParaReserva(
                fila.Id,
                fila.Nombre,
                fila.Estado,
                fila.TipoAprobacion,
                fila.TipoModalidadPrestamo,
                fila.TipoElementoPrestamo);
    }
}
