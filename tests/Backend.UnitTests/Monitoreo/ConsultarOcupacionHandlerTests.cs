using Backend.Application.Interfaces;
using Backend.Application.Modules.Monitoreo;
using Backend.Application.Modules.Monitoreo.Dtos;
using Backend.Domain.Enums;
using Backend.UnitTests.Asistencia;

namespace Backend.UnitTests.Monitoreo;

/// <summary>
/// Pruebas de la agregación del mapa de ocupación: uso vigente, sesiones sin cerrar, pendientes de
/// check-in, reservas futuras y áreas sin puestos activos.
/// </summary>
public sealed class ConsultarOcupacionHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 3, 10, 30, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Area = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid AreaVacia = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private sealed class ConsultaFalsa : IConsultaOcupacionActual
    {
        private readonly OcupacionInstantanea _instantanea;

        public ConsultaFalsa(OcupacionInstantanea instantanea) => _instantanea = instantanea;

        public Task<OcupacionInstantanea> ObtenerAsync(
            DateTimeOffset ahora,
            TimeSpan tolerancia,
            CancellationToken cancellationToken = default) => Task.FromResult(_instantanea);
    }

    private static ConsultarOcupacionHandler Handler(
        OcupacionInstantanea instantanea,
        int tolerancia = 15) =>
        new(
            new ConsultaFalsa(instantanea),
            SoporteAsistencia.Opciones(tolerancia),
            SoporteAsistencia.Reloj(Ahora).Object);

    private static OcupacionInstantanea Instantanea(
        IReadOnlyList<AreaOcupable> areas,
        IReadOnlyList<PuestoOcupable> puestos,
        IReadOnlyList<ReservaOcupacionActual> reservas) => new(areas, puestos, reservas);

    private static AreaOcupable AreaConAforo(Guid id, string nombre, int aforo = 20) => new(id, nombre, aforo);

    private static List<PuestoOcupable> Puestos(Guid areaId, int cantidad) =>
        Enumerable.Range(1, cantidad)
            .Select(indice => new PuestoOcupable(areaId, Guid.NewGuid(), $"PC-{indice:00}"))
            .ToList();

    private static ReservaOcupacionActual EnUso(PuestoOcupable puesto, int inicioMinutos, int finMinutos) =>
        new(
            Guid.NewGuid(),
            puesto.EspacioId,
            puesto.PuestoId,
            EstadoReserva.EnUso,
            Ahora.AddMinutes(inicioMinutos),
            Ahora.AddMinutes(finMinutos),
            Ahora.AddMinutes(inicioMinutos + 2),
            "Ana Titular");

    private static ReservaOcupacionActual Confirmada(PuestoOcupable puesto, int inicioMinutos) =>
        new(
            Guid.NewGuid(),
            puesto.EspacioId,
            puesto.PuestoId,
            EstadoReserva.Confirmada,
            Ahora.AddMinutes(inicioMinutos),
            Ahora.AddMinutes(inicioMinutos + 60),
            null,
            "Luis Titular");

    [Fact]
    public async Task ElMapaCuentaElUsoVigenteYLosPendientesDeCheckIn()
    {
        var puestos = Puestos(Area, 20);
        var reservas = puestos.Take(12)
            .Select(puesto => EnUso(puesto, -10, 50))
            .Concat(puestos.Skip(12).Take(3).Select(puesto => Confirmada(puesto, 10)))
            .ToList();

        var resultado = await Handler(Instantanea([AreaConAforo(Area, "Laboratorio A")], puestos, reservas)).HandleAsync();

        Assert.True(resultado.Exitoso);
        var mapa = resultado.Valor!;

        Assert.Equal(Ahora, mapa.GeneradoEn);
        Assert.Equal(15, mapa.ToleranciaMinutos);

        var area = Assert.Single(mapa.Areas);
        Assert.Equal(20, area.Aforo);
        Assert.Equal(20, area.PuestosActivos);
        Assert.Equal(12, area.EnUso);
        Assert.Equal(3, area.PendientesCheckIn);
        Assert.Equal(0, area.SesionesSinCerrar);
        Assert.Equal(5, area.Libres);
        Assert.Equal(60m, area.PorcentajeOcupacion);
        Assert.Equal(Ahora.AddMinutes(25), area.ProximaLiberacion);
        Assert.Equal(12, area.EnCurso.Count);
        Assert.All(area.EnCurso, sesion => Assert.Equal("Ana Titular", sesion.Titular));

        Assert.Equal(20, mapa.Totales.PuestosActivos);
        Assert.Equal(12, mapa.Totales.EnUso);
        Assert.Equal(3, mapa.Totales.PendientesCheckIn);
        Assert.Equal(5, mapa.Totales.Libres);
        Assert.Equal(60m, mapa.Totales.PorcentajeOcupacion);
    }

    [Fact]
    public async Task UnaSesionQueYaPasoSuFinSeInformaSinContarseComoOcupada()
    {
        var puestos = Puestos(Area, 20);
        var reservas = new[] { EnUso(puestos[0], -90, -30) };

        var resultado = await Handler(Instantanea([AreaConAforo(Area, "Laboratorio A")], puestos, reservas)).HandleAsync();

        var area = Assert.Single(resultado.Valor!.Areas);

        Assert.Equal(0, area.EnUso);
        Assert.Equal(1, area.SesionesSinCerrar);
        Assert.Equal(20, area.Libres);
        Assert.Empty(area.EnCurso);
        Assert.Equal(0m, area.PorcentajeOcupacion);
        Assert.Null(area.ProximaLiberacion);
    }

    [Fact]
    public async Task LasReservasFuturasNoCuentanComoOcupacion()
    {
        var puestos = Puestos(Area, 20);
        var reservas = new[] { Confirmada(puestos[0], 240) };

        var resultado = await Handler(Instantanea([AreaConAforo(Area, "Laboratorio A")], puestos, reservas)).HandleAsync();

        var area = Assert.Single(resultado.Valor!.Areas);

        Assert.Equal(0, area.EnUso);
        Assert.Equal(0, area.PendientesCheckIn);
        Assert.Equal(20, area.Libres);
        Assert.Null(area.ProximaLiberacion);
        Assert.Equal(0m, area.PorcentajeOcupacion);
    }

    [Fact]
    public async Task UnAreaSinPuestosActivosApareceConCeros()
    {
        var resultado = await Handler(Instantanea([AreaConAforo(AreaVacia, "Depósito", 30)], [], [])).HandleAsync();

        var area = Assert.Single(resultado.Valor!.Areas);

        Assert.Equal("Depósito", area.Nombre);
        Assert.Equal(30, area.Aforo);
        Assert.Equal(0, area.PuestosActivos);
        Assert.Equal(0, area.EnUso);
        Assert.Equal(0, area.PendientesCheckIn);
        Assert.Equal(0, area.SesionesSinCerrar);
        Assert.Equal(0, area.Libres);
        Assert.Equal(0m, area.PorcentajeOcupacion);
        Assert.Empty(area.EnCurso);
        Assert.Null(area.ProximaLiberacion);
    }

    [Fact]
    public async Task LasAreasSeOrdenanPorNombreYLosTotalesSumanSuDesglose()
    {
        var puestosA = Puestos(Area, 2);
        var puestosB = Puestos(AreaVacia, 2);
        var instantanea = Instantanea(
            [AreaConAforo(Area, "Zona B", 10), AreaConAforo(AreaVacia, "Zona A", 5)],
            [.. puestosA, .. puestosB],
            [EnUso(puestosA[0], -5, 30), Confirmada(puestosB[0], 5)]);

        var resultado = await Handler(instantanea).HandleAsync();

        var areas = resultado.Valor!.Areas;
        Assert.Equal("Zona A", areas[0].Nombre);
        Assert.Equal("Zona B", areas[1].Nombre);
        Assert.Equal(15, resultado.Valor.Totales.Aforo);
        Assert.Equal(4, resultado.Valor.Totales.PuestosActivos);
        Assert.Equal(1, resultado.Valor.Totales.EnUso);
        Assert.Equal(1, resultado.Valor.Totales.PendientesCheckIn);
        Assert.Equal(2, resultado.Valor.Totales.Libres);
    }
}
