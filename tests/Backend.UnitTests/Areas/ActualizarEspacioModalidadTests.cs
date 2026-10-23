using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Moq;

namespace Backend.UnitTests.Areas;

/// <summary>
/// Pruebas del cambio de modalidad del área: bloqueo con ocupación futura y actualización
/// permitida cuando la agenda está libre.
/// </summary>
public sealed class ActualizarEspacioModalidadTests
{
    private static readonly DateTimeOffset Momento = new(2026, 7, 1, 8, 0, 0, TimeSpan.FromHours(-5));

    private static Espacio Area() => Espacio.Crear(
        "Sala de cómputo",
        "Bloque C",
        CategoriaEspacio.SalaComputo,
        40,
        500,
        TipoAprobacion.Automatica,
        Momento,
        tipoModalidadPrestamo: TipoModalidadPrestamo.PuestoIndividual,
        tipoElementoPrestamo: TipoElementoPrestamo.EquipoComputo);

    private static ActualizarEspacioHandler Handler(Espacio espacio, bool conOcupacion)
    {
        var repositorio = new Mock<IEspaciosRepository>();
        repositorio.Setup(servicio => servicio.ObtenerAsync(espacio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(espacio);
        repositorio.Setup(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repositorio.Setup(servicio => servicio.ExisteNombreUbicacionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var ocupacion = new Mock<IConsultaOcupacionEspacio>();
        ocupacion.Setup(servicio => servicio.HayOcupacionFuturaAsync(
                espacio.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conOcupacion);

        var reloj = new Mock<IRelojSistema>();
        reloj.SetupGet(servicio => servicio.Ahora).Returns(Momento);

        return new ActualizarEspacioHandler(
            repositorio.Object,
            ocupacion.Object,
            new OpcionesEspacios { AforoMaximo = 500 },
            reloj.Object);
    }

    private static ComandoActualizarEspacio Comando(Espacio espacio) => new(
        espacio.Nombre,
        espacio.Ubicacion,
        espacio.Categoria,
        espacio.Aforo,
        espacio.TipoAprobacion,
        EstadoUnidad.Activo,
        RowVersion: null,
        TipoModalidadPrestamo.SubZonaCompartida,
        TipoElementoPrestamo.ZonaDescansoJuegos);

    [Fact]
    public async Task ElCambioDeModalidadConOcupacionFuturaDevuelve409()
    {
        var espacio = Area();
        var resultado = await Handler(espacio, conOcupacion: true).HandleAsync(espacio.Id, Comando(espacio));

        Assert.False(resultado.Exitoso);
        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesEspacio.ModalidadConOcupacion, resultado.Mensaje);
        Assert.Equal(TipoModalidadPrestamo.PuestoIndividual, espacio.TipoModalidadPrestamo);
    }

    [Fact]
    public async Task ElCambioDeModalidadSinOcupacionSeAplica()
    {
        var espacio = Area();
        var resultado = await Handler(espacio, conOcupacion: false).HandleAsync(espacio.Id, Comando(espacio));

        Assert.True(resultado.Exitoso);
        Assert.Equal(TipoModalidadPrestamo.SubZonaCompartida, espacio.TipoModalidadPrestamo);
        Assert.Equal(TipoElementoPrestamo.ZonaDescansoJuegos, espacio.TipoElementoPrestamo);
    }
}
