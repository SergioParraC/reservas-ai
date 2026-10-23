using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas;
using Backend.Application.Modules.Reservas;
using Backend.Application.Modules.Usuarios;
using Backend.Infrastructure;
using Backend.Infrastructure.Persistence;
using Backend.Presentation.Api.Problemas;
using Backend.Presentation.Api.Seguridad;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Servicios de la API: contrato de errores, persistencia, autenticación híbrida y casos de uso.
builder.Services.AddProblemasRfc7807();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAutenticacionHibrida(builder.Configuration);
builder.Services.AddCorsSpa(builder.Configuration);
builder.Services.AddLimitacionTasa();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>();

builder.Services.AddScoped<LoginLocalHandler>();
builder.Services.AddScoped<ObtenerPerfilHandler>();
builder.Services.AddScoped<IUsuarioActual, UsuarioActual>();

builder.Services.AddScoped<ListarEspaciosHandler>();
builder.Services.AddScoped<ObtenerEspacioHandler>();
builder.Services.AddScoped<CrearEspacioHandler>();
builder.Services.AddScoped<ActualizarEspacioHandler>();
builder.Services.AddScoped<DesactivarEspacioHandler>();
builder.Services.AddScoped<CrearPuestoHandler>();
builder.Services.AddScoped<ActualizarPuestoHandler>();
builder.Services.AddScoped<DesactivarPuestoHandler>();
builder.Services.AddScoped<SubirFotografiaHandler>();
builder.Services.AddScoped<EliminarFotografiaHandler>();
builder.Services.AddScoped<GenerarQrEspacioHandler>();
builder.Services.AddScoped<GenerarQrPuestoHandler>();
builder.Services.AddScoped<GenerarPlantillaQrHandler>();
builder.Services.AddScoped<ConsultarCatalogoHandler>();
builder.Services.AddScoped<ObtenerEspacioPublicoHandler>();

builder.Services.AddScoped<ConsultarDisponibilidadHandler>();
builder.Services.AddScoped<CrearReservaHandler>();
builder.Services.AddScoped<ConsultarMisReservasHandler>();
builder.Services.AddScoped<CancelarReservaHandler>();

builder.Services.AddScoped<RegistrarUsuarioHandler>();
builder.Services.AddScoped<VincularEntraHandler>();
builder.Services.AddScoped<DesvincularEntraHandler>();
builder.Services.AddScoped<ConsultarUsuariosHandler>();
builder.Services.AddScoped<CambiarRolHandler>();

var app = builder.Build();

app.UseExceptionHandler();

// Fotografías del catálogo: archivo en volumen y URL pública estable bajo /media.
var rutaAlmacen = builder.Configuration.GetValue<string>($"{OpcionesAlmacen.Seccion}:RutaBase")
    ?? new OpcionesAlmacen().RutaBase;
var rutaAbsolutaAlmacen = Path.GetFullPath(rutaAlmacen);
Directory.CreateDirectory(rutaAbsolutaAlmacen);

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

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(rutaAbsolutaAlmacen),
    RequestPath = RutasPublicas.Media
});

app.UseCors(CorsExtensions.PoliticaSpa);
app.UseAuthentication();
app.UseMiddleware<ResolucionFederadaMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();

/// <summary>Punto de entrada expuesto para las pruebas de integración con <c>WebApplicationFactory</c>.</summary>
public partial class Program
{
}

