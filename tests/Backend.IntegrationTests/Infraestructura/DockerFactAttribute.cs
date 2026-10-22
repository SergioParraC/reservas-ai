namespace Backend.IntegrationTests.Infraestructura;

/// <summary>
/// Marca una prueba que requiere Docker. Si el motor no está disponible, la prueba
/// se omite en lugar de fallar, de modo que la suite unitaria permanezca verde.
/// </summary>
internal sealed class DockerFactAttribute : FactAttribute
{
    /// <summary>Evalúa la disponibilidad de Docker al descubrir la prueba.</summary>
    public DockerFactAttribute()
    {
        if (!DockerDisponible.Si)
        {
            Skip = "Docker no está disponible: active Docker Desktop para ejecutar las pruebas de integración.";
        }
    }
}
