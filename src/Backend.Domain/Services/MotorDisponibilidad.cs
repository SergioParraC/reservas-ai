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
    FueraDeHorizonte = 3,

    /// <summary>La franja se cruza con una franja no operativa del calendario institucional.</summary>
    FueraDeOperacion = 4,

    /// <summary>La franja queda fuera del periodo académico activo.</summary>
    FueraDePeriodoAcademico = 5
}

/// <summary>Franja candidata con su disponibilidad y el motivo cuando no lo está.</summary>
public sealed record FranjaCandidata(FranjaHoraria Franja, bool Disponible, MotivoNoDisponible? Motivo);

/// <summary>
/// Cruce entre una ocurrencia candidata y un bloque ocupado de la agenda del área.
/// </summary>
public sealed record ColisionFranja(FranjaHoraria Candidata, FranjaHoraria Ocupada);


/// <summary>
/// Motor de disponibilidad puro: calcula franjas y detecta solapamientos sin estado,
/// sin reloj implícito y sin dependencias tecnológicas.
/// </summary>
public static class MotorDisponibilidad
{
    /// <summary>Máximo de colisiones que se informan en una serie para acotar el problema HTTP.</summary>
    public const int MaximoColisionesReportadas = 20;

    /// <summary>Indica si la franja candidata se cruza con alguna de las ocupadas.</summary>
    public static bool HaySolapamiento(FranjaHoraria candidata, IEnumerable<FranjaHoraria> ocupadas)
    {
        ArgumentNullException.ThrowIfNull(candidata);
        ArgumentNullException.ThrowIfNull(ocupadas);

        return ocupadas.Any(ocupada => candidata.SeSolapaCon(ocupada));
    }

    /// <summary>
    /// Detecta colisiones entre las ocurrencias candidatas y la agenda ocupada mediante un
    /// barrido ordenado con fusión de bloques: <c>O((n + m) log(n + m))</c> en lugar del
    /// doble bucle cuadrático. Devuelve como máximo <see cref="MaximoColisionesReportadas"/>.
    /// </summary>
    public static IReadOnlyList<ColisionFranja> DetectarColisiones(
        IReadOnlyList<FranjaHoraria> candidatas,
        IReadOnlyList<FranjaHoraria> ocupadas,
        int maximo = MaximoColisionesReportadas)
    {
        ArgumentNullException.ThrowIfNull(candidatas);
        ArgumentNullException.ThrowIfNull(ocupadas);

        if (candidatas.Count == 0 || ocupadas.Count == 0 || maximo < 1)
        {
            return [];
        }

        var bloques = Fusionar(ocupadas);
        var orden = candidatas.OrderBy(candidata => candidata.Inicio).ToArray();
        var colisiones = new List<ColisionFranja>();

        var i = 0;
        var j = 0;

        while (i < orden.Length && j < bloques.Count && colisiones.Count < maximo)
        {
            if (orden[i].Fin <= bloques[j].Inicio)
            {
                i++;
            }
            else if (bloques[j].Fin <= orden[i].Inicio)
            {
                j++;
            }
            else
            {
                colisiones.Add(new ColisionFranja(orden[i], bloques[j]));
                i++;
            }
        }

        return colisiones;
    }

    /// <summary>
    /// Fusiona los intervalos ocupados solapados o contiguos para recorrer la agenda una sola vez.
    /// </summary>
    public static IReadOnlyList<FranjaHoraria> Fusionar(IReadOnlyList<FranjaHoraria> ocupadas)
    {
        ArgumentNullException.ThrowIfNull(ocupadas);

        if (ocupadas.Count == 0)
        {
            return [];
        }

        var ordenadas = ocupadas.OrderBy(ocupada => ocupada.Inicio).ToArray();
        var fusionadas = new List<FranjaHoraria>();

        var inicio = ordenadas[0].Inicio;
        var fin = ordenadas[0].Fin;

        for (var indice = 1; indice < ordenadas.Length; indice++)
        {
            if (ordenadas[indice].Inicio <= fin)
            {
                if (ordenadas[indice].Fin > fin)
                {
                    fin = ordenadas[indice].Fin;
                }

                continue;
            }

            fusionadas.Add(FranjaHoraria.Desde(inicio, fin));
            inicio = ordenadas[indice].Inicio;
            fin = ordenadas[indice].Fin;
        }

        fusionadas.Add(FranjaHoraria.Desde(inicio, fin));

        return fusionadas;
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
    /// <param name="calendario">Calendario institucional; se evalúa antes que la agenda.</param>
    public static IReadOnlyList<FranjaCandidata> GenerarFranjas(
        DateOnly fecha,
        PoliticaReserva politica,
        TimeZoneInfo zona,
        IReadOnlyCollection<FranjaHoraria> ocupadas,
        DateTimeOffset ahora,
        int? duracionMinutos = null,
        TimeOnly? desde = null,
        TimeOnly? hasta = null,
        CalendarioInstitucional? calendario = null)
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
            var motivo = ResolverMotivo(franja, ocupadas, politica, ahora, calendario, zona);

            franjas.Add(new FranjaCandidata(franja, motivo is null, motivo));

            cursor += duracion;
        }

        return franjas;
    }

    private static MotivoNoDisponible? ResolverMotivo(
        FranjaHoraria franja,
        IReadOnlyCollection<FranjaHoraria> ocupadas,
        PoliticaReserva politica,
        DateTimeOffset ahora,
        CalendarioInstitucional? calendario,
        TimeZoneInfo zona)
    {
        if (calendario is not null)
        {
            var evaluacion = calendario.Evaluar(franja, zona);

            switch (evaluacion.Motivo)
            {
                case MotivoCalendario.SinPeriodoActivo:
                case MotivoCalendario.FueraDePeriodo:
                    return MotivoNoDisponible.FueraDePeriodoAcademico;
                case MotivoCalendario.NoOperativa:
                    return MotivoNoDisponible.FueraDeOperacion;
            }
        }

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
