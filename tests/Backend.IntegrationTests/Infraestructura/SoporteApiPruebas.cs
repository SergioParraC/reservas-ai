using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.Application.Common;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Backend.IntegrationTests.Infraestructura;

/// <summary>
/// Utilidades compartidas por las pruebas HTTP del calendario, las series y las solicitudes:
/// activación del periodo, alta de áreas y creación de sesiones con rol.
/// </summary>
internal static class SoporteApiPruebas
{
    /// <summary>Contraseña usada por las cuentas de prueba.</summary>
    public const string Contrasena = "Clave.Segura.2026";

    /// <summary>Zona horaria institucional de las pruebas.</summary>
    public static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    /// <summary>Fecha local de hoy en la zona institucional.</summary>
    public static DateOnly Hoy() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zona).DateTime);

    /// <summary>Primer martes con al menos una semana de antelación.</summary>
    public static DateOnly ProximoMartes()
    {
        var fecha = Hoy().AddDays(7);

        while (fecha.DayOfWeek != DayOfWeek.Tuesday)
        {
            fecha = fecha.AddDays(1);
        }

        return fecha;
    }

    /// <summary>Autentica el administrador sembrado al arrancar la API.</summary>
    public static async Task AutenticarAdministradorAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            correoInstitucional = FabricaApiPruebas.CorreoAdmin,
            contrasena = FabricaApiPruebas.ContrasenaAdmin
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var token = documento.RootElement.GetProperty("token").GetString();

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Registra un periodo y lo deja activo.</summary>
    public static async Task<Guid> ActivarPeriodoAsync(HttpClient cliente, DateOnly inicio, DateOnly fin)
    {
        var alta = await cliente.PostAsJsonAsync("/api/periodos-academicos", new
        {
            nombre = $"Periodo {inicio:yyyy-MM-dd}",
            fechaInicio = inicio,
            fechaFin = fin
        });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        using var documento = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var periodoId = documento.RootElement.GetProperty("id").GetGuid();

        var activar = await cliente.PutAsync($"/api/periodos-academicos/{periodoId}/activar", content: null);
        Assert.Equal(HttpStatusCode.OK, activar.StatusCode);

        return periodoId;
    }

    /// <summary>Registra un área con modalidad de área completa y aprobación docente.</summary>
    public static async Task<Guid> CrearAreaDeAreaCompletaAsync(HttpClient cliente)
    {
        var alta = await cliente.PostAsJsonAsync("/api/espacios", new
        {
            nombre = $"Laboratorio {Guid.NewGuid():N}",
            ubicacion = "Bloque B",
            categoria = "Laboratorio",
            aforo = 30,
            tipoAprobacion = "Docente",
            tipoModalidadPrestamo = "AreaCompleta",
            tipoElementoPrestamo = "LaboratorioEspecializado"
        });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        using var documento = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Fecha local de la zona institucional desplazada los días indicados.</summary>
    public static DateOnly FechaLocal(int dias = 0) => Hoy().AddDays(dias);

    /// <summary>Instante de la zona institucional para una fecha y hora locales.</summary>
    public static DateTimeOffset EnZona(DateOnly fecha, int hora, int minuto = 0)
    {
        var local = fecha.ToDateTime(new TimeOnly(hora, minuto), DateTimeKind.Unspecified);

        return new DateTimeOffset(local, Zona.GetUtcOffset(local));
    }

    /// <summary>Registra un área de puesto individual con un puesto y devuelve sus datos y su QR.</summary>
    public static async Task<PuestoPreparado> CrearAreaConPuestoAsync(
        HttpClient administrador,
        string identificador = "PC-01",
        int aforo = 5)
    {
        var alta = await administrador.PostAsJsonAsync("/api/espacios", new
        {
            nombre = $"Sala {Guid.NewGuid():N}",
            ubicacion = "Bloque C",
            categoria = "SalaComputo",
            aforo,
            tipoAprobacion = "Automatica",
            tipoModalidadPrestamo = "PuestoIndividual",
            tipoElementoPrestamo = "EquipoComputo"
        });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        using var areaDocumento = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var espacioId = areaDocumento.RootElement.GetProperty("id").GetGuid();

        var puesto = await administrador.PostAsJsonAsync($"/api/espacios/{espacioId}/puestos", new
        {
            identificador,
            descripcionEquipo = "Equipo de pruebas"
        });

        Assert.Equal(HttpStatusCode.Created, puesto.StatusCode);

        using var puestoDocumento = JsonDocument.Parse(await puesto.Content.ReadAsStringAsync());
        var raiz = puestoDocumento.RootElement;

        return new PuestoPreparado(
            espacioId,
            raiz.GetProperty("id").GetGuid(),
            raiz.GetProperty("identificador").GetString() ?? string.Empty,
            raiz.GetProperty("codigoQR").GetString() ?? string.Empty);
    }

    /// <summary>Registra una cuenta con rol académico y devuelve un cliente autenticado.</summary>
    public static Task<HttpClient> CrearEstudianteAsync(
        WebApplicationFactory<Program> fabrica,
        HttpClient administrador,
        string correo) =>
        CrearClienteConRolAsync(fabrica, administrador, correo, RolesAplicacion.Estudiante);

    /// <summary>
    /// Registra una cuenta, le asigna el rol con la sesión administrativa y devuelve un cliente
    /// autenticado con el token renovado.
    /// </summary>
    public static async Task<HttpClient> CrearClienteConRolAsync(
        WebApplicationFactory<Program> fabrica,
        HttpClient administrador,
        string correo,
        string rol)
    {
        using var registroHttp = fabrica.CreateClient();
        var registro = await registroHttp.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = "Usuario de Pruebas",
            correoInstitucional = correo,
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

        using var registroDocumento = JsonDocument.Parse(await registro.Content.ReadAsStringAsync());
        var usuarioId = registroDocumento.RootElement.GetProperty("id").GetGuid();

        var cambio = await administrador.PutAsJsonAsync($"/api/usuarios/{usuarioId}/rol", new { rol });
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);

        var cliente = fabrica.CreateClient();
        var sesion = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            correoInstitucional = correo,
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);

        using var sesionDocumento = JsonDocument.Parse(await sesion.Content.ReadAsStringAsync());
        var token = sesionDocumento.RootElement.GetProperty("token").GetString();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return cliente;
    }

    /// <summary>Crea una reserva sobre un puesto y devuelve su identificador y sus marcas.</summary>
    public static async Task<ReservaPreparada> ReservarAsync(
        HttpClient cliente,
        Guid puestoId,
        DateTimeOffset inicio,
        int duracionMinutos = 60)
    {
        var alta = await cliente.PostAsJsonAsync("/api/reservas", new
        {
            puestoId,
            inicio,
            fin = inicio.AddMinutes(duracionMinutos)
        });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        using var documento = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var raiz = documento.RootElement;

        return new ReservaPreparada(
            raiz.GetProperty("id").GetGuid(),
            raiz.GetProperty("inicio").GetDateTimeOffset(),
            raiz.GetProperty("finPlanificado").GetDateTimeOffset());
    }
}

/// <summary>Datos del puesto recién creado, incluido el código QR impreso en su placa.</summary>
internal sealed record PuestoPreparado(Guid EspacioId, Guid PuestoId, string Identificador, string CodigoQr);

/// <summary>Datos mínimos de una reserva recién creada.</summary>
internal sealed record ReservaPreparada(Guid Id, DateTimeOffset Inicio, DateTimeOffset FinPlanificado);
