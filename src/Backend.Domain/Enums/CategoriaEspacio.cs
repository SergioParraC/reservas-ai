namespace Backend.Domain.Enums;

/// <summary>
/// Clasificación funcional de un área física del campus.
/// </summary>
public enum CategoriaEspacio
{
    /// <summary>Salón de clases.</summary>
    Aula = 1,

    /// <summary>Laboratorio de prácticas o investigación.</summary>
    Laboratorio = 2,

    /// <summary>Auditorio o sala de conferencias.</summary>
    Auditorio = 3,

    /// <summary>Sala de cómputo con puestos individuales.</summary>
    SalaComputo = 4,

    /// <summary>Biblioteca o sala de estudio.</summary>
    Biblioteca = 5,

    /// <summary>Escenario deportivo o recreativo.</summary>
    Deportivo = 6,

    /// <summary>Oficina administrativa.</summary>
    Oficina = 7,

    /// <summary>Área que no encaja en ninguna categoría anterior.</summary>
    Otro = 8
}
