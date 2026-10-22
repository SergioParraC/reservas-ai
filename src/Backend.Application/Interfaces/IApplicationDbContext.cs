namespace Backend.Application.Interfaces;

/// <summary>
/// Unidad de trabajo de la capa de persistencia.
/// No expone <c>DbSet</c> de entidades de identidad ni tipos de ASP.NET Core Identity:
/// los usuarios y roles se gestionan exclusivamente a través de <see cref="IIdentityService"/>.
/// Los conjuntos de datos de la Etapa 2 se incorporarán aquí cuando sus entidades vivan en el dominio.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>Confirma los cambios pendientes de la unidad de trabajo.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
