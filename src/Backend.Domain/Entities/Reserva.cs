using System.Globalization;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.Domain.Entities;

/// <summary>
/// Reserva puntual de un puesto de cómputo. Es una raíz de agregado propia: referencia al
/// puesto por identidad y concentra las invariantes de franja, estado y titularidad.
/// </summary>
public sealed class Reserva
{
    private Reserva()
    {
    }

    private Reserva(Guid id, Guid puestoId, Guid usuarioId, FranjaHoraria franja, DateTimeOffset creadaEn)
    {
        Id = id;
        PuestoId = puestoId;
        UsuarioId = usuarioId;
        Inicio = franja.Inicio;
        Fin = franja.Fin;
        FinPlanificado = franja.Fin;
        Estado = EstadoReserva.Confirmada;
        CreadaEn = creadaEn;
    }

    /// <summary>Identificador interno de la reserva.</summary>
    public Guid Id { get; private set; }

    /// <summary>Puesto reservado; el código QR del puesto no se altera.</summary>
    public Guid PuestoId { get; private set; }

    /// <summary>Titular de la reserva.</summary>
    public Guid UsuarioId { get; private set; }

    /// <summary>Inicio de la franja reservada.</summary>
    public DateTimeOffset Inicio { get; private set; }

    /// <summary>Fin de la franja reservada; se acorta al registrar una salida anticipada.</summary>
    public DateTimeOffset Fin { get; private set; }

    /// <summary>Fin pactado en el alta; nunca cambia y sirve de techo al check-out.</summary>
    public DateTimeOffset FinPlanificado { get; private set; }

    /// <summary>Estado transaccional de la reserva.</summary>
    public EstadoReserva Estado { get; private set; }

    /// <summary>Momento del alta.</summary>
    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>Momento de la cancelación o de la liberación por inasistencia, si ocurrió.</summary>
    public DateTimeOffset? CanceladaEn { get; private set; }

    /// <summary>Momento del ingreso confirmado con el QR del puesto, si ocurrió.</summary>
    public DateTimeOffset? FechaCheckIn { get; private set; }

    /// <summary>Momento de la salida registrada por el titular, si ocurrió.</summary>
    public DateTimeOffset? FechaCheckOut { get; private set; }

    /// <summary>Token de concurrencia optimista gestionado por la base de datos.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Franja de la reserva como valor derivado de las columnas persistidas.</summary>
    public FranjaHoraria Franja => FranjaHoraria.Desde(Inicio, Fin);

    /// <summary>Indica si la reserva sigue bloqueando su franja.</summary>
    public bool EstaVigente => Estado is EstadoReserva.Confirmada or EstadoReserva.EnUso;

    /// <summary>Crea una reserva confirmada de inmediato para el titular.</summary>
    public static Reserva Confirmar(
        Guid puestoId,
        Guid usuarioId,
        FranjaHoraria franja,
        DateTimeOffset creadaEn)
    {
        ArgumentNullException.ThrowIfNull(franja);

        if (puestoId == Guid.Empty || usuarioId == Guid.Empty)
        {
            throw new DomainException(MensajesReserva.PuestoNoEncontrado);
        }

        return new Reserva(Guid.NewGuid(), puestoId, usuarioId, franja, creadaEn);
    }

    /// <summary>Indica si la reserva pertenece al usuario indicado.</summary>
    public bool PerteneceA(Guid usuarioId) => UsuarioId == usuarioId;

    /// <summary>Indica si el titular aún puede cancelar con el margen configurado.</summary>
    public bool PuedeCancelarseEn(DateTimeOffset momento, PoliticaReserva politica)
    {
        ArgumentNullException.ThrowIfNull(politica);

        return Estado == EstadoReserva.Confirmada && momento <= Inicio - politica.AntelacionCancelacion;
    }

    /// <summary>
    /// Indica si el instante cae dentro de la ventana simétrica de check-in
    /// <c>[Inicio - tolerancia, Inicio + tolerancia]</c>. La tolerancia llega de configuración.
    /// </summary>
    public bool EstaEnVentanaDeCheckIn(DateTimeOffset momento, TimeSpan tolerancia) =>
        momento >= Inicio - tolerancia && momento <= Inicio + tolerancia;

    /// <summary>
    /// Confirma la presencia física del titular: solo desde <see cref="EstadoReserva.Confirmada"/> y
    /// dentro de la ventana de tolerancia; registra la marca real del ingreso.
    /// </summary>
    public void RegistrarCheckIn(DateTimeOffset momento, TimeSpan tolerancia)
    {
        if (Estado != EstadoReserva.Confirmada)
        {
            throw new ConflictoException(MensajesReserva.TransicionInvalida);
        }

        if (!EstaEnVentanaDeCheckIn(momento, tolerancia))
        {
            throw new ConflictoException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesReserva.CheckInFueraDeVentana,
                Inicio - tolerancia,
                Inicio + tolerancia));
        }

        Estado = EstadoReserva.EnUso;
        FechaCheckIn = momento;
    }

    /// <summary>
    /// Registra la salida desde <see cref="EstadoReserva.EnUso"/>, sin ventana horaria. Acorta
    /// <see cref="Fin"/> al momento de salida con piso en <see cref="Inicio"/> y techo en
    /// <see cref="FinPlanificado"/>: el remanente vuelve a estar disponible de inmediato.
    /// </summary>
    public void Finalizar(DateTimeOffset momento)
    {
        if (Estado != EstadoReserva.EnUso)
        {
            throw new ConflictoException(MensajesReserva.TransicionInvalida);
        }

        Estado = EstadoReserva.Finalizada;
        FechaCheckOut = momento;

        var finAcortado = momento < FinPlanificado ? momento : FinPlanificado;
        Fin = finAcortado < Inicio ? Inicio : finAcortado;
    }

    /// <summary>
    /// Cancela la reserva confirmada respetando el margen de antelación. El código QR del
    /// puesto no se modifica: solo cambia el estado transaccional.
    /// </summary>
    public void Cancelar(DateTimeOffset momento, PoliticaReserva politica)
    {
        ArgumentNullException.ThrowIfNull(politica);

        if (Estado != EstadoReserva.Confirmada)
        {
            throw new ConflictoException(MensajesReserva.EstadoNoCancelable);
        }

        if (!PuedeCancelarseEn(momento, politica))
        {
            throw new ConflictoException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesReserva.FueraDePlazo,
                (int)politica.AntelacionCancelacion.TotalMinutes));
        }

        Estado = EstadoReserva.Cancelada;
        CanceladaEn = momento;
    }

    /// <summary>
    /// Marca la reserva como liberada por inasistencia. Solo aplica a reservas confirmadas que
    /// nunca registraron ingreso; una sesión <see cref="EstadoReserva.EnUso"/> se cierra con el
    /// check-out del titular.
    /// </summary>
    public void LiberarPorNoShow(DateTimeOffset momento)
    {
        if (Estado != EstadoReserva.Confirmada)
        {
            throw new ConflictoException(MensajesReserva.TransicionInvalida);
        }

        Estado = EstadoReserva.NoShow;
        CanceladaEn = momento;
    }
}
