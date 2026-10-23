using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Backend.IntegrationTests.Infraestructura;

/// <summary>
/// Construye una API real (WebApplicationFactory) contra una base de datos del contenedor.
/// La configuración de pruebas se inyecta como último origen, de modo que sustituye
/// a appsettings y User Secrets (también en CI, donde no existen secretos locales).
/// </summary>
internal static class FabricaApiPruebas
{
    /// <summary>Clave de firma usada en las pruebas.</summary>
    public const string ClaveJwt = "clave-de-pruebas-suficientemente-larga-para-hmac-256";

    /// <summary>Emisor de los tokens locales en pruebas.</summary>
    public const string EmisorLocal = "https://api.reservas-espacios.edu";

    /// <summary>Audiencia de los tokens locales en pruebas.</summary>
    public const string AudienciaLocal = "reservas-espacios-spa";

    /// <summary>Correo del administrador sembrado.</summary>
    public const string CorreoAdmin = "admin@uninpahu.edu.co";

    /// <summary>Contraseña del administrador sembrado.</summary>
    public const string ContrasenaAdmin = "Clave.Segura.2026";

    /// <summary>Inquilino de Entra ID simulado en las pruebas.</summary>
    public const string TenantIdPrueba = "11111111-2222-3333-4444-555555555555";

    /// <summary>Cliente (audiencia) de Entra ID simulado.</summary>
    public const string ClienteIdPrueba = "99999999-8888-7777-6666-555555555555";

    /// <summary>Emisor OIDC del inquilino simulado.</summary>
    public const string EmisorEntra = "https://login.microsoftonline.com/" + TenantIdPrueba + "/v2.0";

    /// <summary>Origen permitido por la política CORS en pruebas.</summary>
    public const string OrigenSpa = "http://localhost:4200";

    /// <summary>
    /// Crea la fábrica con la configuración base de autenticación y siembra.
    /// La configuración se publica como variables de entorno porque <c>Program</c> lee
    /// <c>builder.Configuration</c> antes de <c>Build()</c> y las variables de entorno
    /// tienen precedencia sobre appsettings y User Secrets.
    /// </summary>
    /// <param name="cadena">Cadena de conexión a la base de datos de la prueba.</param>
    /// <param name="ajustar">Ajustes adicionales sobre el host.</param>
    /// <param name="sobrescribir">Valores de configuración que reemplazan los base.</param>
    /// <param name="entorno">Entorno de ejecución del host.</param>
    public static WebApplicationFactory<Program> Crear(
        string cadena,
        Action<IWebHostBuilder>? ajustar = null,
        IDictionary<string, string?>? sobrescribir = null,
        string entorno = "Development")
    {
        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConnectionStrings:DefaultConnection"] = cadena,
            ["Jwt:Key"] = ClaveJwt,
            ["Jwt:Issuer"] = EmisorLocal,
            ["Jwt:Audience"] = AudienciaLocal,
            ["Jwt:ExpiraMinutos"] = "60",
            ["Seed:AdminEmail"] = CorreoAdmin,
            ["Seed:AdminPassword"] = ContrasenaAdmin,
            ["Seed:AdminNombre"] = "Administradora de Pruebas",
            ["Cors:OrigenesPermitidos:0"] = OrigenSpa
        };

        if (sobrescribir is not null)
        {
            foreach (var (clave, valor) in sobrescribir)
            {
                valores[clave] = valor;
            }
        }

        PublicarEnEntorno(valores);

        return new WebApplicationFactory<Program>().WithWebHostBuilder(constructor =>
        {
            constructor.UseEnvironment(entorno);
            ajustar?.Invoke(constructor);
        });
    }

    private static void PublicarEnEntorno(IDictionary<string, string?> valores)
    {
        foreach (var clave in ClavesGestionadas)
        {
            Environment.SetEnvironmentVariable(clave, null);
        }

        foreach (var (clave, valor) in valores)
        {
            Environment.SetEnvironmentVariable(clave.Replace(":", "__", StringComparison.Ordinal), valor);
        }
    }

    private static readonly string[] ClavesGestionadas =
    [
        "ConnectionStrings__DefaultConnection",
        "Jwt__Key",
        "Jwt__Issuer",
        "Jwt__Audience",
        "Jwt__ExpiraMinutos",
        "Seed__AdminEmail",
        "Seed__AdminPassword",
        "Seed__AdminNombre",
        "Cors__OrigenesPermitidos__0",
        "EntraId__TenantId",
        "EntraId__ClientId",
        "EntraId__Audience",
        "EntraId__AuthorityOverride",
        "Identity__Lockout__MaxIntentosFallidos",
        "Espacios__AforoMaximo",
        "Espacios__EtiquetasPorHoja",
        "Espacios__TamanoMaximoImagenBytes",
        "Espacios__MaximoUnidadesPlantilla",
        "Almacen__RutaBase",
        "Identidad__PermitirAutorregistro",
        "Identidad__DominioCorreoInstitucional"
    ];
}
