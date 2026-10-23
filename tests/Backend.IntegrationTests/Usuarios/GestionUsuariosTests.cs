using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Application.Common;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Backend.IntegrationTests.Usuarios;

/// <summary>
/// Pruebas de integración de la consola administrativa: listado, filtros, cambio de rol,
/// efecto en el siguiente inicio de sesión y guardas de último administrador y autoedición.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class GestionUsuariosTests
{
    private const string Contrasena = "Clave.Segura.2026";

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa la prueba con el contenedor compartido.</summary>
    public GestionUsuariosTests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task LaConsolaListaCuentasYCambiaElRolConEfectoEnElSiguienteAcceso()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("gestion_usuarios");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var estudiante = await RegistrarAsync(cliente, "luis@uninpahu.edu.co");
        var tokenAdmin = await IniciarSesionAsync(cliente, FabricaApiPruebas.CorreoAdmin, FabricaApiPruebas.ContrasenaAdmin);

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAdmin.Token);

        var listado = await cliente.GetAsync("/api/usuarios?rol=Estudiante&vinculado=false");

        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);

        var pagina = await listado.Content.ReadFromJsonAsync<PaginaUsuarios>();
        Assert.NotNull(pagina);
        Assert.Equal(1, pagina.Total);
        Assert.Single(pagina.Elementos);
        Assert.Equal("luis@uninpahu.edu.co", pagina.Elementos[0].CorreoInstitucional);
        Assert.False(pagina.Elementos[0].Vinculado);
        Assert.Contains(RolesAplicacion.Estudiante, pagina.Elementos[0].Rol ?? string.Empty);

        var cambio = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{estudiante.Id}/rol",
            new { rol = RolesAplicacion.Docente });

        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);

        var actualizado = await cambio.Content.ReadFromJsonAsync<CambioRolRespuesta>();
        Assert.NotNull(actualizado);
        Assert.Equal(RolesAplicacion.Docente, actualizado.Rol);
        Assert.True(actualizado.RequiereRenovacion);

        var tokenEstudiante = await IniciarSesionAsync(cliente, "luis@uninpahu.edu.co", Contrasena);
        Assert.Contains(RolesAplicacion.Docente, tokenEstudiante.Roles);
    }

    [DockerFact]
    public async Task UnRolAcademicoNoAccedeALaConsolaYUnAnonimoRecibe401()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("gestion_autorizacion");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var anonimo = await cliente.GetAsync("/api/usuarios");
        Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);

        await RegistrarAsync(cliente, "luis@uninpahu.edu.co");
        var tokenEstudiante = await IniciarSesionAsync(cliente, "luis@uninpahu.edu.co", Contrasena);

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenEstudiante.Token);
        var denegado = await cliente.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, denegado.StatusCode);
    }

    [DockerFact]
    public async Task LaAutoedicionYElRolNoSoportadoSeRechazan()
    {
        var cadena = await _sql.CrearBaseDeDatosAsync("gestion_guardas");
        await using var fabrica = FabricaApiPruebas.Crear(cadena);
        using var cliente = fabrica.CreateClient();

        var tokenAdmin = await IniciarSesionAsync(cliente, FabricaApiPruebas.CorreoAdmin, FabricaApiPruebas.ContrasenaAdmin);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAdmin.Token);

        var perfil = await cliente.GetAsync("/api/auth/me");
        var administrador = await perfil.Content.ReadFromJsonAsync<PerfilRespuesta>();
        Assert.NotNull(administrador);

        var autoedicion = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{administrador.Id}/rol",
            new { rol = RolesAplicacion.Docente });

        Assert.Equal(HttpStatusCode.Conflict, autoedicion.StatusCode);

        var rolInvalido = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/rol",
            new { rol = "Superusuario" });

        Assert.Equal(HttpStatusCode.BadRequest, rolInvalido.StatusCode);

        var inexistente = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{Guid.NewGuid()}/rol",
            new { rol = RolesAplicacion.Docente });

        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    private static async Task<RegistroRespuesta> RegistrarAsync(HttpClient cliente, string correo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = "Luis Estudiante",
            correoInstitucional = correo,
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var registrada = await respuesta.Content.ReadFromJsonAsync<RegistroRespuesta>();
        Assert.NotNull(registrada);

        return registrada;
    }

    private static async Task<TokenRespuesta> IniciarSesionAsync(HttpClient cliente, string correo, string contrasena)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            correoInstitucional = correo,
            contrasena
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var token = await respuesta.Content.ReadFromJsonAsync<TokenRespuesta>();
        Assert.NotNull(token);

        return token;
    }

    private sealed record RegistroRespuesta(Guid Id, string NombreCompleto, string CorreoInstitucional, string Rol);

    private sealed record TokenRespuesta(string Token, string TipoToken, DateTimeOffset ExpiraEn, IReadOnlyCollection<string> Roles);

    private sealed record PerfilRespuesta(Guid Id, string CorreoInstitucional, IReadOnlyCollection<string> Roles);

    private sealed record UsuarioItem(Guid Id, string NombreCompleto, string CorreoInstitucional, string? Rol, bool Vinculado);

    private sealed record PaginaUsuarios(IReadOnlyList<UsuarioItem> Elementos, int Total, int Pagina, int Tamano);

    private sealed record CambioRolRespuesta(Guid Id, string CorreoInstitucional, string Rol, bool RequiereRenovacion);
}
