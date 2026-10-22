using System.Reflection;
using Backend.Application.Interfaces;
using FluentAssertions;

namespace Backend.UnitTests.Seguridad;

/// <summary>
/// Contrato de los puertos de autenticación: ninguna firma pública puede exponer tipos
/// de ASP.NET Core Identity, de EF Core ni del ensamblado de infraestructura.
/// </summary>
public sealed class ContratoPuertosIdentidadTests
{
    [Fact]
    public void IIdentityServiceNoExponeTiposDeIdentityNiDeInfraestructura()
    {
        var tiposExpuestos = typeof(IIdentityService)
            .GetMethods()
            .SelectMany(metodo => metodo.GetParameters().Select(parametro => parametro.ParameterType).Append(metodo.ReturnType))
            .SelectMany(Descomponer)
            .Where(EsTipoProhibido)
            .Select(tipo => tipo.FullName ?? tipo.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        tiposExpuestos.Should().BeEmpty();
    }

    [Fact]
    public void ITokenServiceNoExponeTiposDeIdentityNiDeInfraestructura()
    {
        var tiposExpuestos = typeof(ITokenService)
            .GetMethods()
            .SelectMany(metodo => metodo.GetParameters().Select(parametro => parametro.ParameterType).Append(metodo.ReturnType))
            .SelectMany(Descomponer)
            .Where(EsTipoProhibido)
            .Select(tipo => tipo.FullName ?? tipo.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        tiposExpuestos.Should().BeEmpty();
    }

    private static IEnumerable<Type> Descomponer(Type tipo)
    {
        yield return tipo;

        foreach (var argumento in tipo.IsGenericType ? tipo.GetGenericArguments() : [])
        {
            yield return argumento;
        }
    }

    private static bool EsTipoProhibido(Type tipo) =>
        tipo.Assembly.GetName().Name == "Backend.Infrastructure"
        || (tipo.Namespace?.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) ?? false)
        || (tipo.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ?? false);
}
