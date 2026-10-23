using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Backend.IntegrationTests.Areas;

/// <summary>
/// Pruebas del catálogo público: filtros combinables, comparación sin acentos,
/// paginación, exposición mínima y filtrado de áreas inactivas.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class CatalogoAreasTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public CatalogoAreasTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElCatalogoFiltraSinAcentosPorCategoriaYAforoYNoExponeElQr()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("catalogo_filtros");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var admin = fabrica.CreateClient();
        await AutenticarAdministradorAsync(admin);

        await CrearAreaAsync(admin, "Laboratório de Redes", "Bloque B", "Laboratorio", 25);
        await CrearAreaAsync(admin, "Auditorio Central", "Bloque A", "Auditorio", 60);
        await CrearAreaAsync(admin, "Laboratorio de Química", "Bloque C", "Laboratorio", 10);

        using var anonimo = fabrica.CreateClient();

        var texto = await anonimo.GetFromJsonAsync<PaginaCatalogo>("/api/catalogo/espacios?q=laboratorio");
        Assert.NotNull(texto);
        Assert.Equal(2, texto.Total);

        var combinado = await anonimo.GetFromJsonAsync<PaginaCatalogo>(
            "/api/catalogo/espacios?categoria=Laboratorio&aforoMinimo=20");
        Assert.NotNull(combinado);
        Assert.Equal(1, combinado.Total);
        Assert.Equal("Laboratório de Redes", combinado.Elementos[0].Nombre);

        var vacio = await anonimo.GetFromJsonAsync<PaginaCatalogo>("/api/catalogo/espacios?q=inexistente");
        Assert.NotNull(vacio);
        Assert.Equal(0, vacio.Total);
        Assert.Empty(vacio.Elementos);

        var segundaPagina = await anonimo.GetFromJsonAsync<PaginaCatalogo>("/api/catalogo/espacios?pagina=2&tamano=2");
        Assert.NotNull(segundaPagina);
        Assert.Equal(3, segundaPagina.Total);
        Assert.Single(segundaPagina.Elementos);

        var crudo = await anonimo.GetStringAsync("/api/catalogo/espacios");
        Assert.DoesNotContain("codigoQR", crudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fotografiaRuta", crudo, StringComparison.OrdinalIgnoreCase);
    }

    [DockerFact]
    public async Task LaPlantillaQrIncluyeLosPuestosYPaginaLasHojas()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("catalogo_plantilla");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();
        await AutenticarAdministradorAsync(cliente);

        var area = await CrearAreaAsync(cliente, "Laboratorio de Redes", "Bloque B", "Laboratorio", 3);

        foreach (var identificador in new[] { "PC-01", "PC-02" })
        {
            var puesto = await cliente.PostAsJsonAsync($"/api/espacios/{area.Id}/puestos", new { identificador });
            Assert.Equal(HttpStatusCode.Created, puesto.StatusCode);
        }

        var plantilla = await cliente.GetAsync(
            $"/api/espacios/qr/plantilla?espacioIds={area.Id}&incluirPuestos=true&hoja=1");

        Assert.Equal(HttpStatusCode.OK, plantilla.StatusCode);
        Assert.Equal("image/svg+xml", plantilla.Content.Headers.ContentType?.MediaType);
        Assert.Equal("1", plantilla.Headers.GetValues("X-Total-Hojas").Single());

        var svg = await plantilla.Content.ReadAsStringAsync();
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(svg, "<path").Count);

        var hojaInexistente = await cliente.GetAsync(
            $"/api/espacios/qr/plantilla?espacioIds={area.Id}&incluirPuestos=true&hoja=2");
        Assert.Equal(HttpStatusCode.BadRequest, hojaInexistente.StatusCode);

        var sinUnidades = await cliente.GetAsync("/api/espacios/qr/plantilla");
        Assert.Equal(HttpStatusCode.BadRequest, sinUnidades.StatusCode);
    }

    [DockerFact]
    public async Task LaFotografiaSePublicaBajoMediaYSeRechazaUnTipoNoPermitido()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "reservas-media-" + Guid.NewGuid().ToString("N"));
        var cadena = await _sql.CrearBaseDeDatosAsync("catalogo_media");

        try
        {
            await using var fabrica = FabricaApiPruebas.Crear(cadena, sobrescribir: new Dictionary<string, string?>
            {
                ["Almacen:RutaBase"] = raiz
            });
            using var cliente = fabrica.CreateClient();
            await AutenticarAdministradorAsync(cliente);

            var area = await CrearAreaAsync(cliente, "Laboratorio de Medios", "Bloque D", "Laboratorio", 10);

            using var formulario = new MultipartFormDataContent();
            var archivo = new ByteArrayContent(ContenidoPng());
            archivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            formulario.Add(archivo, "imagen", "foto.png");

            var subida = await cliente.PostAsync($"/api/espacios/{area.Id}/imagen", formulario);
            Assert.Equal(HttpStatusCode.OK, subida.StatusCode);

            using var documento = JsonDocument.Parse(await subida.Content.ReadAsStringAsync());
            var url = documento.RootElement.GetProperty("urlFotografia").GetString();
            Assert.NotNull(url);
            Assert.StartsWith("/media/espacios/", url, StringComparison.Ordinal);
            Assert.DoesNotContain(raiz, url, StringComparison.OrdinalIgnoreCase);

            var descarga = await cliente.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);

            using var reemplazo = new MultipartFormDataContent();
            var segundaImagen = new ByteArrayContent(ContenidoPng());
            segundaImagen.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            reemplazo.Add(segundaImagen, "imagen", "foto-2.png");

            var segundaSubida = await cliente.PostAsync($"/api/espacios/{area.Id}/imagen", reemplazo);
            Assert.Equal(HttpStatusCode.OK, segundaSubida.StatusCode);

            using var segundoDocumento = JsonDocument.Parse(await segundaSubida.Content.ReadAsStringAsync());
            var segundaUrl = segundoDocumento.RootElement.GetProperty("urlFotografia").GetString();
            Assert.NotNull(segundaUrl);
            Assert.NotEqual(url, segundaUrl);

            var anterior = await cliente.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, anterior.StatusCode);

            using var invalido = new MultipartFormDataContent();
            var gif = new ByteArrayContent([0x47, 0x49, 0x46, 0x38, 0x39, 0x61]);
            gif.Headers.ContentType = new MediaTypeHeaderValue("image/gif");
            invalido.Add(gif, "imagen", "foto.gif");

            var rechazo = await cliente.PostAsync($"/api/espacios/{area.Id}/imagen", invalido);
            Assert.Equal(HttpStatusCode.BadRequest, rechazo.StatusCode);

            var eliminacion = await cliente.DeleteAsync($"/api/espacios/{area.Id}/imagen");
            Assert.Equal(HttpStatusCode.NoContent, eliminacion.StatusCode);
        }
        finally
        {
            if (Directory.Exists(raiz))
            {
                Directory.Delete(raiz, recursive: true);
            }
        }
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

    private static async Task<AreaCreada> CrearAreaAsync(
        HttpClient cliente,
        string nombre,
        string ubicacion,
        string categoria,
        int aforo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/espacios", new
        {
            nombre,
            ubicacion,
            categoria,
            aforo,
            tipoAprobacion = "Docente"
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var area = await respuesta.Content.ReadFromJsonAsync<AreaCreada>();
        Assert.NotNull(area);

        return area;
    }

    private static byte[] ContenidoPng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];

    private sealed record TokenRespuesta(string Token);

    private sealed record AreaCreada(Guid Id, string Nombre, string CodigoQR);

    private sealed record AreaCatalogo(Guid Id, string Nombre, int Aforo);

    private sealed record PaginaCatalogo(IReadOnlyList<AreaCatalogo> Elementos, int Total, int Pagina, int Tamano);
}
