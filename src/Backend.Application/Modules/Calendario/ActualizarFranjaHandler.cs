using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Calendario.Dtos;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Calendario;

/// <summary>Edita una franja no operativa conservando su vigencia.</summary>
public sealed class ActualizarFranjaHandler
{
    private readonly IFranjasNoOperativasRepository _repositorio;
    private readonly IConsultaCalendario _consulta;

    /// <summary>Inicializa el caso de uso con sus puertos.</summary>
    public ActualizarFranjaHandler(
        IFranjasNoOperativasRepository repositorio,
        IConsultaCalendario consulta)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
    }

    /// <summary>Actualiza la franja si no queda duplicada con otra.</summary>
    public async Task<Resultado<FranjaNoOperativaDetalle>> HandleAsync(
        Guid franjaId,
        ComandoActualizarFranjaNoOperativa comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var franja = await _repositorio.ObtenerAsync(franjaId, cancellationToken);

        if (franja is null)
        {
            return Resultado<FranjaNoOperativaDetalle>.Fallo(
                TipoFallo.NoEncontrado,
                MensajesCalendario.FranjaNoEncontrada);
        }

        if (!string.IsNullOrWhiteSpace(comando.RowVersion)
            && !string.Equals(comando.RowVersion, Convert.ToBase64String(franja.RowVersion), StringComparison.Ordinal))
        {
            return AyudanteCalendario.ConflictoConcurrencia<FranjaNoOperativaDetalle>();
        }

        try
        {
            franja.Actualizar(
                comando.DiaSemana,
                comando.FechaEspecifica,
                comando.HoraInicio,
                comando.HoraFin,
                comando.EsFeriado,
                comando.Motivo);

            if (await _repositorio.ExisteDuplicadaAsync(
                    franja.DiaSemana,
                    franja.FechaEspecifica,
                    franja.HoraInicio,
                    franja.HoraFin,
                    franja.Id,
                    cancellationToken))
            {
                return Resultado<FranjaNoOperativaDetalle>.Fallo(
                    TipoFallo.Conflicto,
                    MensajesCalendario.FranjaDuplicada);
            }

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
