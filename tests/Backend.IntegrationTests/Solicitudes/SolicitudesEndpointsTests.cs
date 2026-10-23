using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Backend.IntegrationTests.Infraestructura;

namespace Backend.IntegrationTests.Solicitudes;

/// <summary>
/// Pruebas HTTP de la radicación con pre-informe: alta válida, descarga restringida, límites de
/// tamaño y tipo, bandeja por competencia y transiciones de estado con notificación.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class SolicitudesEndpointsTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public SolicitudesEndpointsTests(SqlServerContainerFixture sql) => _sql = sql;

    private static byte[] Pdf() => Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF");

    private static MultipartFormDataContent Formulario(
        Guid espacioId,
        byte[]? contenido = null,
        string tipoContenido = "application/pdf")
    {
        var inicio = SoporteApiPruebas.Hoy().AddDays(1).ToDateTime(new TimeOnly(14, 0), DateTimeKind.Unspecified);
        var zona = SoporteApiPruebas.Zona;
        var inicioZona = new DateTimeOffset(inicio, zona.GetUtcOffset(inicio));

        var formulario = new MultipartFormDataContent
        {
            { new StringContent(espacioId.ToString()), "espacioId" },
            { new StringContent(inicioZona.ToString("O")), "inicio" },
            { new StringContent(inicioZona.AddHours(2).ToString("O")), "fin" },
            { new StringContent("Bases de datos"), "materia" },
            { new StringContent("Ing. Laura Gómez"), "docenteResponsable" },
            { new StringContent("Práctica de laboratorio con preinforme aprobado."), "justificacion" }
        };

        if (contenido is not null)
        {
            var archivo = new ByteArrayContent(contenido);
            archivo.Headers.ContentType = new MediaTypeHeaderValue(tipoContenido);
            formulario.Add(archivo, "archivo", "informe.pdf");
        }

        return formulario;
    }

    [DockerFact]
    public async Task LaSolicitudSeRadicaSeDescargaYSeDecidePorCompetencia()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("solicitudes_flujo");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var administrador = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(administrador);

        var hoy = SoporteApiPruebas.Hoy();
        await SoporteApiPruebas.ActivarPeriodoAsync(administrador, hoy.AddDays(-10), hoy.AddDays(100));
        var areaId = await SoporteApiPruebas.CrearAreaDeAreaCompletaAsync(administrador);

        using var estudiante = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "estudiante.solicitudes@uninpahu.edu.co",
            "Estudiante");

        using var anonimo = fabrica.CreateClient();
        using var formularioAnonimo = Formulario(areaId, Pdf());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsync("/api/solicitudes", formularioAnonimo)).StatusCode);

        using var formularioSinArchivo = Formulario(areaId);
        var sinArchivo = await estudiante.PostAsync("/api/solicitudes", formularioSinArchivo);
        Assert.Equal(HttpStatusCode.BadRequest, sinArchivo.StatusCode);

        using var formularioTipo = Formulario(areaId, Encoding.ASCII.GetBytes("no es pdf"), "text/plain");
        var tipoInvalido = await estudiante.PostAsync("/api/solicitudes", formularioTipo);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, tipoInvalido.StatusCode);
        Assert.Contains("archivo-no-valido", await tipoInvalido.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var formularioGrande = Formulario(areaId, new byte[5_500_000]);
        var demasiadoGrande = await estudiante.PostAsync("/api/solicitudes", formularioGrande);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, demasiadoGrande.StatusCode);
        Assert.Contains(
            "archivo-demasiado-grande",
            await demasiadoGrande.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);

        using var formulario = Formulario(areaId, Pdf());
        var alta = await estudiante.PostAsync("/api/solicitudes", formulario);

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        using var creada = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        var solicitudId = creada.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(1, creada.RootElement.GetProperty("estado").GetInt32());
        Assert.Equal("informe.pdf", creada.RootElement.GetProperty("archivoNombreOriginal").GetString());

        var descarga = await estudiante.GetAsync($"/api/solicitudes/{solicitudId}/preinforme");
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
        Assert.Equal("application/pdf", descarga.Content.Headers.ContentType?.MediaType);
        Assert.Contains("attachment", descarga.Content.Headers.ContentDisposition?.DispositionType ?? string.Empty);
        Assert.True(descarga.Headers.CacheControl?.NoStore);
        Assert.True(descarga.Headers.CacheControl?.NoCache);

        var propias = await estudiante.GetAsync("/api/solicitudes/me");
        Assert.Equal(HttpStatusCode.OK, propias.StatusCode);

        using var historial = JsonDocument.Parse(await propias.Content.ReadAsStringAsync());
        Assert.Equal(1, historial.RootElement.GetProperty("total").GetInt32());

        var bandejaEstudiante = await estudiante.GetAsync("/api/bandeja-aprobaciones");
        Assert.Equal(HttpStatusCode.Forbidden, bandejaEstudiante.StatusCode);

        using var tercero = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "tercero.solicitudes@uninpahu.edu.co",
            "Estudiante");

        var ajena = await tercero.GetAsync($"/api/solicitudes/{solicitudId}/preinforme");
        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);
        Assert.Contains("solicitud-ajena", await ajena.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var docente = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "docente.solicitudes@uninpahu.edu.co",
            "Docente");

        var bandeja = await docente.GetAsync("/api/bandeja-aprobaciones?estado=Radicado");
        Assert.Equal(HttpStatusCode.OK, bandeja.StatusCode);

        using var pagina = JsonDocument.Parse(await bandeja.Content.ReadAsStringAsync());
        Assert.Equal(1, pagina.RootElement.GetProperty("total").GetInt32());

        var revision = await docente.PutAsJsonAsync(
            $"/api/solicitudes/{solicitudId}/estado",
            new { estado = "EnRevision" });

        Assert.Equal(HttpStatusCode.OK, revision.StatusCode);

        var rechazo = await docente.PutAsJsonAsync(
            $"/api/solicitudes/{solicitudId}/estado",
            new { estado = "Rechazado" });

        Assert.Equal(HttpStatusCode.BadRequest, rechazo.StatusCode);
        Assert.Contains("motivo", await rechazo.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var aprobacion = await docente.PutAsJsonAsync(
            $"/api/solicitudes/{solicitudId}/estado",
            new { estado = "Aprobado" });

        Assert.Equal(HttpStatusCode.OK, aprobacion.StatusCode);

        using var decidida = JsonDocument.Parse(await aprobacion.Content.ReadAsStringAsync());
        Assert.Equal(3, decidida.RootElement.GetProperty("estado").GetInt32());
    }

    [DockerFact]
    public async Task LaAprobacionConcurrenteSoloOcupaUnaVezLaFranja()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("solicitudes_concurrentes");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var administrador = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(administrador);

        var hoy = SoporteApiPruebas.Hoy();
        await SoporteApiPruebas.ActivarPeriodoAsync(administrador, hoy.AddDays(-10), hoy.AddDays(100));
        var areaId = await SoporteApiPruebas.CrearAreaDeAreaCompletaAsync(administrador);

        using var primera = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "primera.concurrente@uninpahu.edu.co",
            "Estudiante");

        using var segunda = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "segunda.concurrente@uninpahu.edu.co",
            "Estudiante");

        using var docente = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "docente.concurrente@uninpahu.edu.co",
            "Docente");

        var solicitudA = await RadicarAsync(primera, areaId);
        var solicitudB = await RadicarAsync(segunda, areaId);

        Assert.NotEqual(solicitudA, solicitudB);

        foreach (var solicitudId in new[] { solicitudA, solicitudB })
        {
            var revision = await docente.PutAsJsonAsync(
                $"/api/solicitudes/{solicitudId}/estado",
                new { estado = "EnRevision" });

            Assert.Equal(HttpStatusCode.OK, revision.StatusCode);
        }

        var aprobaciones = await Task.WhenAll(
            docente.PutAsJsonAsync($"/api/solicitudes/{solicitudA}/estado", new { estado = "Aprobado" }),
            docente.PutAsJsonAsync($"/api/solicitudes/{solicitudB}/estado", new { estado = "Aprobado" }));

        Assert.Equal(1, aprobaciones.Count(respuesta => respuesta.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, aprobaciones.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Conflict));
    }

    private static async Task<Guid> RadicarAsync(HttpClient cliente, Guid espacioId)
    {
        using var formulario = Formulario(espacioId, Pdf());
        var alta = await cliente.PostAsync("/api/solicitudes", formulario);

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        using var documento = JsonDocument.Parse(await alta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("id").GetGuid();
    }
}
