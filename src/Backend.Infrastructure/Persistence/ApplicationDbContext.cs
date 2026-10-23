using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence;

/// <summary>
/// Contexto único de persistencia de la aplicación. Agrupa el esquema de ASP.NET Core Identity,
/// el catálogo físico y actúa como unidad de trabajo de la capa de aplicación.
/// </summary>
public sealed class ApplicationDbContext : IdentityDbContext<Usuario, Rol, Guid>, IApplicationDbContext
{
    /// <summary>Inicializa el contexto con las opciones resueltas por el contenedor de servicios.</summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>Áreas físicas del campus.</summary>
    public DbSet<Espacio> Espacios => Set<Espacio>();

    /// <summary>Puestos de cómputo de las áreas.</summary>
    public DbSet<Puesto> Puestos => Set<Puesto>();

    /// <summary>Reservas puntuales de puestos con su token de concurrencia optimista.</summary>
    public DbSet<Reserva> Reservas => Set<Reserva>();

    /// <summary>Periodos académicos del calendario institucional.</summary>
    public DbSet<PeriodoAcademico> PeriodosAcademicos => Set<PeriodoAcademico>();

    /// <summary>Franjas no operativas globales del calendario institucional.</summary>
    public DbSet<FranjaNoOperativa> FranjasNoOperativas => Set<FranjaNoOperativa>();

    /// <summary>Series semestrales de áreas completas.</summary>
    public DbSet<ReservaRecurrente> ReservasRecurrentes => Set<ReservaRecurrente>();

    /// <summary>Agenda única de las áreas: series y solicitudes aprobadas.</summary>
    public DbSet<ReservaEspacio> ReservasEspacio => Set<ReservaEspacio>();

    /// <summary>Solicitudes de préstamo con su pre-informe y su estado auditable.</summary>
    public DbSet<SolicitudPrestamo> SolicitudesPrestamo => Set<SolicitudPrestamo>();

    /// <summary>Cola persistida de notificaciones por correo.</summary>
    public DbSet<CorreoPendiente> CorreosPendientes => Set<CorreoPendiente>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
