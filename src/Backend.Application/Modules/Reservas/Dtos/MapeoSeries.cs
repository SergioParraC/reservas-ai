using Backend.Domain.Entities;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Reservas.Dtos;

/// <summary>Mapeos y clasificación de mensajes del módulo de series semestrales.</summary>
public static class MapeoSeries
{
    /// <summary>Proyecta la serie recién creada con sus ocurrencias y las omitidas por el calendario.</summary>
    public static SerieDetalle ADetalle(
        ReservaRecurrente serie,
        string espacioNombre,
        OcurrenciasGeneradas generadas)
    {
        ArgumentNullException.ThrowIfNull(serie);
        ArgumentNullException.ThrowIfNull(generadas);

        var ocurrencias = generadas.Ocurrencias
            .Select(franja => new OcurrenciaSerie(franja.Inicio, franja.Fin, false, null))
            .Concat(generadas.Omitidas.Select(franja => new OcurrenciaSerie(
                franja.Inicio,
                franja.Fin,
                true,
                "fuera-de-operacion")))
            .OrderBy(ocurrencia => ocurrencia.Inicio)
            .ToArray();

        return new SerieDetalle(
            serie.Id,
            serie.EspacioId,
            espacioNombre,
            serie.Materia,
            serie.TipoRecurrencia,
            serie.DiaSemana,
            serie.Patron.FechasFijas,
            serie.HoraInicio,
            serie.HoraFin,
            serie.PeriodoInicio,
            serie.PeriodoFin,
            serie.CreadaEn,
            generadas.Ocurrencias.Count,
            ocurrencias);
    }

    /// <summary>Construye el diccionario de errores por campo del contrato RFC 7807.</summary>
    public static IDictionary<string, string[]> Errores(string campo, string mensaje) =>
        new Dictionary<string, string[]>(StringComparer.Ordinal) { [campo] = [mensaje] };

    /// <summary>Determina el campo del cuerpo al que corresponde un mensaje de dominio.</summary>
    public static string CampoDe(string mensaje) => mensaje switch
    {
        MensajesSemestre.PeriodoInvalido or MensajesSemestre.PeriodoExcedido => "periodoFin",
        MensajesSemestre.FechaFueraDePeriodo => "fechasFijas",
        MensajesSemestre.DiaSemanaRequerido or MensajesSemestre.FechasFijasRequeridas
            or MensajesSemestre.FechasFijasSinDia or MensajesSemestre.TipoRecurrenciaInvalido => "tipoRecurrencia",
        MensajesSemestre.FranjaPatronInvalida => "horaFin",
        MensajesSemestre.AntelacionMinima => "periodoInicio",
        MensajesSemestre.MateriaRequerida or MensajesSemestre.MateriaDemasiadoLarga => "materia",
        _ => "inicio"
    };
}
