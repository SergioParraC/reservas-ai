namespace Backend.Domain.ValueObjects;

/// <summary>
/// Política institucional de reservas expresada en el dominio, sin configuración ni zona horaria:
/// la capa de aplicación la construye desde <c>OpcionesReservas</c>.
/// </summary>
public sealed record PoliticaReserva(
    TimeSpan DuracionMinima,
    TimeSpan DuracionMaxima,
    int IntervaloMinutos,
    TimeSpan AntelacionMinima,
    TimeSpan AntelacionMaxima,
    TimeSpan AntelacionCancelacion,
    TimeOnly HoraApertura,
    TimeOnly HoraCierre)
{
    /// <summary>Indica si la hora indicada pertenece a la jornada institucional.</summary>
    public bool DentroDeJornada(TimeOnly hora) => hora >= HoraApertura && hora <= HoraCierre;
}
