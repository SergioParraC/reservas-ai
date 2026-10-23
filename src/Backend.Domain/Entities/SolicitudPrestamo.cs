using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.Domain.Entities;

/// <summary>
/// Solicitud formal de préstamo de un área completa con su pre-informe PDF. Es una raíz de
/// agregado con una máquina de estados explícita y motivo obligatorio de rechazo.
/// </summary>
public sealed class SolicitudPrestamo
{
    /// <summary>Longitud máxima de la materia.</summary>
    public const int LongitudMaximaMateria = 120;

    /// <summary>Longitud máxima del nombre del docente responsable.</summary>
    public const int LongitudMaximaDocente = 150;

    /// <summary>Longitud máxima de la justificación.</summary>
    public const int LongitudMaximaJustificacion = 1000;

    /// <summary>Longitud máxima del motivo de rechazo.</summary>
    public const int LongitudMaximaMotivoRechazo = 500;

    private SolicitudPrestamo()
    {
    }

    private SolicitudPrestamo(
        Guid id,
        Guid solicitanteId,
        Guid espacioId,
        FranjaHoraria franja,
        string materia,
        string docenteResponsable,
        string justificacion,
        string archivoRuta,
        string archivoNombreOriginal,
        long archivoTamanoBytes,
        DateTimeOffset radicadaEn)
    {
        Id = id;
        SolicitanteId = solicitanteId;
        EspacioId = espacioId;
        Inicio = franja.Inicio;
        Fin = franja.Fin;
        Materia = materia;
        DocenteResponsable = docenteResponsable;
        Justificacion = justificacion;
        ArchivoRuta = archivoRuta;
        ArchivoNombreOriginal = archivoNombreOriginal;
        ArchivoTamanoBytes = archivoTamanoBytes;
        Estado = EstadoSolicitud.Radicado;
        RadicadaEn = radicadaEn;
    }

    /// <summary>Identificador interno de la solicitud.</summary>
    public Guid Id { get; private set; }

    /// <summary>Autor de la solicitud.</summary>
    public Guid SolicitanteId { get; private set; }

    /// <summary>Área solicitada.</summary>
    public Guid EspacioId { get; private set; }

    /// <summary>Inicio de la franja solicitada.</summary>
    public DateTimeOffset Inicio { get; private set; }

    /// <summary>Fin de la franja solicitada.</summary>
    public DateTimeOffset Fin { get; private set; }

    /// <summary>Materia o actividad de la solicitud.</summary>
    public string Materia { get; private set; } = string.Empty;

    /// <summary>Docente responsable de la actividad.</summary>
    public string DocenteResponsable { get; private set; } = string.Empty;

    /// <summary>Justificación de la solicitud.</summary>
    public string Justificacion { get; private set; } = string.Empty;

    /// <summary>Ruta relativa del pre-informe dentro del almacén; nunca la ruta física.</summary>
    public string ArchivoRuta { get; private set; } = string.Empty;

    /// <summary>Nombre original del archivo, solo como metadato de descarga.</summary>
    public string ArchivoNombreOriginal { get; private set; } = string.Empty;

    /// <summary>Tamaño del archivo en bytes.</summary>
    public long ArchivoTamanoBytes { get; private set; }

    /// <summary>Estado actual dentro de la máquina de estados.</summary>
    public EstadoSolicitud Estado { get; private set; }

    /// <summary>Momento de la radicación.</summary>
    public DateTimeOffset RadicadaEn { get; private set; }

    /// <summary>Momento de la última decisión, si ocurrió.</summary>
    public DateTimeOffset? RevisadaEn { get; private set; }

    /// <summary>Revisor que tomó o decidió la solicitud.</summary>
    public Guid? RevisorId { get; private set; }

    /// <summary>Motivo registrado en el rechazo.</summary>
    public string? MotivoRechazo { get; private set; }

    /// <summary>Token de concurrencia optimista gestionado por la base de datos.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Franja solicitada como valor derivado de las columnas persistidas.</summary>
    public FranjaHoraria Franja => FranjaHoraria.Desde(Inicio, Fin);

    /// <summary>Indica si la solicitud ya no admite transiciones.</summary>
    public bool EsTerminal => Estado is EstadoSolicitud.Aprobado or EstadoSolicitud.Rechazado;

    /// <summary>Indica si la solicitud pertenece al usuario indicado.</summary>
    public bool PerteneceA(Guid usuarioId) => SolicitanteId == usuarioId;

