using System.Net;
using System.Net.Http.Json;
using Backend.Application.Common;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Backend.IntegrationTests.Usuarios;

/// <summary>
/// Pruebas de integración del autorregistro: primera cuenta administrativa, segunda
/// estudiante, dominio institucional, correo duplicado y autorregistro deshabilitado.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class RegistroUsuariosTests
{
    private const string Contrasena = "Clave.Segura.2026";

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public RegistroUsuariosTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task LaPrimeraCuentaEsAdministrativaYLaSiguienteEstudiante()
    {
        await using var fabrica = await CrearFabricaAsync("registro_bootstrap");
        using var cliente = fabrica.CreateClient();

        var primera = await cliente.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = "Ana Administrativa",
            correoInstitucional = "ana@uninpahu.edu.co",
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);

        var administradora = await primera.Content.ReadFromJsonAsync<RegistroRespuesta>();
        Assert.NotNull(administradora);
        Assert.Equal(RolesAplicacion.Administrativo, administradora.Rol);

        var segunda = await cliente.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = "Luis Estudiante",
            correoInstitucional = "luis@uninpahu.edu.co",
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);

        var estudiante = await segunda.Content.ReadFromJsonAsync<RegistroRespuesta>();
        Assert.NotNull(estudiante);
        Assert.Equal(RolesAplicacion.Estudiante, estudiante.Rol);

        var login = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            correoInstitucional = "ana@uninpahu.edu.co",
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [DockerFact]
    public async Task DosRegistrosConcurrentesSobreUnSistemaVacioProducenUnSoloAdministrador()
    {
        await using var fabrica = await CrearFabricaAsync("registro_concurrente");

        var respuestas = await Task.WhenAll(
            RegistrarAsync(fabrica, "ana@uninpahu.edu.co"),
            RegistrarAsync(fabrica, "luis@uninpahu.edu.co"));

        var creadas = respuestas
            .Where(respuesta => respuesta.Cuerpo is not null)
            .Select(respuesta => respuesta.Cuerpo!)
            .ToArray();

        Assert.NotEmpty(creadas);
        Assert.True(creadas.Count(cuerpo => cuerpo.Rol == RolesAplicacion.Administrativo) <= 1);
        Assert.All(creadas, cuerpo => Assert.Contains(cuerpo.Rol, new[] { RolesAplicacion.Administrativo, RolesAplicacion.Estudiante }));
    }

    [DockerFact]
    public async Task UnCorreoDeOtroDominioDevuelve400SinCrearLaCuenta()
    {
        await using var fabrica = await CrearFabricaAsync("registro_dominio");
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = "Persona Externa",
            correoInstitucional = "persona@gmail.com",
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var problema = await respuesta.Content.ReadFromJsonAsync<Problema>();
        Assert.NotNull(problema);
        Assert.EndsWith("/solicitud-invalida", problema.Type, StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task UnCorreoYaRegistradoDevuelve409()
    {
        await using var fabrica = await CrearFabricaAsync("registro_duplicado");
        using var cliente = fabrica.CreateClient();
        var solicitud = new
        {
            nombreCompleto = "Ana Administrativa",
            correoInstitucional = "ana@uninpahu.edu.co",
            contrasena = Contrasena
        };

        var primera = await cliente.PostAsJsonAsync("/api/usuarios/registro", solicitud);
        var repetida = await cliente.PostAsJsonAsync("/api/usuarios/registro", solicitud);

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [DockerFact]
    public async Task ElAutorregistroDeshabilitadoDevuelve403()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("registro_deshabilitado");
        await using var fabrica = FabricaApiPruebas.Crear(
            cadena,
            sobrescribir: new Dictionary<string, string?>
            {
                ["Seed:AdminEmail"] = string.Empty,
                ["Seed:AdminPassword"] = string.Empty,
                ["Identidad:PermitirAutorregistro"] = "false"
            });
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = "Ana",
            correoInstitucional = "ana@uninpahu.edu.co",
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private async Task<WebApplicationFactory<Program>> CrearFabricaAsync(string prefijo)
    {
        var cadena = await _sql.CrearBaseDeDatosAsync(prefijo);

        return FabricaApiPruebas.Crear(cadena, sobrescribir: new Dictionary<string, string?>
        {
            ["Seed:AdminEmail"] = string.Empty,
            ["Seed:AdminPassword"] = string.Empty
        });
    }

    private static async Task<(HttpStatusCode Estado, RegistroRespuesta? Cuerpo)> RegistrarAsync(
        WebApplicationFactory<Program> fabrica,
        string correo)
    {
        using var cliente = fabrica.CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = "Cuenta Concurrente",
            correoInstitucional = correo,
            contrasena = Contrasena
        });

        RegistroRespuesta? cuerpo = null;

        if (respuesta.StatusCode == HttpStatusCode.Created)
        {
            cuerpo = await respuesta.Content.ReadFromJsonAsync<RegistroRespuesta>();
        }

        return (respuesta.StatusCode, cuerpo);
    }

    private sealed record RegistroRespuesta(Guid Id, string NombreCompleto, string CorreoInstitucional, string Rol);

    private sealed record Problema(string Type, string Title, int Status, string? Detail);
}
