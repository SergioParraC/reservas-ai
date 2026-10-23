using Backend.Domain.ValueObjects;

namespace Backend.Domain.Services;

/// <summary>Motivo por el que una franja no puede reservarse.</summary>
public enum MotivoNoDisponible
{
    /// <summary>Otra reserva vigente ocupa total o parcialmente la franja.</summary>
    Ocupada = 1,

    /// <summary>La franja empieza antes de la antelación mínima permitida.</summary>
    FueraDeAntelacion = 2,

    /// <summary>La franja supera el horizonte máximo de programación.</summary>
    FueraDeHorizonte = 3
}

/// <summary>Franja candidata con su disponibilidad y el motivo cuando no lo está.</summary>
public sealed record FranjaCandidata(FranjaHoraria Franja, bool Disponible, MotivoNoDisponible? Motivo);

/// <summary>
/// Motor de disponibilidad puro: calcula franjas y detecta solapamientos sin estado,
/// sin reloj implícito y sin dependencias tecnológicas.
/// </summary>
public static class MotorDisponibilidad
{
    /// <summary>Indica si la franja candidata se cruza con alguna de las ocupadas.</summary>
    public static bool HaySolapamiento(FranjaHoraria candidata, IEnumerable<FranjaHoraria> ocupadas)
    {
        ArgumentNullException.ThrowIfNull(candidata);
        ArgumentNullException.ThrowIfNull(ocupadas);

        return ocupadas.Any(ocupada => candidata.SeSolapaCon(ocupada));
    }

    /// <summary>
    /// Genera las franjas de una jornada con su disponibilidad frente a las reservas vigentes.
    /// </summary>
    /// <param name="fecha">Fecha local de la jornada consultada.</param>
    /// <param name="politica">Política vigente de reservas.</param>
    /// <param name="zona">Zona horaria institucional.</param>
    /// <param name="ocupadas">Franjas ocupadas por reservas vigentes del puesto.</param>
    /// <param name="ahora">Instante actual del servidor.</param>
    /// <param name="duracionMinutos">Duración de cada franja; por omisión la mínima permitida.</param>
    /// <param name="desde">Hora de inicio de la consulta; por omisión la apertura institucional.</param>
    /// <param name="hasta">Hora final de la consulta; por omisión el cierre institucional.</param>
    public static IReadOnlyList<FranjaCandidata> GenerarFranjas(
        DateOnly fecha,
        PoliticaReserva politica,
        TimeZoneInfo zona,
        IReadOnlyCollection<FranjaHoraria> ocupadas,
        DateTimeOffset ahora,
        int? duracionMinutos = null,
        TimeOnly? desde = null,
        TimeOnly? hasta = null)
    {
        ArgumentNullException.ThrowIfNull(politica);
        ArgumentNullException.ThrowIfNull(zona);
        ArgumentNullException.ThrowIfNull(ocupadas);

        var duracion = ResolverDuracion(politica, duracionMinutos);
        var apertura = Max(desde ?? politica.HoraApertura, politica.HoraApertura);
        var cierre = Min(hasta ?? politica.HoraCierre, politica.HoraCierre);

        if (cierre <= apertura)
        {
            return [];
        }

        var franjas = new List<FranjaCandidata>();
        var cursor = InicioEnZona(fecha, apertura, zona);
        var finJornada = InicioEnZona(fecha, cierre, zona);

        while (cursor + duracion <= finJornada)
        {
            var franja = FranjaHoraria.Desde(cursor, cursor + duracion);
            var motivo = ResolverMotivo(franja, ocupadas, politica, ahora);

            franjas.Add(new FranjaCandidata(franja, motivo is null, motivo));

            cursor += duracion;
        }

        return franjas;
    }

    private static MotivoNoDisponible? ResolverMotivo(
        FranjaHoraria franja,
        IReadOnlyCollection<FranjaHoraria> ocupadas,
        PoliticaReserva politica,
        DateTimeOffset ahora)
    {
        if (HaySolapamiento(franja, ocupadas))
        {
            return MotivoNoDisponible.Ocupada;
        }

        if (franja.Inicio < ahora + politica.AntelacionMinima)
        {
            return MotivoNoDisponible.FueraDeAntelacion;
        }

        return franja.Inicio > ahora + politica.AntelacionMaxima
            ? MotivoNoDisponible.FueraDeHorizonte
            : null;
    }

    private static TimeSpan ResolverDuracion(PoliticaReserva politica, int? duracionMinutos)
    {
        var duracion = duracionMinutos is { } minutos && minutos > 0
            ? TimeSpan.FromMinutes(minutos)
            : politica.DuracionMinima;

        return duracion < politica.DuracionMinima
            ? politica.DuracionMinima
            : duracion > politica.DuracionMaxima
                ? politica.DuracionMaxima
                : duracion;
    }

    /// <summary>Construye el desplazamiento local de una fecha y hora en la zona indicada.</summary>
    public static DateTimeOffset InicioEnZona(DateOnly fecha, TimeOnly hora, TimeZoneInfo zona)
    {
        ArgumentNullException.ThrowIfNull(zona);

        var local = fecha.ToDateTime(hora, DateTimeKind.Unspecified);

        return new DateTimeOffset(local, zona.GetUtcOffset(local));
    }

    private static TimeOnly Max(TimeOnly izquierda, TimeOnly derecha) => izquierda > derecha ? izquierda : derecha;

    private static TimeOnly Min(TimeOnly izquierda, TimeOnly derecha) => izquierda < derecha ? izquierda : derecha;
}
