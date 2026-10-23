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

    /// <summary>Fin de la franja reservada.</summary>
    public DateTimeOffset Fin { get; private set; }

    /// <summary>Estado transaccional de la reserva.</summary>
    public EstadoReserva Estado { get; private set; }

    /// <summary>Momento del alta.</summary>
    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>Momento de la cancelación, si ocurrió.</summary>
    public DateTimeOffset? CanceladaEn { get; private set; }

    /// <summary>Token de concurrencia optimista gestionado por la base de datos.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Franja de la reserva como valor derivado de las columnas persistidas.</summary>
    public FranjaHoraria Franja => FranjaHoraria.Desde(Inicio, Fin);

    /// <summary>Indica si la reserva sigue bloqueando su franja.</summary>
    public bool EstaVigente => Estado == EstadoReserva.Confirmada;

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

        return EstaVigente && momento <= Inicio - politica.AntelacionCancelacion;
    }

    /// <summary>
    /// Cancela la reserva confirmada respetando el margen de antelación. El código QR del
    /// puesto no se modifica: solo cambia el estado transaccional.
    /// </summary>
    public void Cancelar(DateTimeOffset momento, PoliticaReserva politica)
    {
        ArgumentNullException.ThrowIfNull(politica);

        if (!EstaVigente)
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

    /// <summary>Marca la reserva como liberada por inasistencia; reservado para la Etapa 3.</summary>
    public void LiberarPorNoShow(DateTimeOffset momento)
    {
        if (!EstaVigente)
        {
            throw new ConflictoException(MensajesReserva.EstadoNoCancelable);
        }

        Estado = EstadoReserva.CanceladaPorNoShow;
        CanceladaEn = momento;
    }
}