    /// <summary>
    /// Radica una solicitud en estado <see cref="EstadoSolicitud.Radicado"/> con los metadatos
    /// del pre-informe ya almacenado.
    /// </summary>
    public static SolicitudPrestamo Radicar(
        Guid solicitanteId,
        Guid espacioId,
        FranjaHoraria franja,
        string materia,
        string docenteResponsable,
        string justificacion,
        string archivoRuta,
        string archivoNombreOriginal,
        long archivoTamanoBytes,
        DateTimeOffset radicadaEn,
        Guid? solicitudId = null)
    {
        ArgumentNullException.ThrowIfNull(franja);

        if (solicitanteId == Guid.Empty)
        {
            throw new DomainException(MensajesReserva.SinSesion);
        }

        if (espacioId == Guid.Empty)
        {
            throw new DomainException(MensajesEspacio.EspacioNoEncontrado);
        }

        if (archivoTamanoBytes <= 0)
        {
            throw new DomainException(MensajesSolicitud.ArchivoRequerido);
        }

        return new SolicitudPrestamo(
            solicitudId ?? Guid.NewGuid(),
            solicitanteId,
            espacioId,
            franja,
            Normalizar(materia, LongitudMaximaMateria, MensajesSolicitud.MateriaRequerida, MensajesSolicitud.MateriaDemasiadoLarga),
            Normalizar(docenteResponsable, LongitudMaximaDocente, MensajesSolicitud.DocenteRequerido, MensajesSolicitud.DocenteDemasiadoLargo),
            Normalizar(justificacion, LongitudMaximaJustificacion, MensajesSolicitud.JustificacionRequerida, MensajesSolicitud.JustificacionDemasiadoLarga),
            Normalizar(archivoRuta, 300, MensajesSolicitud.ArchivoRequerido, MensajesSolicitud.ArchivoRequerido),
            Normalizar(archivoNombreOriginal, 260, MensajesSolicitud.ArchivoRequerido, MensajesSolicitud.ArchivoRequerido),
            archivoTamanoBytes,
            radicadaEn);
    }

    /// <summary>Toma la solicitud para revisión; solo es válido desde <c>Radicado</c>.</summary>
    public void TomarEnRevision(Guid revisorId, DateTimeOffset momento)
    {
        if (Estado != EstadoSolicitud.Radicado)
        {
            throw new ConflictoException(MensajesSolicitud.TransicionInvalida);
        }

        ValidarRevisor(revisorId);

        Estado = EstadoSolicitud.EnRevision;
        RevisorId = revisorId;
        RevisadaEn = momento;
        MotivoRechazo = null;
    }

    /// <summary>Aprueba la solicitud; solo es válido desde <c>EnRevision</c>.</summary>
    public void Aprobar(Guid revisorId, DateTimeOffset momento)
    {
        if (Estado != EstadoSolicitud.EnRevision)
        {
            throw new ConflictoException(MensajesSolicitud.TransicionInvalida);
        }

        ValidarRevisor(revisorId);

        Estado = EstadoSolicitud.Aprobado;
        RevisorId = revisorId;
        RevisadaEn = momento;
        MotivoRechazo = null;
    }

    /// <summary>
    /// Rechaza la solicitud exigiendo un motivo de al menos <paramref name="longitudMinimaMotivo"/>
    /// caracteres; solo es válido desde <c>EnRevision</c>.
    /// </summary>
    public void Rechazar(Guid revisorId, string? motivo, int longitudMinimaMotivo, DateTimeOffset momento)
    {
        if (Estado != EstadoSolicitud.EnRevision)
        {
            throw new ConflictoException(MensajesSolicitud.TransicionInvalida);
        }

        ValidarRevisor(revisorId);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new DomainException(MensajesSolicitud.MotivoRequerido);
        }

        var normalizado = motivo.Trim();

        if (normalizado.Length < Math.Max(1, longitudMinimaMotivo))
        {
            throw new DomainException(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                MensajesSolicitud.MotivoDemasiadoCorto,
                Math.Max(1, longitudMinimaMotivo)));
        }

        if (normalizado.Length > LongitudMaximaMotivoRechazo)
        {
            throw new DomainException(MensajesSolicitud.MotivoDemasiadoLargo);
        }

        Estado = EstadoSolicitud.Rechazado;
        RevisorId = revisorId;
        RevisadaEn = momento;
        MotivoRechazo = normalizado;
    }

    private void ValidarRevisor(Guid revisorId)
    {
        if (revisorId == Guid.Empty)
        {
            throw new DomainException(MensajesReserva.SinSesion);
        }

        if (revisorId == SolicitanteId)
        {
            throw new ConflictoException(MensajesSolicitud.TransicionInvalida);
        }
    }

    private static string Normalizar(string valor, int longitudMaxima, string mensajeRequerido, string mensajeLargo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException(mensajeRequerido);
        }

        var normalizado = valor.Trim();

        if (normalizado.Length > longitudMaxima)
        {
            throw new DomainException(mensajeLargo);
        }

        return normalizado;
    }
}
