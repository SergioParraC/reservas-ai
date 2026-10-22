using Backend.Application.Interfaces;

namespace Backend.Infrastructure.Common;

/// <summary>Implementación del reloj del sistema basada en la hora UTC.</summary>
public sealed class RelojSistema : IRelojSistema
{
    /// <inheritdoc />
    public DateTimeOffset Ahora => DateTimeOffset.UtcNow;
}
