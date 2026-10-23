using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Application.Autenticacion;
using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Backend.IntegrationTests.Smoke;

/// <summary>
/// Humo integral de la Etapa 2: registro del primer administrador, alta de área con
/// fotografía y dos puestos, plantilla de tres etiquetas, catálogo filtrado, vinculación
/// con Entra ID (validador simulado del host de pruebas) y cambio de rol.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class FlujoEtapa2Tests
{
    private const string Contrasena = "Clave.Segura.2026";
    private const string CorreoAdministrador = "ana@uninpahu.edu.co";
    private const string CorreoEstudiante = "luis@uninpahu.edu.co";

    private readonly SqlServerContainerFixture _sql;

    /// <summary>Inicializa el humo con el contenedor compartido.</summary>
    public FlujoEtapa2Tests(SqlServerContainerFixture sql) => _sql = sql;

    [DockerFact]
    public async Task ElFlujoCompletoDeLaEtapa2SeEjecutaExtremoAExtremo()
    {
        var raizAlmacen = Path.Combine(Path.GetTempPath(), "reservas-humo-" + Guid.NewGuid().ToString("N"));
        var cadena = await _sql.CrearBaseDeDatosAsync("humo_etapa2");
        var validador = new ValidadorTokenEntraFalso()
            .Agregar("token-luis", "oid-luis-humo", CorreoEstudiante);

        try
        {
            await using var fabrica = FabricaApiPruebas.Crear(
                cadena,
                constructor => constructor.ConfigureTestServices(servicios =>
                {
                    servicios.RemoveAll<IValidadorTokenEntra>();
                    servicios.AddScoped<IValidadorTokenEntra>(_ => validador);
                }),
                new Dictionary<string, string?>
                {
                    ["Seed:AdminEmail"] = string.Empty,
                    ["Seed:AdminPassword"] = string.Empty,
                    ["Almacen:RutaBase"] = raizAlmacen
                });

            using var cliente = fabrica.CreateClient();

            // 1. Registro del primer administrador del sistema.
            var administrador = await RegistrarAsync(cliente, "Ana Administrativa", CorreoAdministrador);
            Assert.Equal(RolesAplicacion.Administrativo, administrador.Rol);

            var tokenAdministrador = await IniciarSesionAsync(cliente, CorreoAdministrador);
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAdministrador.Token);

            // 2. Alta del área con su código QR propio.
            var alta = await cliente.PostAsJsonAsync("/api/espacios", new
            {
                nombre = "Laboratorio de Redes",
                ubicacion = "Bloque B",
                categoria = "Laboratorio",
                aforo = 3,
                tipoAprobacion = "Docente",
                tipoModalidadPrestamo = "AreaCompleta",
                tipoElementoPrestamo = "LaboratorioEspecializado"
            });

            Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

            var area = await alta.Content.ReadFromJsonAsync<AreaCreada>();
            Assert.NotNull(area);
            Assert.Equal(29, area.CodigoQR.Length);

            // 3. Fotografía del área en el almacén y URL pública.
            using (var formulario = new MultipartFormDataContent())
            {
                var imagen = new ByteArrayContent(ContenidoPng());
                imagen.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                formulario.Add(imagen, "imagen", "laboratorio.png");

                var subida = await cliente.PostAsync($"/api/espacios/{area.Id}/imagen", formulario);
                Assert.Equal(HttpStatusCode.OK, subida.StatusCode);

                using var documento = JsonDocument.Parse(await subida.Content.ReadAsStringAsync());
                var url = documento.RootElement.GetProperty("urlFotografia").GetString();
                Assert.NotNull(url);
                Assert.StartsWith("/media/espacios/", url, StringComparison.Ordinal);
            }

            // 4. Dos puestos individuales dentro del aforo.
            foreach (var identificador in new[] { "PC-01", "PC-02" })
            {
                var puesto = await cliente.PostAsJsonAsync(
                    $"/api/espacios/{area.Id}/puestos",
                    new { identificador, descripcionEquipo = "Equipo de escritorio" });

                Assert.Equal(HttpStatusCode.Created, puesto.StatusCode);
            }

            // 5. Plantilla QR con tres etiquetas: área y dos puestos.
            var plantilla = await cliente.GetAsync(
                $"/api/espacios/qr/plantilla?espacioIds={area.Id}&incluirPuestos=true&hoja=1");

            Assert.Equal(HttpStatusCode.OK, plantilla.StatusCode);
            Assert.Equal("1", plantilla.Headers.GetValues("X-Total-Hojas").Single());

            var svg = await plantilla.Content.ReadAsStringAsync();
            Assert.Equal(3, Regex.Matches(svg, "<path").Count);
            Assert.DoesNotContain("<image", svg, StringComparison.Ordinal);
            Assert.DoesNotContain("data:", svg, StringComparison.Ordinal);

            // 6. Catálogo público filtrado sin sesión y sin exponer el QR.
            using var anonimo = fabrica.CreateClient();
            var catalogo = await anonimo.GetAsync(
                "/api/catalogo/espacios?q=laboratorio&categoria=Laboratorio&aforoMinimo=2");

            Assert.Equal(HttpStatusCode.OK, catalogo.StatusCode);

            var textoCatalogo = await catalogo.Content.ReadAsStringAsync();
            using var pagina = JsonDocument.Parse(textoCatalogo);
            Assert.Equal(1, pagina.RootElement.GetProperty("total").GetInt32());
            Assert.DoesNotContain("codigoQR", textoCatalogo, StringComparison.OrdinalIgnoreCase);

            // 7. Segunda cuenta y vinculación con la identidad institucional.
            var estudiante = await RegistrarAsync(cliente, "Luis Estudiante", CorreoEstudiante);
            Assert.Equal(RolesAplicacion.Estudiante, estudiante.Rol);

            var tokenEstudiante = await IniciarSesionAsync(cliente, CorreoEstudiante);
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenEstudiante.Token);

            var vinculacion = await cliente.PostAsJsonAsync(
                "/api/usuarios/me/vinculacion-entra",
                new { tokenEntra = "token-luis" });

            Assert.Equal(HttpStatusCode.OK, vinculacion.StatusCode);

            var estado = await vinculacion.Content.ReadFromJsonAsync<EstadoVinculacion>();
            Assert.NotNull(estado);
            Assert.True(estado.Vinculado);

            // 8. Cambio de rol administrativo con efecto en el siguiente acceso.
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenAdministrador.Token);

            var cambio = await cliente.PutAsJsonAsync(
                $"/api/usuarios/{estudiante.Id}/rol",
                new { rol = RolesAplicacion.Docente });

            Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);

            var actualizado = await cambio.Content.ReadFromJsonAsync<CambioRol>();
            Assert.NotNull(actualizado);
            Assert.True(actualizado.RequiereRenovacion);

            var nuevoToken = await IniciarSesionAsync(cliente, CorreoEstudiante);
            Assert.Contains(RolesAplicacion.Docente, nuevoToken.Roles);
        }
        finally
        {
            if (Directory.Exists(raizAlmacen))
            {
                Directory.Delete(raizAlmacen, recursive: true);
            }
        }
    }

    private static async Task<Registro> RegistrarAsync(HttpClient cliente, string nombre, string correo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios/registro", new
        {
            nombreCompleto = nombre,
            correoInstitucional = correo,
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var registro = await respuesta.Content.ReadFromJsonAsync<Registro>();
        Assert.NotNull(registro);

        return registro;
    }

    private static async Task<RespuestaToken> IniciarSesionAsync(HttpClient cliente, string correo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            correoInstitucional = correo,
            contrasena = Contrasena
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var token = await respuesta.Content.ReadFromJsonAsync<RespuestaToken>();
        Assert.NotNull(token);

        return token;
    }

    private static byte[] ContenidoPng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];

    private sealed record Registro(Guid Id, string CorreoInstitucional, string Rol);

    private sealed record RespuestaToken(string Token, IReadOnlyCollection<string> Roles);

    private sealed record AreaCreada(Guid Id, string CodigoQR);

    private sealed record EstadoVinculacion(bool Vinculado);

    private sealed record CambioRol(string Rol, bool RequiereRenovacion);

    private sealed class ValidadorTokenEntraFalso : IValidadorTokenEntra
    {
        private readonly Dictionary<string, IdentidadFederada> _tokens = new(StringComparer.Ordinal);

        public ValidadorTokenEntraFalso Agregar(string token, string oid, string correo)
        {
            _tokens[token] = new IdentidadFederada(oid, "Luis Estudiante", correo, []);
            return this;
        }

        public Task<Resultado<IdentidadFederada>> ValidarAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tokens.TryGetValue(token, out var identidad)
                ? Resultado<IdentidadFederada>.Exito(identidad)
                : Resultado<IdentidadFederada>.Fallo(TipoFallo.NoAutorizado, "El token de Entra ID no es válido."));
    }
}
