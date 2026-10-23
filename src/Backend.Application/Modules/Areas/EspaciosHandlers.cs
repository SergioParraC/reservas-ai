using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.Application.Modules.Areas;

/// <summary>
/// Caso de uso del alta de áreas: valida unicidad, asigna un código QR único y persiste el agregado.
/// </summary>
public sealed class CrearEspacioHandler
{
    private const int IntentosCodigoQr = 3;

    private readonly IEspaciosRepository _repositorio;
    private readonly OpcionesEspacios _opciones;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos y la política institucional.</summary>
    public CrearEspacioHandler(IEspaciosRepository repositorio, OpcionesEspacios opciones, IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Registra un área activa y le asigna un código QR único.</summary>
    public async Task<Resultado<EspacioDetalle>> HandleAsync(ComandoCrearEspacio comando, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        try
        {
            // La validación de dominio se ejecuta antes de tocar el repositorio.
            var espacio = Espacio.Crear(
                comando.Nombre,
                comando.Ubicacion,
                comando.Categoria,
                comando.Aforo,
                _opciones.AforoMaximo,
                comando.TipoAprobacion,
                _reloj.Ahora,
                CodigoQR.Generar());

            for (var intento = 0; intento < IntentosCodigoQr; intento++)
            {
                if (!await _repositorio.ExisteCodigoQrAsync(espacio.CodigoQR.Valor, cancellationToken))
                {
                    if (await _repositorio.ExisteNombreUbicacionAsync(espacio.Nombre, espacio.Ubicacion, null, cancellationToken))
                    {
                        return ConflictoNombre();
                    }

                    await _repositorio.AgregarAsync(espacio, cancellationToken);

                    return await _repositorio.GuardarCambiosAsync(cancellationToken)
                        ? Resultado<EspacioDetalle>.Exito(MapeoEspacios.ADetalle(espacio))
                        : ConflictoNombre();
                }

                // Colisión despreciable: se reintenta con un código nuevo sin consumir el anterior.
                espacio = Espacio.Crear(
                    comando.Nombre,
                    comando.Ubicacion,
                    comando.Categoria,
                    comando.Aforo,
                    _opciones.AforoMaximo,
                    comando.TipoAprobacion,
                    _reloj.Ahora,
                    CodigoQR.Generar());
            }

            return Resultado<EspacioDetalle>.Fallo(
                TipoFallo.Conflicto,
                "No fue posible asignar un código QR único. Intente nuevamente.");
        }
        catch (ConflictoException excepcion)
        {
            return Resultado<EspacioDetalle>.Fallo(TipoFallo.Conflicto, excepcion.Message);
        }
        catch (DomainException excepcion)
        {
            return Resultado<EspacioDetalle>.Fallo(
                TipoFallo.Validacion,
                excepcion.Message,
                MapeoEspacios.Errores(MapeoEspacios.CampoDe(excepcion.Message), excepcion.Message));
        }
    }

    private static Resultado<EspacioDetalle> ConflictoNombre() =>
        Resultado<EspacioDetalle>.Fallo(
            TipoFallo.Conflicto,
            MensajesEspacio.NombreUbicacionDuplicado,
            MapeoEspacios.Errores("nombre", MensajesEspacio.NombreUbicacionDuplicado));
}

/// <summary>Edita un área conservando siempre su código QR original.</summary>
public sealed class ActualizarEspacioHandler
{
    private readonly IEspaciosRepository _repositorio;
    private readonly OpcionesEspacios _opciones;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos y la política institucional.</summary>
    public ActualizarEspacioHandler(IEspaciosRepository repositorio, OpcionesEspacios opciones, IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Actualiza los datos descriptivos y el estado del área.</summary>
    public async Task<Resultado<EspacioDetalle>> HandleAsync(
        Guid espacioId,
        ComandoActualizarEspacio comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var espacio = await _repositorio.ObtenerAsync(espacioId, cancellationToken);

        if (espacio is null)
        {
            return Resultado<EspacioDetalle>.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado);
        }

        if (!string.IsNullOrWhiteSpace(comando.RowVersion)
            && !string.Equals(comando.RowVersion, Convert.ToBase64String(espacio.RowVersion), StringComparison.Ordinal))
        {
            return ConflictoConcurrencia();
        }

        try
        {
            espacio.Actualizar(
                comando.Nombre,
                comando.Ubicacion,
                comando.Categoria,
                comando.TipoAprobacion,
                comando.Estado,
                comando.Aforo,
                _opciones.AforoMaximo,
                _reloj.Ahora);

            if (await _repositorio.ExisteNombreUbicacionAsync(espacio.Nombre, espacio.Ubicacion, espacio.Id, cancellationToken))
            {
                return Resultado<EspacioDetalle>.Fallo(
                    TipoFallo.Conflicto,
                    MensajesEspacio.NombreUbicacionDuplicado,
                    MapeoEspacios.Errores("nombre", MensajesEspacio.NombreUbicacionDuplicado));
            }

            return await _repositorio.GuardarCambiosAsync(cancellationToken)
                ? Resultado<EspacioDetalle>.Exito(MapeoEspacios.ADetalle(espacio))
                : ConflictoConcurrencia();
        }
        catch (ConflictoException excepcion)
        {
            return Resultado<EspacioDetalle>.Fallo(TipoFallo.Conflicto, excepcion.Message);
        }
        catch (DomainException excepcion)
        {
            return Resultado<EspacioDetalle>.Fallo(
                TipoFallo.Validacion,
                excepcion.Message,
                MapeoEspacios.Errores(MapeoEspacios.CampoDe(excepcion.Message), excepcion.Message));
        }
    }

