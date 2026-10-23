using System.Globalization;
using Backend.Domain.Exceptions;

namespace Backend.Domain.Entities;

/// <summary>
/// Periodo académico vigente del calendario institucional. Define la ventana en la que se
/// admiten reservas, series y solicitudes; como máximo uno puede estar activo a la vez.
/// </summary>
public sealed class PeriodoAcademico
{
    /// <summary>Longitud máxima del nombre del periodo.</summary>
    public const int LongitudMaximaNombre = 40;

    private PeriodoAcademico()
    {
    }

    private PeriodoAcademico(Guid id, string nombre, DateOnly fechaInicio, DateOnly fechaFin, DateTimeOffset momento)
    {
        Id = id;
        Nombre = nombre;
        FechaInicio = fechaInicio;
        FechaFin = fechaFin;
        EstadoActivo = false;
        CreadoEn = momento;
        ActualizadoEn = momento;
    }

    /// <summary>Identificador interno del periodo.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre visible del periodo (<c>2026-2</c>, por ejemplo).</summary>
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Primer día del periodo, inclusive.</summary>
    public DateOnly FechaInicio { get; private set; }

    /// <summary>Último día del periodo, inclusive.</summary>
    public DateOnly FechaFin { get; private set; }

    /// <summary>Indica si el periodo es el vigente; nunca hay dos activos.</summary>
    public bool EstadoActivo { get; private set; }

    /// <summary>Momento del alta.</summary>
    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Momento de la última modificación.</summary>
    public DateTimeOffset ActualizadoEn { get; private set; }

    /// <summary>Token de concurrencia optimista gestionado por la base de datos.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Cantidad de días que cubre el periodo.</summary>
    public int Dias => FechaFin.DayNumber - FechaInicio.DayNumber + 1;

    /// <summary>
    /// Crea un periodo inactivo. La activación es una operación explícita y transaccional.
    /// </summary>
    public static PeriodoAcademico Crear(
        string nombre,
        DateOnly fechaInicio,
        DateOnly fechaFin,
        DateTimeOffset momento)
    {
        var nombreNormalizado = NormalizarNombre(nombre);
        ValidarRango(fechaInicio, fechaFin);

        return new PeriodoAcademico(Guid.NewGuid(), nombreNormalizado, fechaInicio, fechaFin, momento);
    }

    /// <summary>Actualiza el nombre y el rango del periodo conservando su estado.</summary>
    public void Actualizar(string nombre, DateOnly fechaInicio, DateOnly fechaFin, DateTimeOffset momento)
    {
        Nombre = NormalizarNombre(nombre);
        ValidarRango(fechaInicio, fechaFin);

        FechaInicio = fechaInicio;
        FechaFin = fechaFin;
        ActualizadoEn = momento;
    }

    /// <summary>Marca el periodo como vigente. La exclusividad la garantiza el repositorio.</summary>
    public void Activar(DateTimeOffset momento)
    {
        EstadoActivo = true;
        ActualizadoEn = momento;
    }

    /// <summary>Retira la vigencia del periodo.</summary>
    public void Desactivar(DateTimeOffset momento)
    {
        EstadoActivo = false;
        ActualizadoEn = momento;
    }

    /// <summary>Indica si la fecha cae dentro de la ventana del periodo.</summary>
    public bool Contiene(DateOnly fecha) => fecha >= FechaInicio && fecha <= FechaFin;

    /// <summary>Indica si una ventana completa cae dentro del periodo.</summary>
    public bool ContieneVentana(DateOnly inicio, DateOnly fin) => inicio >= FechaInicio && fin <= FechaFin;

    /// <summary>Indica si el rango indicado se cruza con este periodo.</summary>
    public bool SeSolapaCon(DateOnly inicio, DateOnly fin) => inicio <= FechaFin && FechaInicio <= fin;

    /// <summary>Indica si el periodo es anterior al instante indicado.</summary>
    public bool TerminoAntesDe(DateOnly fecha) => FechaFin < fecha;

    private static string NormalizarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException(MensajesCalendario.PeriodoNombreRequerido);
        }

        var normalizado = nombre.Trim();

        if (normalizado.Length > LongitudMaximaNombre)
        {
            throw new DomainException(MensajesCalendario.PeriodoNombreDemasiadoLargo);
        }

        return normalizado;
    }

    private static void ValidarRango(DateOnly fechaInicio, DateOnly fechaFin)
    {
        if (fechaFin < fechaInicio)
        {
            throw new DomainException(string.Format(
                CultureInfo.InvariantCulture,
                MensajesCalendario.PeriodoFechasInvertidas));
        }
    }
}
