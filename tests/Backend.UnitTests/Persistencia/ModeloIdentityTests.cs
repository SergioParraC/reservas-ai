using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Backend.Infrastructure.Persistence.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Backend.UnitTests.Persistencia;

/// <summary>
/// Pruebas del modelo de EF Core que no requieren servidor: validan la configuración
/// Fluent API de identidad sin abrir conexión a la base de datos.
/// </summary>
public sealed class ModeloIdentityTests
{
    private const string CadenaSinServidor =
        "Server=(localdb)\\mssqllocaldb;Database=ModeloIdentidad;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public void ElModeloMapeaLaTablaAspNetUsersConLongitudesYConversionDeTipoActor()
    {
        using var contexto = CrearContexto();
        var entidad = contexto.Model.FindEntityType(typeof(Usuario));

        entidad.Should().NotBeNull();
        entidad!.GetTableName().Should().Be("AspNetUsers");
        entidad.FindProperty(nameof(Usuario.NombreCompleto))!.GetMaxLength()
            .Should().Be(UsuarioConfiguration.LongitudMaximaNombreCompleto);
        entidad.FindProperty(nameof(Usuario.IdentificadorFederado))!.GetMaxLength()
            .Should().Be(UsuarioConfiguration.LongitudMaximaIdentificadorFederado);

        var tipoActor = entidad.FindProperty(nameof(Usuario.TipoActor))!;
        tipoActor.GetProviderClrType().Should().Be(typeof(int));
        tipoActor.IsNullable.Should().BeFalse();

        entidad.FindProperty(nameof(Usuario.Activo)).Should().NotBeNull();
        entidad.FindProperty(nameof(Usuario.CreadoEn)).Should().NotBeNull();
        entidad.FindProperty(nameof(Usuario.UltimoAccesoEn)).Should().NotBeNull();
    }

    [Fact]
    public void ElModeloDeclaraIndiceUnicoFiltradoSobreElIdentificadorFederado()
    {
        using var contexto = CrearContexto();
        var entidad = contexto.Model.FindEntityType(typeof(Usuario))!;

        var indice = entidad.GetIndexes().Single(indiceModelo =>
            indiceModelo.Properties.Count == 1
            && indiceModelo.Properties[0].Name == nameof(Usuario.IdentificadorFederado));

        indice.IsUnique.Should().BeTrue();
        indice.GetDatabaseName().Should().Be(UsuarioConfiguration.NombreIndiceIdentificadorFederado);
        indice.GetFilter().Should().Be("[IdentificadorFederado] IS NOT NULL");
    }

    [Fact]
    public void ElModeloMapeaLaTablaAspNetRolesConNombreObligatorio()
    {
        using var contexto = CrearContexto();
        var entidad = contexto.Model.FindEntityType(typeof(Rol));

        entidad.Should().NotBeNull();
        entidad!.GetTableName().Should().Be("AspNetRoles");
        entidad.FindProperty(nameof(Rol.Name))!.GetMaxLength().Should().Be(RolConfiguration.LongitudMaximaNombre);
        entidad.FindProperty(nameof(Rol.Name))!.IsNullable.Should().BeFalse();
    }

    private static ApplicationDbContext CrearContexto()
    {
        var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(CadenaSinServidor)
            .Options;

        return new ApplicationDbContext(opciones);
    }
}
