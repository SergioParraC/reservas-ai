using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;

namespace Backend.IntegrationTests.Calendario;

/// <summary>
/// Pruebas HTTP de las series semestrales: omisión del día no operativo, colisión íntegra y
/// ámbito de la consulta propia.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class SeriesEndpointsTests
{
    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public SeriesEndpointsTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task LaSerieOmiteElDiaNoOperativoYColisionaSinPersistir()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("series_flujo");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var administrador = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(administrador);

        var hoy = SoporteApiPruebas.Hoy();
        var primerMartes = SoporteApiPruebas.ProximoMartes();
        var ultimoMartes = primerMartes.AddDays(112);

        await SoporteApiPruebas.ActivarPeriodoAsync(administrador, hoy.AddDays(-10), ultimoMartes.AddDays(10));
        var areaId = await SoporteApiPruebas.CrearAreaDeAreaCompletaAsync(administrador);

        var noOperativa = await administrador.PostAsJsonAsync("/api/franjas-no-operativas", new
        {
            fechaEspecifica = primerMartes.AddDays(21),
            horaInicio = "00:00",
            horaFin = "23:59",
            esFeriado = true,
            motivo = "Feriado institucional"
        });

        Assert.Equal(HttpStatusCode.Created, noOperativa.StatusCode);

        using var docente = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "docente.series@uninpahu.edu.co",
            "Docente");

        var serie = await docente.PostAsJsonAsync("/api/reservas/recurrentes", new
        {
            espacioId = areaId,
            tipoRecurrencia = "Semanal",
            diaSemana = "Tuesday",
            horaInicio = "14:00",
            horaFin = "16:00",
            periodoInicio = primerMartes,
            periodoFin = ultimoMartes,
            materia = "Cálculo II"
        });

        Assert.Equal(HttpStatusCode.Created, serie.StatusCode);

        using var creada = JsonDocument.Parse(await serie.Content.ReadAsStringAsync());
        Assert.Equal(16, creada.RootElement.GetProperty("totalOcurrencias").GetInt32());
        Assert.Contains(
            creada.RootElement.GetProperty("ocurrencias").EnumerateArray(),
            ocurrencia => ocurrencia.GetProperty("omitida").GetBoolean());

        var colision = await docente.PostAsJsonAsync("/api/reservas/recurrentes", new
        {
            espacioId = areaId,
            tipoRecurrencia = "Semanal",
            diaSemana = "Tuesday",
            horaInicio = "14:00",
            horaFin = "16:00",
            periodoInicio = primerMartes,
            periodoFin = ultimoMartes,
            materia = "Física II"
        });

        Assert.Equal(HttpStatusCode.Conflict, colision.StatusCode);
        Assert.Contains("colision-serie", await colision.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var propias = await docente.GetAsync("/api/reservas/recurrentes/me");
        Assert.Equal(HttpStatusCode.OK, propias.StatusCode);

        using var pagina = JsonDocument.Parse(await propias.Content.ReadAsStringAsync());
        Assert.Equal(1, pagina.RootElement.GetProperty("total").GetInt32());

        using var estudiante = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "estudiante.series@uninpahu.edu.co",
            "Estudiante");

        var denegado = await estudiante.PostAsJsonAsync("/api/reservas/recurrentes", new
        {
            espacioId = areaId,
            tipoRecurrencia = "Semanal",
            diaSemana = "Tuesday",
            horaInicio = "14:00",
            horaFin = "16:00",
            periodoInicio = primerMartes,
            periodoFin = ultimoMartes,
            materia = "Cálculo II"
        });

        Assert.Equal(HttpStatusCode.Forbidden, denegado.StatusCode);
    }

    [DockerFact]
    public async Task DosDocentesConcurrentesSoloConfirmanUnaSerie()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("series_concurrentes");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var administrador = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(administrador);

        var hoy = SoporteApiPruebas.Hoy();
        var primerMartes = SoporteApiPruebas.ProximoMartes();
        var ultimoMartes = primerMartes.AddDays(112);

        await SoporteApiPruebas.ActivarPeriodoAsync(administrador, hoy.AddDays(-10), ultimoMartes.AddDays(10));
        var areaId = await SoporteApiPruebas.CrearAreaDeAreaCompletaAsync(administrador);

        using var docenteA = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "docente.a.concurrente@uninpahu.edu.co",
            "Docente");

        using var docenteB = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "docente.b.concurrente@uninpahu.edu.co",
            "Docente");

        var peticionA = docenteA.PostAsJsonAsync("/api/reservas/recurrentes", new
        {
            espacioId = areaId,
            tipoRecurrencia = "Semanal",
            diaSemana = "Tuesday",
            horaInicio = "14:00",
            horaFin = "16:00",
            periodoInicio = primerMartes,
            periodoFin = ultimoMartes,
            materia = "Cálculo II"
        });

        var peticionB = docenteB.PostAsJsonAsync("/api/reservas/recurrentes", new
        {
            espacioId = areaId,
            tipoRecurrencia = "Semanal",
            diaSemana = "Tuesday",
            horaInicio = "14:00",
            horaFin = "16:00",
            periodoInicio = primerMartes,
            periodoFin = ultimoMartes,
            materia = "Física II"
        });

        var respuestas = await Task.WhenAll(peticionA, peticionB);

        Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Conflict));

        var series = await Task.WhenAll(
            ContarSeriesAsync(docenteA),
            ContarSeriesAsync(docenteB));

        Assert.Equal(1, series.Sum());
    }

    private static async Task<int> ContarSeriesAsync(HttpClient cliente)
    {
        using var documento = JsonDocument.Parse(
            await cliente.GetStringAsync("/api/reservas/recurrentes/me"));

        return documento.RootElement.GetProperty("total").GetInt32();
    }

    [DockerFact]
    public async Task LaSerieColisionaConUnaReservaPuntualDeUnPuestoDelArea()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("series_puntual");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var administrador = fabrica.CreateClient();
        await SoporteApiPruebas.AutenticarAdministradorAsync(administrador);

        var hoy = SoporteApiPruebas.Hoy();
        var primerMartes = SoporteApiPruebas.ProximoMartes();
        var ultimoMartes = primerMartes.AddDays(112);

        await SoporteApiPruebas.ActivarPeriodoAsync(administrador, hoy.AddDays(-10), ultimoMartes.AddDays(10));
        var areaId = await SoporteApiPruebas.CrearAreaDeAreaCompletaAsync(administrador);

        var puesto = await administrador.PostAsJsonAsync(
            $"/api/espacios/{areaId}/puestos",
            new { identificador = "PC-01" });

        Assert.Equal(HttpStatusCode.Created, puesto.StatusCode);

        using var puestoDocumento = JsonDocument.Parse(await puesto.Content.ReadAsStringAsync());
        var puestoId = puestoDocumento.RootElement.GetProperty("id").GetGuid();

        var martesReservado = primerMartes.AddDays(14);
        var local = martesReservado.ToDateTime(new TimeOnly(14, 0), DateTimeKind.Unspecified);
        var inicio = new DateTimeOffset(local, SoporteApiPruebas.Zona.GetUtcOffset(local));

        await using (var conexion = new SqlConnection(cadena))
        {
            await conexion.OpenAsync();

            await using var comando = conexion.CreateCommand();
            comando.CommandText = """
                DECLARE @usuario uniqueidentifier = (SELECT TOP 1 [Id] FROM [AspNetUsers]);
                INSERT INTO [Reservas] ([Id], [PuestoId], [UsuarioId], [Inicio], [Fin], [Estado], [CreadaEn])
                VALUES (NEWID(), @puestoId, @usuario, @inicio, @fin, 1, SYSDATETIMEOFFSET());
                """;
            comando.Parameters.AddWithValue("@puestoId", puestoId);
            comando.Parameters.AddWithValue("@inicio", inicio);
            comando.Parameters.AddWithValue("@fin", inicio.AddHours(1));

            await comando.ExecuteNonQueryAsync();
        }

        using var docente = await SoporteApiPruebas.CrearClienteConRolAsync(
            fabrica,
            administrador,
            "docente.puntual@uninpahu.edu.co",
            "Docente");

        var serie = await docente.PostAsJsonAsync("/api/reservas/recurrentes", new
        {
            espacioId = areaId,
            tipoRecurrencia = "Semanal",
            diaSemana = "Tuesday",
            horaInicio = "14:00",
            horaFin = "16:00",
            periodoInicio = primerMartes,
            periodoFin = ultimoMartes,
            materia = "Cálculo II"
        });

        Assert.Equal(HttpStatusCode.Conflict, serie.StatusCode);
        Assert.Contains("colision-serie", await serie.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
