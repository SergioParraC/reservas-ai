using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Calendario.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Calendario;

/// <summary>Registra una franja no operativa rechazando duplicados exactos.</summary>
public sealed class CrearFranjaHandler
{
    private readonly IFranjasNoOperativasRepository _repositorio;
    private readonly IConsultaCalendario _consulta;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con sus puertos y el reloj.</summary>
    public CrearFranjaHandler(
        IFranjasNoOperativasRepository repositorio,
        IConsultaCalendario consulta,
        IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Registra la franja si no existe otra equivalente.</summary>
    public async Task<Resultado<FranjaNoOperativaDetalle>> HandleAsync(
        ComandoCrearFranjaNoOperativa comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        try
        {
            var franja = FranjaNoOperativa.Crear(
                comando.DiaSemana,
                comando.FechaEspecifica,
                comando.HoraInicio,
                comando.HoraFin,
                comando.EsFeriado,
                comando.Motivo,
                _reloj.Ahora);

            if (await _repositorio.ExisteDuplicadaAsync(
                    franja.DiaSemana,
                    franja.FechaEspecifica,
                    franja.HoraInicio,
                    franja.HoraFin,
                    null,
                    cancellationToken))
            {
                return Resultado<FranjaNoOperativaDetalle>.Fallo(
                    TipoFallo.Conflicto,
                    MensajesCalendario.FranjaDuplicada);
            }

            await _repositorio.AgregarAsync(franja, cancellationToken);

            if (!await _repositorio.GuardarCambiosAsync(cancellationToken))
            {
                return AyudanteCalendario.ConflictoConcurrencia<FranjaNoOperativaDetalle>();
            }

            _consulta.Invalidar();

            return Resultado<FranjaNoOperativaDetalle>.Exito(MapeoCalendario.ADetalle(franja));
        }
        catch (DomainException excepcion)
        {
            return Resultado<FranjaNoOperativaDetalle>.Fallo(
                TipoFallo.Validacion,
                excepcion.Message,
                MapeoCalendario.Errores(AyudanteCalendario.CampoDe(excepcion.Message), excepcion.Message));
        }
    }
}

/// <summary>Da de baja lógica una franja no operativa.</summary>
public sealed class EliminarFranjaHandler
{
    private readonly IFranjasNoOperativasRepository _repositorio;
    private readonly IConsultaCalendario _consulta;

    /// <summary>Inicializa el caso de uso con sus puertos.</summary>
    public EliminarFranjaHandler(
        IFranjasNoOperativasRepository repositorio,
        IConsultaCalendario consulta)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    /// <summary>Desactiva la franja indicada.</summary>
    public async Task<Resultado> HandleAsync(Guid franjaId, CancellationToken cancellationToken = default)
    {
        var franja = await _repositorio.ObtenerAsync(franjaId, cancellationToken);

        if (franja is null)
        {
            return Resultado.Fallo(TipoFallo.NoEncontrado, MensajesCalendario.FranjaNoEncontrada);
        }

        franja.Desactivar();

        if (!await _repositorio.GuardarCambiosAsync(cancellationToken))
        {
            return Resultado.Fallo(TipoFallo.Conflicto, "La franja fue modificada por otra sesión.");
        }

        _consulta.Invalidar();

        return Resultado.Exito();
    }
}