    private static Resultado<EspacioDetalle> ConflictoConcurrencia() =>
        Resultado<EspacioDetalle>.Fallo(
            TipoFallo.Conflicto,
            "El área fue modificada por otra sesión. Recargue los datos e intente nuevamente.");
}

/// <summary>Da de baja lógica un área; el registro y su QR se conservan.</summary>
public sealed class DesactivarEspacioHandler
{
    private readonly IEspaciosRepository _repositorio;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos.</summary>
    public DesactivarEspacioHandler(IEspaciosRepository repositorio, IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Desactiva el área indicada.</summary>
    public async Task<Resultado> HandleAsync(Guid espacioId, CancellationToken cancellationToken = default)
    {
        var espacio = await _repositorio.ObtenerAsync(espacioId, cancellationToken);

        if (espacio is null)
        {
            return Resultado.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado);
        }

        espacio.Desactivar(_reloj.Ahora);

        return await _repositorio.GuardarCambiosAsync(cancellationToken)
            ? Resultado.Exito()
            : Resultado.Fallo(TipoFallo.Conflicto, "No fue posible dar de baja el área. Intente nuevamente.");
    }
}

/// <summary>Consulta el detalle administrativo de un área.</summary>
public sealed class ObtenerEspacioHandler
{
    private readonly IConsultaEspacios _consulta;

    /// <summary>Inicializa el caso de uso con el puerto de lectura.</summary>
    public ObtenerEspacioHandler(IConsultaEspacios consulta)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    /// <summary>Devuelve el detalle del área o un fallo de recurso inexistente.</summary>
    public async Task<Resultado<EspacioDetalle>> HandleAsync(Guid espacioId, CancellationToken cancellationToken = default)
    {
        var detalle = await _consulta.ObtenerAdministrativoAsync(espacioId, cancellationToken);

        return detalle is null
            ? Resultado<EspacioDetalle>.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado)
            : Resultado<EspacioDetalle>.Exito(detalle);
    }
}

/// <summary>Lista áreas con filtros administrativos, incluidas las dadas de baja.</summary>
public sealed class ListarEspaciosHandler
{
    private readonly IConsultaEspacios _consulta;

    /// <summary>Inicializa el caso de uso con el puerto de lectura.</summary>
    public ListarEspaciosHandler(IConsultaEspacios consulta)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    /// <summary>Ejecuta la consulta paginada.</summary>
    public Task<PaginaResultado<EspacioResumen>> HandleAsync(FiltroEspaciosAdmin filtro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var normalizado = filtro with
        {
            Q = string.IsNullOrWhiteSpace(filtro.Q) ? null : filtro.Q.Trim(),
            Categoria = filtro.Categoria is { } categoria && Enum.IsDefined(categoria) ? categoria : null,
            Estado = filtro.Estado is { } estado && Enum.IsDefined(estado) ? estado : null,
            Pagina = Paginacion.NormalizarPagina(filtro.Pagina),
            Tamano = Paginacion.NormalizarTamano(filtro.Tamano)
        };

        return _consulta.ListarAdministrativoAsync(normalizado, cancellationToken);
    }
}
