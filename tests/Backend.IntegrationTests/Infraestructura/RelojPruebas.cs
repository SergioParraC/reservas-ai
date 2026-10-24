using Backend.Application.Interfaces;

namespace Backend.IntegrationTests.Infraestructura;

/// <summary>
/// Reloj mutable de las pruebas de integración. Permite situar la API en un instante concreto para
/// validar la ventana de check-in, la salida anticipada y el corte del barrido sin esperas reales.
/// </summary>
internal sealed class RelojPruebas : IRelojSistema
{
    /// <summary>Instante actual que observa la API; arranca en la hora real.</summary>
    public DateTimeOffset Ahora { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Sitúa el reloj en el instante indicado.</summary>
    public void SituarEn(DateTimeOffset momento) => Ahora = momento;
}
