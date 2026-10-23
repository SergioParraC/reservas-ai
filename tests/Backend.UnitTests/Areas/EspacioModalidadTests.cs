using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;

namespace Backend.UnitTests.Areas;

/// <summary>
/// Pruebas de la matriz de modalidad y elemento de préstamo del área.
/// </summary>
public sealed class EspacioModalidadTests
{
    private static readonly DateTimeOffset Momento = new(2026, 7, 1, 8, 0, 0, TimeSpan.FromHours(-5));

    private static Espacio Crear(
        TipoModalidadPrestamo modalidad = TipoModalidadPrestamo.AreaCompleta,
        TipoElementoPrestamo elemento = TipoElementoPrestamo.Auditorio) =>
        Espacio.Crear(
            "Auditorio Principal",
            "Bloque A",
            CategoriaEspacio.Auditorio,
            120,
            500,
            TipoAprobacion.Administrativo,
            Momento,
            tipoModalidadPrestamo: modalidad,
            tipoElementoPrestamo: elemento);

    [Theory]
    [InlineData(TipoModalidadPrestamo.AreaCompleta, TipoElementoPrestamo.Auditorio)]
    [InlineData(TipoModalidadPrestamo.AreaCompleta, TipoElementoPrestamo.LaboratorioEspecializado)]
    [InlineData(TipoModalidadPrestamo.PuestoIndividual, TipoElementoPrestamo.EquipoComputo)]
    [InlineData(TipoModalidadPrestamo.SubZonaCompartida, TipoElementoPrestamo.ZonaDescansoJuegos)]
    public void LasCombinacionesCoherentesSeAceptan(TipoModalidadPrestamo modalidad, TipoElementoPrestamo elemento)
    {
        Assert.True(Espacio.EsCombinacionValida(modalidad, elemento));

        var espacio = Crear(modalidad, elemento);

        Assert.Equal(modalidad, espacio.TipoModalidadPrestamo);
        Assert.Equal(elemento, espacio.TipoElementoPrestamo);
        Assert.Equal(modalidad == TipoModalidadPrestamo.AreaCompleta, espacio.AdmiteReservaDeArea);
    }

    [Fact]
    public void LaCombinacionIncoherenteSeRechaza()
    {
        var excepcion = Assert.Throws<DomainException>(() => Crear(
            TipoModalidadPrestamo.PuestoIndividual,
            TipoElementoPrestamo.Auditorio));

        Assert.Equal(MensajesEspacio.CombinacionPrestamoInvalida, excepcion.Message);
    }

    [Fact]
    public void ElCambioDeModalidadSeDetecta()
    {
        var espacio = Crear(TipoModalidadPrestamo.PuestoIndividual, TipoElementoPrestamo.EquipoComputo);

        Assert.False(espacio.CambiaModalidad(TipoModalidadPrestamo.PuestoIndividual, TipoElementoPrestamo.EquipoComputo));
        Assert.True(espacio.CambiaModalidad(TipoModalidadPrestamo.SubZonaCompartida, TipoElementoPrestamo.ZonaDescansoJuegos));
    }

    [Fact]
    public void LaActualizacionConservaLaModalidadCuandoNoSeIndica()
    {
        var espacio = Crear(TipoModalidadPrestamo.PuestoIndividual, TipoElementoPrestamo.EquipoComputo);

        espacio.Actualizar(
            "Sala de cómputo",
            "Bloque B",
            CategoriaEspacio.SalaComputo,
            TipoAprobacion.Automatica,
            EstadoUnidad.Activo,
            40,
            500,
            Momento);

        Assert.Equal(TipoModalidadPrestamo.PuestoIndividual, espacio.TipoModalidadPrestamo);
        Assert.Equal(TipoElementoPrestamo.EquipoComputo, espacio.TipoElementoPrestamo);
    }
}
