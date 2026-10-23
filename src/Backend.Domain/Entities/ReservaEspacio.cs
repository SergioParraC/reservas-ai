using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.Domain.Entities;

/// <summary>
/// Fila de la agenda única del área. Reúne las ocurrencias de series, las solicitudes aprobadas
/// y compite con las reservas puntuales de los puestos del área.
/// </summary>
public sealed class ReservaEspacio
{
    private ReservaEspacio()
    {
    }

    private ReservaEspacio(
        Guid id,
        Guid espacioId,
        Guid titularId,
        OrigenOcupacion origen,
        Guid origenId,
        FranjaHoraria franja,
        DateTimeOffset creadaEn)
    {
        Id = id;
        EspacioId = espacioId;
        TitularId = titularId;
        Origen = origen;
        OrigenId = origenId;
        Inicio = franja.Inicio;
        Fin = franja.Fin;
        Estado = EstadoOcupacion.Activa;
        CreadaEn = creadaEn;
    }

    /// <summary>Identificador interno de la ocupación.</summary>
    public Guid Id { get; private set; }

    /// <summary>Área ocupada.</summary>
    public Guid EspacioId { get; private set; }

    /// <summary>Titular de la ocupación (docente o solicitante).</summary>
    public Guid TitularId { get; private set; }

    /// <summary>Procedencia de la ocupación, conservada para trazabilidad.</summary>
    public OrigenOcupacion Origen { get; private set; }

    /// <summary>Identificador de la serie o solicitud que originó la ocupación.</summary>
    public Guid OrigenId { get; private set; }

    /// <summary>Inicio de la franja ocupada.</summary>
    public DateTimeOffset Inicio { get; private set; }

    /// <summary>Fin de la franja ocupada.</summary>
    public DateTimeOffset Fin { get; private set; }

    /// <summary>Estado transaccional de la ocupación.</summary>
    public EstadoOcupacion Estado { get; private set; }

    /// <summary>Momento del alta.</summary>
    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>Token de concurrencia optimista gestionado por la base de datos.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Franja ocupada como valor derivado de las columnas persistidas.</summary>
    public FranjaHoraria Franja => FranjaHoraria.Desde(Inicio, Fin);

    /// <summary>Indica si la fila sigue bloqueando la agenda del área.</summary>
    public bool EstaActiva => Estado == EstadoOcupacion.Activa;

    /// <summary>Crea una ocupación activa de la agenda del área.</summary>
    public static ReservaEspacio Crear(
        Guid espacioId,
        Guid titularId,
        OrigenOcupacion origen,
        Guid origenId,
        FranjaHoraria franja,
        DateTimeOffset creadaEn)
    {
        ArgumentNullException.ThrowIfNull(franja);

        if (espacioId == Guid.Empty)
        {
            throw new DomainException(MensajesSemestre.EspacioNoEncontrado);
        }

        if (titularId == Guid.Empty)
        {
            throw new DomainException(MensajesReserva.SinSesion);
        }

        if (!Enum.IsDefined(origen))
        {
            throw new DomainException(MensajesSolicitud.TransicionInvalida);
        }

        return new ReservaEspacio(Guid.NewGuid(), espacioId, titularId, origen, origenId, franja, creadaEn);
    }

    /// <summary>Libera la franja ocupada.</summary>
    public void Cancelar()
    {
        if (!EstaActiva)
        {
            throw new ConflictoException(MensajesSolicitud.FranjaNoDisponible);
        }

        Estado = EstadoOcupacion.Cancelada;
    }
}
