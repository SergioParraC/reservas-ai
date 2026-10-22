using Backend.Application.Autenticacion;
using Backend.Application.Interfaces;
using Backend.Infrastructure;
using Backend.Infrastructure.Persistence;
using Backend.Presentation.Api.Problemas;
using Backend.Presentation.Api.Seguridad;

var builder = WebApplication.CreateBuilder(args);

// Servicios de la API: contrato de errores, persistencia, autenticación híbrida y casos de uso.
builder.Services.AddProblemasRfc7807();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAutenticacionHibrida(builder.Configuration);
builder.Services.AddCorsSpa(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>();

builder.Services.AddScoped<LoginLocalHandler>();
builder.Services.AddScoped<ObtenerPerfilHandler>();
builder.Services.AddScoped<IUsuarioActual, UsuarioActual>();

var app = builder.Build();

app.UseExceptionHandler();

// Pipeline HTTP: OpenAPI y migraciones automáticas solo en Development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // En otros entornos las migraciones son un paso explícito de despliegue.
    await app.Services.AplicarMigracionesAsync();
}

// Redirección HTTPS: solo cuando el entorno la exige; en contenedor el tráfico llega por HTTP al proxy.
if (app.Configuration.GetValue("Https:Redirigir", app.Environment.IsProduction()))
{
    app.UseHttpsRedirection();
}

app.UseCors(CorsExtensions.PoliticaSpa);
app.UseAuthentication();
app.UseMiddleware<ResolucionFederadaMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();

/// <summary>Punto de entrada expuesto para las pruebas de integración con <c>WebApplicationFactory</c>.</summary>
public partial class Program
{
}

