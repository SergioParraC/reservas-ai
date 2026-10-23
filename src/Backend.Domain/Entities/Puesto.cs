using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.Domain.Entities;

/// <summary>
/// Puesto individual de cómputo que pertenece a un área. No es un agregado independiente:
/// solo el agregado <see cref="Espacio"/> lo crea y lo modifica, de modo que la regla de
/// aforo no pueda saltarse desde otro punto de escritura.
/// </summary>
public sealed class Puesto
{
    private Puesto()
    {
    }

    private Puesto(
        Guid id,
        Guid espacioId,
        string identificador,
        string? descripcionEquipo,
        CodigoQR codigoQr,
        DateTimeOffset creadoEn)
    {
        Id = id;
        EspacioId = espacioId;
        Identificador = identificador;
        DescripcionEquipo = descripcionEquipo;
        CodigoQR = codigoQr;
        Estado = EstadoUnidad.Activo;
        CreadoEn = creadoEn;
    }

    /// <summary>Identificador interno del puesto.</summary>
    public Guid Id { get; private set; }

    /// <summary>Área a la que pertenece el puesto.</summary>
    public Guid EspacioId { get; private set; }

    /// <summary>Identificador visible del puesto dentro del área (por ejemplo, <c>PC-01</c>).</summary>
    public string Identificador { get; private set; } = string.Empty;

    /// <summary>Descripción del equipo instalado.</summary>
    public string? DescripcionEquipo { get; private set; }

    /// <summary>Código QR inmutable asignado en el alta del puesto.</summary>
    public CodigoQR CodigoQR { get; private set; } = null!;

    /// <summary>Estado actual del puesto.</summary>
    public EstadoUnidad Estado { get; private set; }

    /// <summary>Momento en que se registró el puesto.</summary>
    public DateTimeOffset CreadoEn { get; private set; }

    internal static Puesto Crear(
        Guid espacioId,
        string identificador,
        string? descripcionEquipo,
        CodigoQR codigoQr,
        DateTimeOffset creadoEn) =>
        new(Guid.NewGuid(), espacioId, identificador, descripcionEquipo, codigoQr, creadoEn);

    internal void Actualizar(string identificador, string? descripcionEquipo, EstadoUnidad estado)
    {
        Identificador = identificador;
        DescripcionEquipo = descripcionEquipo;
        Estado = estado;
    }

    internal void Desactivar() => Estado = EstadoUnidad.Inactivo;
}
