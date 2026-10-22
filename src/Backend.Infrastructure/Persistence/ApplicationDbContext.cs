using Backend.Application.Interfaces;
using Backend.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence;

/// <summary>
/// Contexto único de persistencia de la aplicación. Agrupa el esquema de ASP.NET Core Identity
/// y actúa como unidad de trabajo de la capa de aplicación.
/// </summary>
public sealed class ApplicationDbContext : IdentityDbContext<Usuario, Rol, Guid>, IApplicationDbContext
{
    /// <summary>Inicializa el contexto con las opciones resueltas por el contenedor de servicios.</summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
