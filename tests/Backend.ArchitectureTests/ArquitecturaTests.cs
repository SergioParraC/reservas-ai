using System.Reflection;
using System.Runtime.CompilerServices;
using NetArchTest.Rules;

namespace Backend.ArchitectureTests;

/// <summary>
/// Impone la regla de dependencias de Clean Architecture y la pureza del núcleo.
/// Se ejecuta junto con las pruebas unitarias en el pipeline de integración continua.
/// </summary>
public sealed class ArquitecturaTests
{
    private const string PrefijoAplicacion = "Backend.Application";
    private const string PrefijoInfraestructura = "Backend.Infrastructure";
    private const string PrefijoPresentacion = "Backend.Presentation";

    private static readonly Assembly EnsambladoDominio = Assembly.Load("Backend.Domain");
    private static readonly Assembly EnsambladoAplicacion = Assembly.Load(PrefijoAplicacion);
    private static readonly Assembly EnsambladoInfraestructura = Assembly.Load(PrefijoInfraestructura);
    private static readonly Assembly EnsambladoPresentacion = Assembly.Load("Backend.Presentation.Api");

    private static readonly string[] TecnologiasProhibidasEnDominio =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions",
        "Microsoft.IdentityModel",
        "Microsoft.Data",
        "System.Data",
        "Newtonsoft.Json",
        "Dapper"
    ];

    [Fact]
    public void LasCapasSoloDependenDeCapasInteriores()
    {
        var reglas = new (Assembly Ensamblado, string[] CapasProhibidas)[]
        {
            (EnsambladoDominio, [PrefijoAplicacion, PrefijoInfraestructura, PrefijoPresentacion]),
            (EnsambladoAplicacion, [PrefijoInfraestructura, PrefijoPresentacion]),
            (EnsambladoInfraestructura, [PrefijoPresentacion])
        };

        var violaciones = new List<string>();

        foreach (var (ensamblado, capasProhibidas) in reglas)
        {
            var resultado = Types.InAssembly(ensamblado)
                .ShouldNot()
                .HaveDependencyOnAny(capasProhibidas)
                .GetResult();

            if (!resultado.IsSuccessful)
            {
                var tipos = string.Join(", ", resultado.FailingTypeNames ?? []);
                violaciones.Add(
                    $"{ensamblado.GetName().Name} declara dependencias prohibidas hacia " +
                    $"{string.Join(", ", capasProhibidas)} en: {tipos}");
            }
        }

        Assert.True(violaciones.Count == 0, string.Join(Environment.NewLine, violaciones));
    }

    [Fact]
    public void ElDominioNoDependeDePaquetesTecnologicos()
    {
        var resultado = Types.InAssembly(EnsambladoDominio)
            .ShouldNot()
            .HaveDependencyOnAny(TecnologiasProhibidasEnDominio)
            .GetResult();

        var detalle = string.Join(", ", resultado.FailingTypeNames ?? []);

        Assert.True(
            resultado.IsSuccessful,
            $"El dominio debe permanecer en C# puro, sin ORM, framework web, identidad ni utilidades de terceros. Tipos en falta: {detalle}");
    }

    [Fact]
    public void LaAplicacionNoDependeDeIdentityNiDeEntityFramework()
    {
        var reglas = Types.InAssembly(EnsambladoAplicacion)
            .ShouldNot()
            .HaveDependencyOnAny([PrefijoInfraestructura, PrefijoPresentacion, "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions.Identity"])
            .GetResult();

        var violaciones = TiposEnFirmasPublicas(EnsambladoAplicacion, EsTipoTecnologicoDeIdentity);

        Assert.True(
            reglas.IsSuccessful && violaciones.Count == 0,
            "La capa de aplicación solo puede depender del dominio y de sus propios contratos. " +
            $"Dependencias prohibidas: [{string.Join(", ", reglas.FailingTypeNames ?? [])}]; " +
            $"tipos de Identity o EF en firmas públicas: [{string.Join(", ", violaciones)}]");
    }

    [Fact]
    public void LasFirmasPublicasDePresentacionNoExponenTiposDeInfraestructura()
    {
        var violaciones = TiposEnFirmasPublicas(EnsambladoPresentacion, EsTipoDeInfraestructura);

        Assert.True(
            violaciones.Count == 0,
            $"Las firmas públicas de la API solo pueden usar DTOs de la capa de aplicación. Tipos de infraestructura expuestos: {string.Join(", ", violaciones)}");
    }

    private static List<string> TiposEnFirmasPublicas(Assembly ensamblado, Func<Type, bool> esProhibido) =>
        ensamblado.GetTypes()
            .Where(tipo => tipo.IsPublic || tipo.IsNestedPublic)
            .Where(tipo => !tipo.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .SelectMany(tipo => tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(FirmasDelMetodo)
            .Where(esProhibido)
            .Select(tipo => tipo.FullName ?? tipo.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static bool EsTipoTecnologicoDeIdentity(Type tipo) =>
        tipo.Assembly == EnsambladoInfraestructura
        || (tipo.Namespace?.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) ?? false)
        || (tipo.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ?? false)
        || (tipo.Namespace?.StartsWith("Microsoft.Extensions.Identity", StringComparison.Ordinal) ?? false);

    private static IEnumerable<Type> FirmasDelMetodo(MethodInfo metodo)
    {
        foreach (var tipo in Descomponer(metodo.ReturnType))
        {
            yield return tipo;
        }

        foreach (var parametro in metodo.GetParameters())
        {
            foreach (var tipo in Descomponer(parametro.ParameterType))
            {
                yield return tipo;
            }
        }
    }

    private static IEnumerable<Type> Descomponer(Type tipo)
    {
        if (tipo.HasElementType && tipo.GetElementType() is { } elemento)
        {
            foreach (var compuesto in Descomponer(elemento))
            {
                yield return compuesto;
            }

            yield break;
        }

        yield return tipo;

        if (!tipo.IsGenericType)
        {
            yield break;
        }

        foreach (var argumento in tipo.GetGenericArguments())
        {
            foreach (var compuesto in Descomponer(argumento))
            {
                yield return compuesto;
            }
        }
    }

    private static bool EsTipoDeInfraestructura(Type tipo) =>
        tipo.Assembly == EnsambladoInfraestructura
        || (tipo.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ?? false);
}
