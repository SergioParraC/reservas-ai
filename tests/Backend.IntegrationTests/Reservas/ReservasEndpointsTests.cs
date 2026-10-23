using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Backend.IntegrationTests.Reservas;

/// <summary>
/// Pruebas de integración del préstamo directo: disponibilidad autenticada, alta confirmada,
/// bloqueo de la franja, agenda propia, cancelación y contrato RFC 7807.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class ReservasEndpointsTests
{
    private const string Contrasena = "Clave.Segura.2026";

    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public ReservasEndpointsTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task LaReservaDirectaBloqueaLaFranjaYLiberaAlCancelar()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("reservas_flujo");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var puesto = await PrepararPuestoAsync(fabrica, cliente);
        var estudiante = await RegistrarEstudianteAsync(cliente, "ana@uninpahu.edu.co");
        await AutenticarAsync(cliente, estudiante);
        var franja = Manana(10);

        var disponibilidad = await cliente.GetAsync(
            $"/api/puestos/{puesto.Id}/disponibilidad?fecha={franja.Fecha:yyyy-MM-dd}&duracionMinutos=60");
        Assert.Equal(HttpStatusCode.OK, disponibilidad.StatusCode);

        var inicial = await disponibilidad.Content.ReadFromJsonAsync<Disponibilidad>();
        Assert.NotNull(inicial);
        Assert.Equal("America/Bogota", inicial.ZonaHoraria);
        Assert.Contains(inicial.Franjas, item => item.Inicio == franja.Inicio && item.Disponible);

        var alta = await cliente.PostAsJsonAsync("/api/reservas", new
        {
            puestoId = puesto.Id,
            inicio = franja.Inicio,
            fin = franja.Fin
        });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        Assert.Contains("/api/reservas/", alta.Headers.Location?.ToString() ?? string.Empty);

        var creada = await alta.Content.ReadFromJsonAsync<ReservaDetalle>();
        Assert.NotNull(creada);
        Assert.Equal(1, (int)creada.Estado);

        var ocupada = await cliente.GetFromJsonAsync<Disponibilidad>(
            $"/api/puestos/{puesto.Id}/disponibilidad?fecha={franja.Fecha:yyyy-MM-dd}&duracionMinutos=60");
        Assert.NotNull(ocupada);
        var bloqueada = Assert.Single(ocupada.Franjas, item => item.Inicio == franja.Inicio);
        Assert.False(bloqueada.Disponible);
        Assert.Equal("Ocupada", bloqueada.Motivo);

        var mie = await cliente.GetFromJsonAsync<PaginaReservas>("/api/reservas/me");
        Assert.NotNull(mie);
        Assert.Equal(1, mie.Total);
        Assert.True(mie.Elementos[0].Cancelable);

        var baja = await cliente.DeleteAsync($"/api/reservas/{creada.Id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var liberada = await cliente.GetFromJsonAsync<Disponibilidad>(
            $"/api/puestos/{puesto.Id}/disponibilidad?fecha={franja.Fecha:yyyy-MM-dd}&duracionMinutos=60");
        Assert.NotNull(liberada);
        Assert.True(Assert.Single(liberada.Franjas, item => item.Inicio == franja.Inicio).Disponible);

        using var anonimo = fabrica.CreateClient();
        var sinSesion = await anonimo.GetAsync($"/api/puestos/{puesto.Id}/disponibilidad?fecha={franja.Fecha:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.Unauthorized, sinSesion.StatusCode);
    }

    [DockerFact]
    public async Task ElCupoPorUsuarioYLaTitularidadSeRespetan()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("reservas_cupo");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);

        using var cliente = fabrica.CreateClient();
        var puesto = await PrepararPuestoAsync(fabrica, cliente);

        using var otro = fabrica.CreateClient();
        var titular = await RegistrarEstudianteAsync(cliente, "luis@uninpahu.edu.co");
        var tercero = await RegistrarEstudianteAsync(otro, "marta@uninpahu.edu.co");
        await AutenticarAsync(cliente, titular);
        await AutenticarAsync(otro, tercero);

        var manana = Manana(10).Fecha;
        var primera = await ReservarAsync(cliente, puesto.Id, manana, 10);
        var segunda = await ReservarAsync(cliente, puesto.Id, manana, 11);
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);

        var tercera = await ReservarAsync(cliente, puesto.Id, manana, 12);
        Assert.Equal(HttpStatusCode.Conflict, tercera.StatusCode);
        Assert.Contains(
            "limite-reservas-alcanzado",
            await tercera.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        var creada = await primera.Content.ReadFromJsonAsync<ReservaDetalle>();
        Assert.NotNull(creada);

        var ajena = await otro.DeleteAsync($"/api/reservas/{creada.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);

        var fueraDeHorizonte = await cliente.GetAsync(
            $"/api/puestos/{puesto.Id}/disponibilidad?fecha={manana.AddDays(40):yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.BadRequest, fueraDeHorizonte.StatusCode);

        var inexistente = await cliente.PostAsJsonAsync("/api/reservas", new
        {
            puestoId = Guid.NewGuid(),
            inicio = Manana(10).Inicio,
            fin = Manana(10).Fin
        });
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [DockerFact]
    public async Task DosAltasSimultaneasSobreLaMismaFranjaDejanUnaSolaReserva()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("reservas_concurrente");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);

        using var administracion = fabrica.CreateClient();
        var puesto = await PrepararPuestoAsync(fabrica, administracion);
        var franja = Manana(15);

        using var primero = await CrearClienteAutenticadoAsync(fabrica, "ana@uninpahu.edu.co");
        using var segundo = await CrearClienteAutenticadoAsync(fabrica, "luis@uninpahu.edu.co");

        var respuestas = await Task.WhenAll(
            ReservarAsync(primero, puesto.Id, franja.Fecha, 15),
            ReservarAsync(segundo, puesto.Id, franja.Fecha, 15));

        var creadas = respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Created);
        var conflictos = respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, creadas);
        Assert.Equal(1, conflictos);
    }


    [DockerFact]
    public async Task DosCancelacionesSimultaneasSoloAplicanUna()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("reservas_cancelacion");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var puesto = await PrepararPuestoAsync(fabrica, cliente);
        var credenciales = await RegistrarEstudianteAsync(cliente, "ana@uninpahu.edu.co");
        await AutenticarAsync(cliente, credenciales);

        var franja = Manana(9);
        var alta = await ReservarAsync(cliente, puesto.Id, franja.Fecha, 9);
        var creada = await alta.Content.ReadFromJsonAsync<ReservaDetalle>();
        Assert.NotNull(creada);

        var respuestas = await Task.WhenAll(
            cliente.DeleteAsync($"/api/reservas/{creada.Id}"),
            cliente.DeleteAsync($"/api/reservas/{creada.Id}"));

        Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Conflict));
    }

    [DockerFact]
    public async Task DosAltasSimultaneasDelMismoUsuarioNoSuperanElCupo()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("reservas_cupo_concurrente");
        await using var fabrica = FabricaApiPruebas.Crear(
            cadena,
            sobrescribir: new Dictionary<string, string?>
            {
                ["Reservas:MaxReservasActivasPorUsuario"] = "1"
            });

        using var cliente = fabrica.CreateClient();
        var puesto = await PrepararPuestoAsync(fabrica, cliente);
        var credenciales = await RegistrarEstudianteAsync(cliente, "ana@uninpahu.edu.co");
        await AutenticarAsync(cliente, credenciales);
        var fecha = Manana(9).Fecha;

        var respuestas = await Task.WhenAll(
            ReservarAsync(cliente, puesto.Id, fecha, 9),
            ReservarAsync(cliente, puesto.Id, fecha, 10));

        Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Conflict));
    }

    [DockerFact]
    public async Task LaReservaFueraDelPeriodoActivoDevuelve409()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("reservas_periodo");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();
        await AutenticarAdministradorAsync(cliente);

        var area = await cliente.PostAsJsonAsync("/api/espacios", new
        {
            nombre = "Sala sin periodo",
            ubicacion = "Bloque C",
            categoria = "SalaComputo",
            aforo = 5,
            tipoAprobacion = "Automatica",
            tipoModalidadPrestamo = "PuestoIndividual",
            tipoElementoPrestamo = "EquipoComputo"
        });

        Assert.Equal(HttpStatusCode.Created, area.StatusCode);

        using var areaDocumento = JsonDocument.Parse(await area.Content.ReadAsStringAsync());
        var espacioId = areaDocumento.RootElement.GetProperty("id").GetGuid();

        var puesto = await cliente.PostAsJsonAsync($"/api/espacios/{espacioId}/puestos", new { identificador = "PC-01" });
        Assert.Equal(HttpStatusCode.Created, puesto.StatusCode);

        using var puestoDocumento = JsonDocument.Parse(await puesto.Content.ReadAsStringAsync());
        var puestoId = puestoDocumento.RootElement.GetProperty("id").GetGuid();

        var estudiante = await RegistrarEstudianteAsync(cliente, "periodo@uninpahu.edu.co");
        await AutenticarAsync(cliente, estudiante);

        var sinPeriodo = await ReservarAsync(cliente, puestoId, Manana(10).Fecha, 10);
        Assert.Equal(HttpStatusCode.Conflict, sinPeriodo.StatusCode);
        Assert.Contains("sin-periodo-activo", await sinPeriodo.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await AutenticarAdministradorAsync(cliente);
        await ActivarPeriodoAsync(cliente, FechaLocal(-2), FechaLocal(-1));

        await AutenticarAsync(cliente, estudiante);

        var fueraDePeriodo = await ReservarAsync(cliente, puestoId, FechaLocal(3), 10);
        Assert.Equal(HttpStatusCode.Conflict, fueraDePeriodo.StatusCode);
        Assert.Contains(
            "fuera-de-periodo-academico",
            await fueraDePeriodo.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    private static DateOnly FechaLocal(int dias) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zona).DateTime).AddDays(dias);

    private static async Task ActivarPeriodoAsync(HttpClient cliente, DateOnly inicio, DateOnly fin)
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
    }

    private static async Task ActivarPeriodoAsync(HttpClient cliente)
    {
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zona).DateTime);

        var alta = await cliente.PostAsJsonAsync("/api/periodos-academicos", new
        {
            nombre = "Periodo de pruebas",
            fechaInicio = hoy.AddDays(-30),
            fechaFin = hoy.AddDays(60)
        });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        using var documento = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var periodoId = documento.RootElement.GetProperty("id").GetGuid();

        var activar = await cliente.PutAsync($"/api/periodos-academicos/{periodoId}/activar", content: null);
        Assert.Equal(HttpStatusCode.OK, activar.StatusCode);
    }

    private static (DateOnly Fecha, DateTimeOffset Inicio, DateTimeOffset Fin) Manana(int hora)
    {
        var hoyLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zona).DateTime);
        var fecha = hoyLocal.AddDays(1);
        var local = fecha.ToDateTime(new TimeOnly(hora, 0), DateTimeKind.Unspecified);
        var inicio = new DateTimeOffset(local, Zona.GetUtcOffset(local));

        return (fecha, inicio, inicio.AddHours(1));
    }

    private static Task<HttpResponseMessage> ReservarAsync(HttpClient cliente, Guid puestoId, DateOnly fecha, int hora)
    {
        var local = fecha.ToDateTime(new TimeOnly(hora, 0), DateTimeKind.Unspecified);
        var inicio = new DateTimeOffset(local, Zona.GetUtcOffset(local));

        return cliente.PostAsJsonAsync("/api/reservas", new
        {
            puestoId,
            inicio,
            fin = inicio.AddHours(1)
        });
    }

    private static async Task<PuestoRespuesta> PrepararPuestoAsync(
        WebApplicationFactory<Program> fabrica,
        HttpClient cliente)
    {
        Assert.NotNull(cliente);
        await AutenticarAdministradorAsync(cliente);

        var area = await cliente.PostAsJsonAsync("/api/espacios", new
        {
            nombre = "Sala de Cómputo Automática",
            ubicacion = "Bloque C, piso 1",
            categoria = "SalaComputo",
            aforo = 5,
            tipoAprobacion = "Automatica",
            tipoModalidadPrestamo = "PuestoIndividual",
            tipoElementoPrestamo = "EquipoComputo"
        });

        Assert.Equal(HttpStatusCode.Created, area.StatusCode);

        var espacio = await area.Content.ReadFromJsonAsync<EspacioRespuesta>();
        Assert.NotNull(espacio);

        var puesto = await cliente.PostAsJsonAsync($"/api/espacios/{espacio.Id}/puestos", new
        {
            identificador = "PC-01",
            descripcionEquipo = "Equipo de pruebas"
        });

        Assert.Equal(HttpStatusCode.Created, puesto.StatusCode);

        var creado = await puesto.Content.ReadFromJsonAsync<PuestoRespuesta>();
        Assert.NotNull(creado);

        await ActivarPeriodoAsync(cliente);

        return creado;
    }

    private static async Task<HttpClient> CrearClienteAutenticadoAsync(
        WebApplicationFactory<Program> fabrica,
        string correo)
    {
        var cliente = fabrica.CreateClient();
        var credenciales = await RegistrarEstudianteAsync(cliente, correo);
        await AutenticarAsync(cliente, credenciales);

        return cliente;
    }

    private static async Task<Credenciales> RegistrarEstudianteAsync(HttpClient cliente, string correo)
    {
        var registro = await cliente.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = "Estudiante de Pruebas",
            correoInstitucional = correo,
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

        return new Credenciales(correo, Contrasena);
    }

    private static async Task AutenticarAsync(HttpClient cliente, Credenciales credenciales)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            correoInstitucional = credenciales.Correo,
            contrasena = credenciales.Contrasena
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var token = await respuesta.Content.ReadFromJsonAsync<TokenRespuesta>();
        Assert.NotNull(token);

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static Task AutenticarAdministradorAsync(HttpClient cliente) =>
        AutenticarAsync(cliente, new Credenciales(
            FabricaApiPruebas.CorreoAdmin,
            FabricaApiPruebas.ContrasenaAdmin));

    private sealed record Credenciales(string Correo, string Contrasena);

    private sealed record TokenRespuesta(string Token);

    private sealed record EspacioRespuesta(Guid Id, string Nombre);

    private sealed record PuestoRespuesta(Guid Id, string Identificador);

    private sealed record Franja(DateTimeOffset Inicio, DateTimeOffset Fin, bool Disponible, string? Motivo);

    private sealed record Disponibilidad(
        Guid PuestoId,
        string Identificador,
        Guid EspacioId,
        string EspacioNombre,
        string ZonaHoraria,
        IReadOnlyList<Franja> Franjas);

    private sealed record ReservaDetalle(
        Guid Id,
        Guid PuestoId,
        string PuestoIdentificador,
        DateTimeOffset Inicio,
        DateTimeOffset Fin,
        int Estado,
        bool Cancelable);

    private sealed record ReservaResumen(Guid Id, DateTimeOffset Inicio, DateTimeOffset Fin, int Estado, bool Cancelable);

    private sealed record PaginaReservas(IReadOnlyList<ReservaResumen> Elementos, int Total);
}

