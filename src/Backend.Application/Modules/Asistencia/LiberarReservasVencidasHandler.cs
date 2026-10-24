using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Asistencia.Dtos;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Asistencia;

/// <summary>
/// Libera en un solo lote las reservas confirmadas cuya ventana de check-in venció. Es idempotente
/// (solo toma reservas confirmadas), no toca las sesiones <c>EnUso</c> y confirma el lote una sola
/// vez: si el guardado falla, el siguiente ciclo lo reintenta completo.
/// </summary>
public sealed class LiberarReservasVencidasHandler
{
    private readonly IReservasRepository _repositorio;
    private readonly OpcionesReservas _opcionesReservas;
    private readonly OpcionesCheckIn _opcionesCheckIn;
    private readonly IRelojSistema _reloj;

    /// <summary>Inicializa el caso de uso con el repositorio, la configuración del barrido y el reloj.</summary>
    public LiberarReservasVencidasHandler(
        IReservasRepository repositorio,
        OpcionesReservas opcionesReservas,
        OpcionesCheckIn opcionesCheckIn,
        IRelojSistema reloj)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _opcionesReservas = opcionesReservas ?? throw new ArgumentNullException(nameof(opcionesReservas));
        _opcionesCheckIn = opcionesCheckIn ?? throw new ArgumentNullException(nameof(opcionesCheckIn));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
    }

    /// <summary>Ejecuta un barrido y devuelve las reservas liberadas en este ciclo.</summary>
    public async Task<Resultado<ResultadoLiberacion>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.Ahora;

        var vencidas = await _repositorio.ListarSinCheckInVencidasAsync(
            ahora,
            _opcionesReservas.ObtenerVentanaTolerancia(),
            _opcionesCheckIn.MaximoReservasPorBarrido,
            cancellationToken);

        if (vencidas.Count == 0)
        {
            return Resultado<ResultadoLiberacion>.Exito(new ResultadoLiberacion(0, []));
        }

        var liberadas = new List<Guid>(vencidas.Count);

        foreach (var reserva in vencidas)
        {
            try
            {
                reserva.LiberarPorNoShow(ahora);
                liberadas.Add(reserva.Id);
            }
            catch (ConflictoException)
            {
                // Otra ejecución la liberó entre la lectura y el cambio: el barrido es idempotente.
            }
        }

        if (liberadas.Count == 0)
        {
            return Resultado<ResultadoLiberacion>.Exito(new ResultadoLiberacion(0, []));
        }

        return await _repositorio.GuardarCambiosAsync(cancellationToken)
            ? Resultado<ResultadoLiberacion>.Exito(new ResultadoLiberacion(liberadas.Count, liberadas))
            : Resultado<ResultadoLiberacion>.Fallo(TipoFallo.Conflicto, MensajesReserva.LiberacionNoPersistida);
    }
}
