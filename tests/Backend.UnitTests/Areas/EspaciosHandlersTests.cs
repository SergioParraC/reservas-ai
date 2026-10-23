using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using FluentAssertions;
using Moq;

namespace Backend.UnitTests.Areas;

/// <summary>
/// Pruebas de los casos de uso de áreas con Moq sobre los puertos: éxito, validación,
/// conflicto de unicidad y de aforo, y recurso inexistente.
/// </summary>
public sealed class EspaciosHandlersTests
{
    private static readonly DateTimeOffset Momento = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IEspaciosRepository> _repositorio = new(MockBehavior.Strict);
    private readonly Mock<IConsultaEspacios> _consulta = new(MockBehavior.Strict);
    private readonly Mock<IRelojSistema> _reloj = new(MockBehavior.Strict);
    private readonly OpcionesEspacios _opciones = new() { AforoMaximo = 500, EtiquetasPorHoja = 18 };

    public EspaciosHandlersTests()
    {
        _reloj.SetupGet(reloj => reloj.Ahora).Returns(Momento);
    }

    [Fact]
    public async Task CrearPersisteElAreaYDevuelveElCodigoQrAsignado()
    {
        ConfigurarCreacion();

        var resultado = await CrearHandler().HandleAsync(new ComandoCrearEspacio(
            "Laboratorio de Redes",
            "Bloque B",
            CategoriaEspacio.Laboratorio,
            25,
            TipoAprobacion.Docente));

        resultado.Exitoso.Should().BeTrue();
        resultado.Valor.Should().NotBeNull();
        resultado.Valor!.CodigoQR.Should().HaveLength(29);
        resultado.Valor.Estado.Should().Be(EstadoUnidad.Activo);
        resultado.Valor.Aforo.Should().Be(25);

        _repositorio.Verify(repositorio => repositorio.AgregarAsync(It.IsAny<Espacio>(), It.IsAny<CancellationToken>()), Times.Once);
        _repositorio.Verify(repositorio => repositorio.GuardarCambiosAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CrearConAforoInvalidoDevuelveValidacionPorCampo()
    {
        var resultado = await CrearHandler().HandleAsync(new ComandoCrearEspacio(
            "Laboratorio",
            "Bloque B",
            CategoriaEspacio.Laboratorio,
            0,
            TipoAprobacion.Docente));

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Validacion);
        resultado.Errores.Should().ContainKey("aforo");
    }

    [Fact]
    public async Task CrearConNombreRepetidoEnLaMismaUbicacionDevuelveConflicto()
    {
        _repositorio.Setup(repositorio => repositorio.ExisteCodigoQrAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositorio.Setup(repositorio => repositorio.ExisteNombreUbicacionAsync(
                "Laboratorio de Redes", "Bloque B", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var resultado = await CrearHandler().HandleAsync(new ComandoCrearEspacio(
            "Laboratorio de Redes",
            "Bloque B",
            CategoriaEspacio.Laboratorio,
            20,
            TipoAprobacion.Docente));

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Conflicto);
        resultado.Errores.Should().ContainKey("nombre");

        _repositorio.Verify(repositorio => repositorio.AgregarAsync(It.IsAny<Espacio>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActualizarConAforoMenorQueLosPuestosActivosDevuelveConflicto()
    {
        var espacio = CrearArea(aforo: 12);
        for (var indice = 1; indice <= 10; indice++)
        {
            espacio.AgregarPuesto($"PC-{indice:00}", null, Momento);
        }

        _repositorio.Setup(repositorio => repositorio.ObtenerAsync(espacio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(espacio);

        var resultado = await new ActualizarEspacioHandler(
                _repositorio.Object,
                Mock.Of<IConsultaOcupacionEspacio>(),
                _opciones,
                _reloj.Object)
            .HandleAsync(espacio.Id, new ComandoActualizarEspacio(
                espacio.Nombre,
                espacio.Ubicacion,
                espacio.Categoria,
                8,
                espacio.TipoAprobacion,
                EstadoUnidad.Activo,
                RowVersion: null));

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Conflicto);
        resultado.Mensaje.Should().Contain("10 puestos activos");
        espacio.Aforo.Should().Be(12);
    }

    [Fact]
    public async Task CrearPuestoQueExcedeElAforoDevuelveConflictoSinPersistir()
    {
        var espacio = CrearArea(aforo: 1);
        espacio.AgregarPuesto("PC-01", null, Momento);

        _repositorio.Setup(repositorio => repositorio.ObtenerAsync(espacio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(espacio);

        var resultado = await new CrearPuestoHandler(_repositorio.Object, _reloj.Object)
            .HandleAsync(espacio.Id, new ComandoCrearPuesto("PC-02", null));

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.Conflicto);
        espacio.PuestosActivos.Should().Be(1);
    }

    [Fact]
    public async Task ObtenerUnAreaInexistenteDevuelveNoEncontrado()
    {
        var identificador = Guid.NewGuid();
        _consulta.Setup(consulta => consulta.ObtenerAdministrativoAsync(identificador, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EspacioDetalle?)null);

        var resultado = await new ObtenerEspacioHandler(_consulta.Object).HandleAsync(identificador);

        resultado.Exitoso.Should().BeFalse();
        resultado.TipoFallo.Should().Be(TipoFallo.NoEncontrado);
    }

    private CrearEspacioHandler CrearHandler() => new(_repositorio.Object, _opciones, _reloj.Object);

    private void ConfigurarCreacion()
    {
        _repositorio.Setup(repositorio => repositorio.ExisteCodigoQrAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositorio.Setup(repositorio => repositorio.ExisteNombreUbicacionAsync(
                It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositorio.Setup(repositorio => repositorio.AgregarAsync(It.IsAny<Espacio>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repositorio.Setup(repositorio => repositorio.GuardarCambiosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private static Espacio CrearArea(int aforo) =>
        Espacio.Crear(
            "Laboratorio de Redes",
            "Bloque B",
            CategoriaEspacio.Laboratorio,
            aforo,
            500,
            TipoAprobacion.Docente,
            Momento);
}
