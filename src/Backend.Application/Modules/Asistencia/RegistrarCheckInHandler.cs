using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Asistencia.Dtos;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.Application.Modules.Asistencia;

/// <summary>
/// Registra la presencia física del titular comparando el código escaneado con el QR estático del
/// puesto reservado. La ventana se toma de <see cref="OpcionesReservas.ObtenerVentanaTolerancia"/>
/// y se evalúa con el reloj del servidor: el cliente nunca envía la hora.
/// </summary>
public sealed class RegistrarCheckInHandler
{
    private readonly IReservasRepository _repositorio;
    private readonly IConsultaCheckIn _consulta;
    private readonly OpcionesReservas _opciones;
    private readonly IRelojSistema _reloj;
    private readonly IUsuarioActual _usuario;

    /// <summary>Inicializa el caso de uso con sus puertos, la configuración y la identidad actual.</summary>
    public RegistrarCheckInHandler(
        IReservasRepository repositorio,
        IConsultaCheckIn consulta,
        OpcionesReservas opciones,
        IRelojSistema reloj,
        IUsuarioActual usuario)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _opciones = opciones ?? throw new ArgumentNullException(nameof(opciones));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _usuario = usuario ?? throw new ArgumentNullException(nameof(usuario));
    }

    /// <summary>Confirma el ingreso y devuelve la reserva en estado <c>EnUso</c>.</summary>
    public async Task<Resultado<ReservaDetalle>> HandleAsync(
        ComandoRegistrarCheckIn comando,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (_usuario.UsuarioId is not { } titular)
        {
            return Fallo(TipoFallo.NoAutorizado, MensajesReserva.SinSesion);
        }

        var reserva = await _repositorio.ObtenerAsync(comando.ReservaId, cancellationToken);

        if (reserva is null)
        {
            return Fallo(TipoFallo.NoEncontrado, MensajesReserva.ReservaNoEncontrada);
        }

        if (!reserva.PerteneceA(titular))
        {
            return Fallo(TipoFallo.NoPermitido, MensajesReserva.ReservaAjena);
        }

        var vista = await _consulta.ObtenerAsync(comando.ReservaId, cancellationToken);

        if (vista is null)
        {
            return Fallo(TipoFallo.NoEncontrado, MensajesReserva.ReservaNoEncontrada);
        }

        if (reserva.Estado != EstadoReserva.Confirmada)
        {
            return Fallo(TipoFallo.Conflicto, MensajesReserva.TransicionInvalida);
        }

        if (!CodigoCorrespondeA(comando.CodigoQr, vista.CodigoQr))
        {
            return Fallo(TipoFallo.Conflicto, MensajesReserva.CheckInCodigoInvalido);
        }

        try
        {
            reserva.RegistrarCheckIn(_reloj.Ahora, _opciones.ObtenerVentanaTolerancia());
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

    private static bool CodigoCorrespondeA(string? recibido, string esperado)
    {
        if (string.IsNullOrWhiteSpace(recibido))
        {
            return false;
        }

        try
        {
            return string.Equals(CodigoQR.Desde(recibido).Valor, esperado.Trim(), StringComparison.Ordinal);
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
