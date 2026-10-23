using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;
using FluentAssertions;

namespace Backend.UnitTests.Areas;

/// <summary>
/// Pruebas de las invariantes del agregado <see cref="Espacio"/>: aforo, puestos,
/// unicidad del identificador, baja lógica e inmutabilidad del código QR.
/// </summary>
public sealed class EspacioTests
{
    private static readonly DateTimeOffset Momento = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);
    private const int AforoMaximo = 500;

    [Fact]
    public void CrearAsignaCodigoQrYEstadoActivo()
    {
        var espacio = CrearArea(aforo: 10);

        espacio.Id.Should().NotBeEmpty();
        espacio.Estado.Should().Be(EstadoUnidad.Activo);
        espacio.CodigoQR.Valor.Should().HaveLength(CodigoQR.Longitud);
        espacio.PuestosActivos.Should().Be(0);
        espacio.RowVersion.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CrearRechazaAforoFueraDelRangoMinimo(int aforo)
    {
        var accion = () => CrearArea(aforo: aforo);

        accion.Should().Throw<DomainException>().WithMessage(MensajesEspacio.AforoMinimo);
    }

    [Fact]
    public void CrearRechazaAforoSuperiorAlMaximoInstitucional()
    {
        var accion = () => Espacio.Crear(
            "Auditorio Central",
            "Bloque A",
            CategoriaEspacio.Auditorio,
            AforoMaximo + 1,
            AforoMaximo,
            TipoAprobacion.Administrativo,
            Momento);

        accion.Should().Throw<DomainException>().WithMessage("*500*");
    }

    [Fact]
    public void CrearRechazaNombreYUbicacionVacios()
    {
        var sinNombre = () => Espacio.Crear("  ", "Bloque A", CategoriaEspacio.Aula, 10, AforoMaximo, TipoAprobacion.Automatica, Momento);
        var sinUbicacion = () => Espacio.Crear("Aula 1", "", CategoriaEspacio.Aula, 10, AforoMaximo, TipoAprobacion.Automatica, Momento);

        sinNombre.Should().Throw<DomainException>().WithMessage(MensajesEspacio.NombreRequerido);
        sinUbicacion.Should().Throw<DomainException>().WithMessage(MensajesEspacio.UbicacionRequerida);
    }

    [Fact]
    public void AgregarPuestoRespetaElAforoYAceptaHastaElLimite()
    {
        var espacio = CrearArea(aforo: 2);

        espacio.AgregarPuesto("PC-01", "Equipo de escritorio", Momento);
        espacio.AgregarPuesto("PC-02", null, Momento);

        espacio.PuestosActivos.Should().Be(2);

        var excedido = () => espacio.AgregarPuesto("PC-03", null, Momento);

        excedido.Should().Throw<ConflictoException>().WithMessage("*aforo*");
    }

    [Fact]
    public void AgregarPuestoRechazaIdentificadorDuplicadoSinDistinguirMayusculas()
    {
        var espacio = CrearArea(aforo: 5);
        espacio.AgregarPuesto("PC-01", null, Momento);

        var accion = () => espacio.AgregarPuesto("pc-01", null, Momento);

        accion.Should().Throw<ConflictoException>().WithMessage("*PC-01*");
    }

    [Fact]
    public void CambiarAforoPorDebajoDeLosPuestosActivosEsUnConflicto()
    {
        var espacio = CrearArea(aforo: 12);
        for (var indice = 1; indice <= 10; indice++)
        {
            espacio.AgregarPuesto($"PC-{indice:00}", null, Momento);
        }

        var accion = () => espacio.CambiarAforo(8, AforoMaximo, Momento);

        accion.Should().Throw<ConflictoException>().WithMessage("*10 puestos activos*");
        espacio.Aforo.Should().Be(12);
    }

    [Fact]
    public void CambiarAforoAlNumeroDePuestosActivosEsValido()
    {
        var espacio = CrearArea(aforo: 12);
        for (var indice = 1; indice <= 10; indice++)
        {
            espacio.AgregarPuesto($"PC-{indice:00}", null, Momento);
        }

        espacio.CambiarAforo(10, AforoMaximo, Momento);

        espacio.Aforo.Should().Be(10);
    }

    [Fact]
    public void ReactivarUnPuestoConElAforoCompletoEsUnConflicto()
    {
        var espacio = CrearArea(aforo: 1);
        var puesto = espacio.AgregarPuesto("PC-01", null, Momento);
        espacio.DesactivarPuesto(puesto.Id, Momento);
        espacio.AgregarPuesto("PC-02", null, Momento);

        var accion = () => espacio.ActualizarPuesto(puesto.Id, "PC-01", null, EstadoUnidad.Activo, Momento);

        accion.Should().Throw<ConflictoException>();
    }

    [Fact]
    public void DesactivarConservaElCodigoQrYExcluyeElAreaDelEstadoActivo()
    {
        var espacio = CrearArea();
        var codigo = espacio.CodigoQR;

        espacio.Desactivar(Momento);

        espacio.Estado.Should().Be(EstadoUnidad.Inactivo);
        espacio.CodigoQR.Should().Be(codigo);
        espacio.ActualizadoEn.Should().Be(Momento);
    }

    [Fact]
    public void ActualizarNoPermiteSustituirElCodigoQr()
    {
        var espacio = CrearArea();
        var codigo = espacio.CodigoQR;

        espacio.Actualizar(
            "Laboratorio de Redes II",
            "Bloque B, piso 2",
            CategoriaEspacio.Laboratorio,
            TipoAprobacion.Docente,
            EstadoUnidad.Activo,
            30,
            AforoMaximo,
            Momento);

        espacio.CodigoQR.Should().Be(codigo);
        espacio.Nombre.Should().Be("Laboratorio de Redes II");
        espacio.Aforo.Should().Be(30);
    }

    private static Espacio CrearArea(int aforo = 10) =>
        Espacio.Crear(
            "Laboratorio de Redes",
            "Bloque B",
            CategoriaEspacio.Laboratorio,
            aforo,
            AforoMaximo,
            TipoAprobacion.Docente,
            Momento);
}
