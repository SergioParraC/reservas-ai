namespace Backend.Presentation.Api.Problemas;

/// <summary>
/// Resuelve el título legible de un <c>type</c> de conflicto consultando los catálogos de
/// reservas, calendario y solicitudes.
/// </summary>
public static class ProblemasTitulos
{
    /// <summary>Título legible del slug o el genérico cuando no pertenece a ningún módulo.</summary>
    public static string Titulo(string slug) =>
        ProblemasReserva.Titulo(slug)
        ?? ProblemasCalendario.Titulo(slug)
        ?? ProblemasSolicitud.Titulo(slug)
        ?? "Conflicto con el estado actual";
}
