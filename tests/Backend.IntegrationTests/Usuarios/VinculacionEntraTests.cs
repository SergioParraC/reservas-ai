using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Backend.IntegrationTests.Usuarios;

/// <summary>
/// Pruebas de integración de la vinculación con Entra ID: vínculo, idempotencia,
/// desvinculación, discrepancia de correo, <c>oid</c> ajeno y falta de sesión.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class VinculacionEntraTests
{
    private const string Contrasena = "Clave.Segura.2026";
    private const string OidAna = "oid-ana-1111";
    private const string OidLuis = "oid-luis-2222";

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public VinculacionEntraTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task VincularEsIdempotenteYDesvincularEsIdempotente()
    {
        var validador = new ValidadorTokenFalso()
            .Agregar("token-ana", OidAna, "ana@uninpahu.edu.co")
            .Agregar("token-ana-repetido", OidAna, "ana@uninpahu.edu.co");

        await using var fabrica = await CrearFabricaAsync("vinculacion_exitosa", validador);
        using var cliente = fabrica.CreateClient();

        var cuenta = await RegistrarAsync(cliente, "ana@uninpahu.edu.co");
        var tokenLocal = await IniciarSesionAsync(cliente, "ana@uninpahu.edu.co");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenLocal.Token);

        var primera = await cliente.PostAsJsonAsync("/api/usuarios/me/vinculacion-entra", new { tokenEntra = "token-ana" });
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);

        var estado = await primera.Content.ReadFromJsonAsync<EstadoVinculacion>();
        Assert.NotNull(estado);
        Assert.True(estado.Vinculado);

        var repetida = await cliente.PostAsJsonAsync("/api/usuarios/me/vinculacion-entra", new { tokenEntra = "token-ana-repetido" });
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);

        Assert.True(await EstaVinculadaAsync(fabrica, cuenta.Id));

        var desvinculo = await cliente.DeleteAsync("/api/usuarios/me/vinculacion-entra");
        Assert.Equal(HttpStatusCode.NoContent, desvinculo.StatusCode);

        var desvinculoRepetido = await cliente.DeleteAsync("/api/usuarios/me/vinculacion-entra");
        Assert.Equal(HttpStatusCode.NoContent, desvinculoRepetido.StatusCode);

        Assert.False(await EstaVinculadaAsync(fabrica, cuenta.Id));

        // La contraseña local sigue operativa después de desvincular.
        var nuevoToken = await IniciarSesionAsync(cliente, "ana@uninpahu.edu.co");
        Assert.Contains(RolesAplicacion.Estudiante, nuevoToken.Roles);
    }

    [DockerFact]
    public async Task UnCorreoDistintoDevuelve409YConservaElEstado()
    {
        var validador = new ValidadorTokenFalso()
            .Agregar("token-ajeno", OidAna, "otra@uninpahu.edu.co");

        await using var fabrica = await CrearFabricaAsync("vinculacion_correo", validador);
        using var cliente = fabrica.CreateClient();

        var cuenta = await RegistrarAsync(cliente, "ana@uninpahu.edu.co");
        var tokenLocal = await IniciarSesionAsync(cliente, "ana@uninpahu.edu.co");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenLocal.Token);

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios/me/vinculacion-entra", new { tokenEntra = "token-ajeno" });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.False(await EstaVinculadaAsync(fabrica, cuenta.Id));
    }

    [DockerFact]
    public async Task UnOidYaVinculadoAOtroUsuarioDevuelve409()
    {
        var validador = new ValidadorTokenFalso()
            .Agregar("token-ana", OidAna, "ana@uninpahu.edu.co")
            .Agregar("token-luis-con-oid-ana", OidAna, "luis@uninpahu.edu.co");

        await using var fabrica = await CrearFabricaAsync("vinculacion_oid", validador);
        using var cliente = fabrica.CreateClient();

        await RegistrarAsync(cliente, "ana@uninpahu.edu.co");
        var cuentaLuis = await RegistrarAsync(cliente, "luis@uninpahu.edu.co");

        var tokenAna = await IniciarSesionAsync(cliente, "ana@uninpahu.edu.co");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAna.Token);
        var vinculacionAna = await cliente.PostAsJsonAsync("/api/usuarios/me/vinculacion-entra", new { tokenEntra = "token-ana" });
        Assert.Equal(HttpStatusCode.OK, vinculacionAna.StatusCode);

        var tokenLuis = await IniciarSesionAsync(cliente, "luis@uninpahu.edu.co");
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenLuis.Token);
        var conflicto = await cliente.PostAsJsonAsync("/api/usuarios/me/vinculacion-entra", new { tokenEntra = "token-luis-con-oid-ana" });

        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        Assert.False(await EstaVinculadaAsync(fabrica, cuentaLuis.Id));
    }

    [DockerFact]
    public async Task LaVinculacionSinSesionDevuelve401()
    {
        await using var fabrica = await CrearFabricaAsync("vinculacion_anonima", new ValidadorTokenFalso());
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios/me/vinculacion-entra", new { tokenEntra = "token-ana" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    private async Task<WebApplicationFactory<Program>> CrearFabricaAsync(string prefijo, IValidadorTokenEntra validador)
    {
        var cadena = await _sql.CrearBaseDeDatosAsync(prefijo);

        return FabricaApiPruebas.Crear(cadena, constructor =>
            constructor.ConfigureTestServices(servicios =>
            {
                servicios.RemoveAll<IValidadorTokenEntra>();
                servicios.AddScoped(_ => validador);
            }));
    }

    private static async Task<RegistroRespuesta> RegistrarAsync(HttpClient cliente, string correo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = "Cuenta de Pruebas",
            correoInstitucional = correo,
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var registrada = await respuesta.Content.ReadFromJsonAsync<RegistroRespuesta>();
        Assert.NotNull(registrada);

        return registrada;
    }

    private static async Task<TokenRespuesta> IniciarSesionAsync(HttpClient cliente, string correo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            correoInstitucional = correo,
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var token = await respuesta.Content.ReadFromJsonAsync<TokenRespuesta>();
        Assert.NotNull(token);

        return token;
    }

    private static async Task<bool> EstaVinculadaAsync(WebApplicationFactory<Program> fabrica, Guid usuarioId)
    {
        using var alcance = fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider
            .GetRequiredService<Backend.Infrastructure.Persistence.ApplicationDbContext>();

        var identificador = await contexto.Users
            .AsNoTracking()
            .Where(usuario => usuario.Id == usuarioId)
            .Select(usuario => usuario.IdentificadorFederado)
            .FirstOrDefaultAsync();

        return identificador is not null;
    }

    private sealed class ValidadorTokenFalso : IValidadorTokenEntra
    {
        private readonly Dictionary<string, IdentidadFederada> _tokens = new(StringComparer.Ordinal);

        public ValidadorTokenFalso Agregar(string token, string oid, string correo)
        {
            _tokens[token] = new IdentidadFederada(oid, "Cuenta de Pruebas", correo, []);
            return this;
        }

        public Task<Resultado<IdentidadFederada>> ValidarAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tokens.TryGetValue(token, out var identidad)
                ? Resultado<IdentidadFederada>.Exito(identidad)
                : Resultado<IdentidadFederada>.Fallo(TipoFallo.NoAutorizado, "El token de Entra ID no es válido."));
    }

    private sealed record RegistroRespuesta(Guid Id, string NombreCompleto, string CorreoInstitucional, string Rol);

    private sealed record TokenRespuesta(string Token, IReadOnlyCollection<string> Roles);

    private sealed record EstadoVinculacion(bool Vinculado, string CorreoInstitucional);
}
