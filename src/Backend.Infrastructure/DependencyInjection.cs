using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Infrastructure.Common;
using Backend.Infrastructure.Identity;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Identity;
using Backend.Infrastructure.Persistence.Queries;
using Backend.Infrastructure.Persistence.Repositories;
using Backend.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Infrastructure;

/// <summary>
/// Registro de los servicios de infraestructura: contexto de EF Core,
/// almacenes de Identity, emisión de tokens y sembrador de datos base.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Nombre de la clave de configuración de la cadena de conexión.</summary>
    public const string NombreCadenaConexion = "DefaultConnection";

    /// <summary>Intentos fallidos consecutivos antes de bloquear la cuenta (valor por defecto).</summary>
    public const int MaxIntentosFallidosPorDefecto = 5;

    /// <summary>Minutos de bloqueo temporal por defecto.</summary>
    public const int MinutosBloqueoPorDefecto = 15;

    /// <summary>Longitud mínima exigida a las contraseñas locales.</summary>
    public const int LongitudMinimaContrasena = 10;

    /// <summary>
    /// Registra la persistencia de identidad sobre SQL Server y la emisión de tokens locales.
    /// Falla explícitamente al arrancar si la cadena de conexión no está configurada.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var cadenaConexion = configuration.GetConnectionString(NombreCadenaConexion);

        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            throw new InvalidOperationException(
                $"No se encontró la cadena de conexión '{NombreCadenaConexion}'. " +
                "Configúrela en ConnectionStrings:DefaultConnection (appsettings.Development.json, variables de entorno o User Secrets).");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(cadenaConexion, sql =>
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        services.AddScoped<IApplicationDbContext>(proveedor => proveedor.GetRequiredService<ApplicationDbContext>());

        services.AddIdentityCore<Usuario>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = LongitudMinimaContrasena;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = configuration.GetValue(
                    "Identity:Lockout:MaxIntentosFallidos",
                    MaxIntentosFallidosPorDefecto);
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(configuration.GetValue(
                    "Identity:Lockout:MinutosBloqueo",
                    MinutosBloqueoPorDefecto));
            })
            .AddRoles<Rol>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Seccion));
        services.Configure<EntraIdOptions>(configuration.GetSection(EntraIdOptions.Seccion));

        services.AddSingleton(configuration.GetSection(OpcionesEspacios.Seccion).Get<OpcionesEspacios>() ?? new OpcionesEspacios());
        services.AddSingleton(configuration.GetSection(OpcionesAlmacen.Seccion).Get<OpcionesAlmacen>() ?? new OpcionesAlmacen());
        services.AddSingleton(configuration.GetSection(OpcionesIdentidad.Seccion).Get<OpcionesIdentidad>() ?? new OpcionesIdentidad());

        var opcionesReservas = configuration.GetSection(OpcionesReservas.Seccion).Get<OpcionesReservas>() ?? new OpcionesReservas();
        opcionesReservas.Validar();
        services.AddSingleton(opcionesReservas);

        var opcionesSolicitudes = configuration.GetSection(OpcionesSolicitudes.Seccion).Get<OpcionesSolicitudes>() ?? new OpcionesSolicitudes();
        opcionesSolicitudes.Validar();
        services.AddSingleton(opcionesSolicitudes);

        var opcionesSemestre = configuration.GetSection(OpcionesSemestre.Seccion).Get<OpcionesSemestre>() ?? new OpcionesSemestre();
        opcionesSemestre.Validar();
        services.AddSingleton(opcionesSemestre);

        var opcionesCalendario = configuration.GetSection(OpcionesCalendario.Seccion).Get<OpcionesCalendario>() ?? new OpcionesCalendario();
        opcionesCalendario.Validar();
        services.AddSingleton(opcionesCalendario);

        var opcionesSmtp = configuration.GetSection(OpcionesSmtp.Seccion).Get<OpcionesSmtp>() ?? new OpcionesSmtp();
        opcionesSmtp.Validar();
        services.AddSingleton(opcionesSmtp);

        services.AddMemoryCache();

        services.AddSingleton<IRelojSistema, RelojSistema>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IQrGenerator, QRCoderService>();
        services.AddSingleton<IAlmacenArchivos, LocalFileStorageService>();
        services.AddSingleton<IAlmacenDocumentos, FileStorageService>();
        services.AddSingleton<IEmailService, MailKitEmailService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IEntraIdProfileResolver, EntraIdProfileResolver>();
        services.AddScoped<IInicializadorIdentity, InicializadorIdentity>();
        services.AddScoped<IEspaciosRepository, EspaciosRepository>();
        services.AddScoped<IConsultaEspacios, ConsultaEspacios>();
        services.AddScoped<IReservasRepository, ReservasRepository>();
        services.AddScoped<IConsultaReservas, ConsultaReservas>();
        services.AddScoped<IConsultaUsuarios, ConsultaUsuarios>();
        services.AddScoped<IValidadorTokenEntra, ValidadorTokenEntra>();
        services.AddScoped<IPeriodosAcademicosRepository, PeriodosAcademicosRepository>();
        services.AddScoped<IFranjasNoOperativasRepository, FranjasNoOperativasRepository>();
        services.AddScoped<IConsultaCalendario, ConsultaCalendario>();
        services.AddScoped<IReservasRecurrentesRepository, ReservasRecurrentesRepository>();
        services.AddScoped<IConsultaOcupacionEspacio, ConsultaOcupacionEspacio>();
        services.AddScoped<ISolicitudesRepository, SolicitudesRepository>();
        services.AddScoped<IConsultaSolicitudes, ConsultaSolicitudes>();
        services.AddScoped<IColaCorreos, ColaCorreos>();
        services.AddHostedService<DespachadorCorreos>();

        return services;
    }
}
