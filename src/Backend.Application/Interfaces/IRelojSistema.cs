namespace Backend.Application.Interfaces;

/// <summary>
/// Abstracción del reloj del sistema para que los casos de uso sean deterministas en pruebas.
/// </summary>
public interface IRelojSistema
{
    /// <summary>Momento actual en UTC.</summary>
    DateTimeOffset Ahora { get; }
}
