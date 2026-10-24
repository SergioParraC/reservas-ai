using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Asistencia.Dtos;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Asistencia;

/// <summary>
/// Registra la salida del titular. No exige ventana horaria: basta con que la reserva esté
/// <c>EnUso</c>. Al finalizar se acorta el fin de la franja y el remanente queda disponible.
/// </summary>
public sealed class RegistrarCheckOutHandler
{
    private readonly IReservasRepository _repositorio;
    private readonly IConsultaCheckIn _consulta;
    private readonly IRelojSistema _reloj;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos, el reloj y la identidad actual.</summary>
    public RegistrarCheckOutHandler(
        IReservasRepository repositorio,
        IConsultaCheckIn consulta,
        IRelojSistema reloj,
        IUsuarioActual usuario)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Finaliza la sesión abierta y devuelve la reserva con su fin acortado.</summary>
    public async Task<Resultado<ReservaDetalle>> HandleAsync(
        Guid reservaId,
        CancellationToken cancellationToken = default)
    {
        if (_usuario.UsuarioId is not { } titular)
        {
            return Fallo(TipoFallo.NoAutorizado, MensajesReserva.SinSesion);
        }

        var reserva = await _repositorio.ObtenerAsync(reservaId, cancellationToken);

        if (reserva is null)
        {
            return Fallo(TipoFallo.NoEncontrado, MensajesReserva.ReservaNoEncontrada);
        }

        if (!reserva.PerteneceA(titular))
        {
            return Fallo(TipoFallo.NoPermitido, MensajesReserva.ReservaAjena);
        }

        var vista = await _consulta.ObtenerAsync(reservaId, cancellationToken);

        if (vista is null)
        {
            return Fallo(TipoFallo.NoEncontrado, MensajesReserva.ReservaNoEncontrada);
        }

        try
        {
            reserva.Finalizar(_reloj.Ahora);
        }
        catch (ConflictoException excepcion)
        {
            return Fallo(TipoFallo.Conflicto, excepcion.Message);
        }

        return await _repositorio.GuardarCambiosAsync(cancellationToken)
            ? Resultado<ReservaDetalle>.Exito(MapeoAsistencia.ADetalle(reserva, vista))
            : Fallo(TipoFallo.Conflicto, MensajesReserva.TransicionNoPersistida);
    }

    private static Resultado<ReservaDetalle> Fallo(TipoFallo tipo, string mensaje) =>
        Resultado<ReservaDetalle>.Fallo(tipo, mensaje);
}
