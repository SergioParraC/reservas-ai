using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Backend.IntegrationTests.Areas;

/// <summary>
/// Pruebas de integración del mantenimiento de áreas: alta, inmutabilidad del QR,
/// baja lógica, catálogo público y guardas de autorización.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class AreasEndpointsTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public AreasEndpointsTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElAreaSeCreaConQrInmutableYDelaBajaDesapareceDelCatalogo()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("areas_crud");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();
        await AutenticarAdministradorAsync(cliente);

        var creada = await CrearAreaAsync(cliente, "Laboratorio de Redes", "Bloque B", 20);

        Assert.Equal(1, (int)creada.Estado);
        Assert.Equal(29, creada.CodigoQR.Length);

        var detalle = await cliente.GetFromJsonAsync<AreaDetalle>($"/api/espacios/{creada.Id}");
        Assert.NotNull(detalle);
        Assert.Equal(creada.CodigoQR, detalle.CodigoQR);

        var actualizacion = await cliente.PutAsJsonAsync($"/api/espacios/{creada.Id}", new
        {
            nombre = "Laboratorio de Redes II",
            ubicacion = "Bloque B, piso 2",
            categoria = "Laboratorio",
            aforo = 25,
            tipoAprobacion = "Docente",
            tipoModalidadPrestamo = "AreaCompleta",
            tipoElementoPrestamo = "LaboratorioEspecializado",
            estado = "Activo",
            codigoQR = "QR-AAAAAAAAAAAAAAAAAAAAAAAAAA",
            rowVersion = detalle.RowVersion
        });

        Assert.Equal(HttpStatusCode.OK, actualizacion.StatusCode);

        var actualizada = await actualizacion.Content.ReadFromJsonAsync<AreaDetalle>();
        Assert.NotNull(actualizada);
        Assert.Equal(creada.CodigoQR, actualizada.CodigoQR);
        Assert.Equal(25, actualizada.Aforo);

        var qr = await cliente.GetAsync($"/api/espacios/{creada.Id}/qr.svg");
        Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
        Assert.Equal("image/svg+xml", qr.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<svg", await qr.Content.ReadAsStringAsync());

        using var anonimo = fabrica.CreateClient();
        var catalogo = await anonimo.GetAsync("/api/catalogo/espacios");
        Assert.Equal(HttpStatusCode.OK, catalogo.StatusCode);
        Assert.Contains("Laboratorio de Redes II", await catalogo.Content.ReadAsStringAsync());

        var publico = await anonimo.GetFromJsonAsync<AreaPublica>($"/api/catalogo/espacios/{creada.Id}");
        Assert.NotNull(publico);

        var baja = await cliente.DeleteAsync($"/api/espacios/{creada.Id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var catalogoVacio = await anonimo.GetFromJsonAsync<PaginaAreas>("/api/catalogo/espacios");
        Assert.NotNull(catalogoVacio);
        Assert.Equal(0, catalogoVacio.Total);
        Assert.Empty(catalogoVacio.Elementos);

        var detallePublico = await anonimo.GetAsync($"/api/catalogo/espacios/{creada.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detallePublico.StatusCode);

        var listadoAdmin = await cliente.GetFromJsonAsync<PaginaAreas>("/api/espacios?estado=Inactivo");
        Assert.NotNull(listadoAdmin);
        Assert.Equal(1, listadoAdmin.Total);
        Assert.Equal(creada.CodigoQR, listadoAdmin.Elementos[0].CodigoQR);
    }

    [DockerFact]
    public async Task ElAforoYLaUnicidadDeNombreSeValidanCon409Y400()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("areas_aforo");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();
        await AutenticarAdministradorAsync(cliente);

        var area = await CrearAreaAsync(cliente, "Sala de Cómputo 1", "Bloque C", 1);

        var primerPuesto = await cliente.PostAsJsonAsync($"/api/espacios/{area.Id}/puestos", new { identificador = "PC-01" });
        Assert.Equal(HttpStatusCode.Created, primerPuesto.StatusCode);

        var excedido = await cliente.PostAsJsonAsync($"/api/espacios/{area.Id}/puestos", new { identificador = "PC-02" });
        Assert.Equal(HttpStatusCode.Conflict, excedido.StatusCode);

        var repetido = await cliente.PostAsJsonAsync("/api/espacios", new
        {
            nombre = "Sala de Cómputo 1",
            ubicacion = "Bloque C",
            categoria = "SalaComputo",
            aforo = 10,
            tipoAprobacion = "Docente",
            tipoModalidadPrestamo = "PuestoIndividual",
            tipoElementoPrestamo = "EquipoComputo"
        });
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);

        var detalle = await cliente.GetFromJsonAsync<AreaDetalle>($"/api/espacios/{area.Id}");
        Assert.NotNull(detalle);

        var aforoInvalido = await cliente.PutAsJsonAsync($"/api/espacios/{area.Id}", new
        {
            nombre = detalle.Nombre,
            ubicacion = detalle.Ubicacion,
            categoria = "SalaComputo",
            aforo = 0,
            tipoAprobacion = "Docente",
            tipoModalidadPrestamo = "PuestoIndividual",
            tipoElementoPrestamo = "EquipoComputo",
            estado = "Activo",
            rowVersion = detalle.RowVersion
        });

        Assert.Equal(HttpStatusCode.BadRequest, aforoInvalido.StatusCode);

        var aforoBajo = await cliente.PutAsJsonAsync($"/api/espacios/{area.Id}", new
        {
            nombre = detalle.Nombre,
            ubicacion = detalle.Ubicacion,
            categoria = "SalaComputo",
            aforo = 1,
            tipoAprobacion = "Docente",
            tipoModalidadPrestamo = "PuestoIndividual",
            tipoElementoPrestamo = "EquipoComputo",
            estado = "Inactivo",
            rowVersion = detalle.RowVersion
        });

        Assert.Equal(HttpStatusCode.OK, aforoBajo.StatusCode);
    }

    [DockerFact]
    public async Task LasMutacionesExigenRolAdministrativo()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("areas_autorizacion");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var anonimo = await cliente.PostAsJsonAsync("/api/espacios", new
        {
            nombre = "Laboratorio",
            ubicacion = "Bloque B",
            categoria = "Laboratorio",
            aforo = 10,
            tipoAprobacion = "Docente",
            tipoModalidadPrestamo = "AreaCompleta",
            tipoElementoPrestamo = "LaboratorioEspecializado"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);

        await cliente.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = "Estudiante de Pruebas",
            correoInstitucional = "estudiante@uninpahu.edu.co",
            contrasena = "Clave.Segura.2026"
        });

        var sesion = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            correoInstitucional = "estudiante@uninpahu.edu.co",
            contrasena = "Clave.Segura.2026"
        });
        var token = await sesion.Content.ReadFromJsonAsync<TokenRespuesta>();
        Assert.NotNull(token);

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var denegado = await cliente.PostAsJsonAsync("/api/espacios", new
        {
            nombre = "Laboratorio",
            ubicacion = "Bloque B",
            categoria = "Laboratorio",
            aforo = 10,
            tipoAprobacion = "Docente",
            tipoModalidadPrestamo = "AreaCompleta",
            tipoElementoPrestamo = "LaboratorioEspecializado"
        });

        Assert.Equal(HttpStatusCode.Forbidden, denegado.StatusCode);
    }

    private static async Task AutenticarAdministradorAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            correoInstitucional = FabricaApiPruebas.CorreoAdmin,
            contrasena = FabricaApiPruebas.ContrasenaAdmin
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var token = await respuesta.Content.ReadFromJsonAsync<TokenRespuesta>();
        Assert.NotNull(token);

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static async Task<AreaDetalle> CrearAreaAsync(HttpClient cliente, string nombre, string ubicacion, int aforo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/espacios", new
        {
            nombre,
            ubicacion,
            categoria = "Laboratorio",
            aforo,
            tipoAprobacion = "Docente",
            tipoModalidadPrestamo = "AreaCompleta",
            tipoElementoPrestamo = "LaboratorioEspecializado"
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var area = await respuesta.Content.ReadFromJsonAsync<AreaDetalle>();
        Assert.NotNull(area);

        return area;
    }

    private sealed record TokenRespuesta(string Token);

    private sealed record AreaDetalle(
        Guid Id,
        string Nombre,
        string Ubicacion,
        int Categoria,
        int Aforo,
        int TipoAprobacion,
        int Estado,
        string CodigoQR,
        string RowVersion,
        IReadOnlyList<PuestoDetalle> Puestos);

    private sealed record PuestoDetalle(Guid Id, string Identificador, string CodigoQR, int Estado);

    private sealed record AreaResumen(Guid Id, string Nombre, string CodigoQR, int Estado);

    private sealed record PaginaAreas(IReadOnlyList<AreaResumen> Elementos, int Total, int Pagina, int Tamano);

    private sealed record AreaPublica(Guid Id, string Nombre, string Ubicacion, int Aforo);
}
