using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Areas;

/// <summary>Caso de uso del alta de un puesto dentro del agregado del área.</summary>
public sealed class CrearPuestoHandler
{
    private readonly IEspaciosRepository _repositorio;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos.</summary>
    public CrearPuestoHandler(IEspaciosRepository repositorio, IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Agrega un puesto al área validando el aforo y la unicidad del identificador.</summary>
    public async Task<Resultado<PuestoDetalle>> HandleAsync(
        Guid espacioId,
        ComandoCrearPuesto comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var espacio = await _repositorio.ObtenerAsync(espacioId, cancellationToken);

        if (espacio is null)
        {
            return Resultado<PuestoDetalle>.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado);
        }

        try
        {
            var puesto = espacio.AgregarPuesto(comando.Identificador, comando.DescripcionEquipo, _reloj.Ahora);

            return await _repositorio.GuardarCambiosAsync(cancellationToken)
                ? Resultado<PuestoDetalle>.Exito(MapeoEspacios.ADetalle(puesto))
                : Resultado<PuestoDetalle>.Fallo(TipoFallo.Conflicto, "No fue posible registrar el puesto. Intente nuevamente.");
        }
        catch (ConflictoException excepcion)
        {
            return Resultado<PuestoDetalle>.Fallo(TipoFallo.Conflicto, excepcion.Message);
        }
        catch (DomainException excepcion)
        {
            return Resultado<PuestoDetalle>.Fallo(
                TipoFallo.Validacion,
                excepcion.Message,
                MapeoEspacios.Errores(MapeoEspacios.CampoDe(excepcion.Message), excepcion.Message));
        }
    }
}

/// <summary>Actualiza un puesto existente sin permitir cambiar su código QR.</summary>
public sealed class ActualizarPuestoHandler
{
    private readonly IEspaciosRepository _repositorio;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos.</summary>
    public ActualizarPuestoHandler(IEspaciosRepository repositorio, IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Modifica los datos descriptivos y el estado del puesto.</summary>
    public async Task<Resultado<PuestoDetalle>> HandleAsync(
        Guid espacioId,
        Guid puestoId,
        ComandoActualizarPuesto comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var espacio = await _repositorio.ObtenerAsync(espacioId, cancellationToken);

        if (espacio is null)
        {
            return Resultado<PuestoDetalle>.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado);
        }

        try
        {
            var puesto = espacio.ActualizarPuesto(
                puestoId,
                comando.Identificador,
                comando.DescripcionEquipo,
                comando.Estado,
                _reloj.Ahora);

            return await _repositorio.GuardarCambiosAsync(cancellationToken)
                ? Resultado<PuestoDetalle>.Exito(MapeoEspacios.ADetalle(puesto))
                : Resultado<PuestoDetalle>.Fallo(TipoFallo.Conflicto, "No fue posible actualizar el puesto. Intente nuevamente.");
        }
        catch (ConflictoException excepcion)
        {
            return Resultado<PuestoDetalle>.Fallo(TipoFallo.Conflicto, excepcion.Message);
        }
        catch (DomainException excepcion)
        {
            return ExcepcionDePuesto.EsNoEncontrado(excepcion)
                ? Resultado<PuestoDetalle>.Fallo(TipoFallo.NoEncontrado, excepcion.Message)
                : Resultado<PuestoDetalle>.Fallo(
                    TipoFallo.Validacion,
                    excepcion.Message,
                    MapeoEspacios.Errores(MapeoEspacios.CampoDe(excepcion.Message), excepcion.Message));
        }
    }
}

/// <summary>Da de baja lógica un puesto conservando su código QR.</summary>
public sealed class DesactivarPuestoHandler
{
    private readonly IEspaciosRepository _repositorio;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos.</summary>
    public DesactivarPuestoHandler(IEspaciosRepository repositorio, IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Desactiva el puesto indicado del área.</summary>
    public async Task<Resultado> HandleAsync(Guid espacioId, Guid puestoId, CancellationToken cancellationToken = default)
    {
        var espacio = await _repositorio.ObtenerAsync(espacioId, cancellationToken);

        if (espacio is null)
        {
            return Resultado.Fallo(TipoFallo.NoEncontrado, MensajesEspacio.EspacioNoEncontrado);
        }

        try
        {
            espacio.DesactivarPuesto(puestoId, _reloj.Ahora);
        }
        catch (DomainException excepcion)
        {
            return Resultado.Fallo(
                ExcepcionDePuesto.EsNoEncontrado(excepcion) ? TipoFallo.NoEncontrado : TipoFallo.Validacion,
                excepcion.Message);
        }

        return await _repositorio.GuardarCambiosAsync(cancellationToken)
            ? Resultado.Exito()
            : Resultado.Fallo(TipoFallo.Conflicto, "No fue posible dar de baja el puesto. Intente nuevamente.");
    }
}

/// <summary>Clasifica los mensajes de dominio propios de los puestos.</summary>
internal static class ExcepcionDePuesto
{
    /// <summary>Indica si el fallo corresponde a un puesto inexistente en el área.</summary>
    public static bool EsNoEncontrado(DomainException excepcion) =>
        string.Equals(excepcion.Message, MensajesEspacio.PuestoInexistente, StringComparison.Ordinal);
}
