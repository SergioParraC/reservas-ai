using Backend.Domain.Entities;
using Backend.Domain.Exceptions;

namespace Backend.UnitTests.Calendario;

/// <summary>
/// Pruebas de las franjas no operativas: día completo, fecha específica, sábado parcial,
/// criterios excluyentes y detección de duplicados.
/// </summary>
public sealed class FranjaNoOperativaTests
{
    private static readonly DateTimeOffset Momento = new(2026, 7, 1, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    [Fact]
    public void UnaFranjaDeDiaCompletoCubreTodoElDia()
    {
        var domingo = FranjaNoOperativa.Crear(
            DayOfWeek.Sunday,
            null,
            new TimeOnly(0, 0),
            new TimeOnly(23, 59),
            esFeriado: false,
            "Cierre dominical",
            Momento);

        Assert.True(domingo.EsDiaCompleto);
        Assert.True(domingo.AplicaA(new DateOnly(2026, 8, 9)));

        var intervalo = domingo.Expandir(new DateOnly(2026, 8, 9), Zona);

        Assert.NotNull(intervalo);
        Assert.Equal(new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.FromHours(-5)), intervalo.Inicio);
        Assert.Equal(new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.FromHours(-5)), intervalo.Fin);
    }

    [Fact]
    public void UnaFechaEspecificaNoAplicaAOtrosDias()
    {
        var feriado = FranjaNoOperativa.Crear(
            null,
            new DateOnly(2026, 10, 13),
            new TimeOnly(0, 0),
            new TimeOnly(23, 59),
            esFeriado: true,
            "Día de la raza",
            Momento);

        Assert.True(feriado.AplicaA(new DateOnly(2026, 10, 13)));
        Assert.False(feriado.AplicaA(new DateOnly(2026, 10, 20)));
        Assert.Null(feriado.Expandir(new DateOnly(2026, 10, 20), Zona));
    }

    [Fact]
    public void ElSabadoParcialSoloBloqueaSuRango()
    {
        var sabado = FranjaNoOperativa.Crear(
            DayOfWeek.Saturday,
            null,
            new TimeOnly(13, 0),
            new TimeOnly(21, 0),
            esFeriado: false,
            "Mantenimiento",
            Momento);

        var intervalo = sabado.Expandir(new DateOnly(2026, 9, 12), Zona);

        Assert.NotNull(intervalo);
        Assert.Equal(new TimeOnly(13, 0), TimeOnly.FromDateTime(intervalo.Inicio.DateTime));
        Assert.Equal(new TimeOnly(21, 0), TimeOnly.FromDateTime(intervalo.Fin.DateTime));
        Assert.False(sabado.EsDiaCompleto);
    }

    [Fact]
    public void LosCriteriosSonExcluyentes()
    {
        Assert.Throws<DomainException>(() => FranjaNoOperativa.Crear(
            DayOfWeek.Monday,
            new DateOnly(2026, 9, 7),
            new TimeOnly(8, 0),
            new TimeOnly(9, 0),
            esFeriado: false,
            "Motivo",
            Momento));

        Assert.Throws<DomainException>(() => FranjaNoOperativa.Crear(
            null,
            null,
            new TimeOnly(8, 0),
            new TimeOnly(9, 0),
            esFeriado: false,
            "Motivo",
            Momento));
    }

    [Fact]
    public void LaDetencionDeDuplicadosComparaCriterioYHorario()
    {
        var franja = FranjaNoOperativa.Crear(
            DayOfWeek.Saturday,
            null,
            new TimeOnly(13, 0),
            new TimeOnly(21, 0),
            esFeriado: false,
            "Mantenimiento",
            Momento);

        Assert.True(franja.EsEquivalenteA(DayOfWeek.Saturday, null, new TimeOnly(13, 0), new TimeOnly(21, 0)));
        Assert.False(franja.EsEquivalenteA(DayOfWeek.Sunday, null, new TimeOnly(13, 0), new TimeOnly(21, 0)));
        Assert.False(franja.EsEquivalenteA(DayOfWeek.Saturday, null, new TimeOnly(14, 0), new TimeOnly(21, 0)));
    }

    [Fact]
    public void LaBajaLogicaImpideQueLaFranjaSigaAplicando()
    {
        var franja = FranjaNoOperativa.Crear(
            DayOfWeek.Sunday,
            null,
            new TimeOnly(0, 0),
            new TimeOnly(23, 59),
            esFeriado: false,
            "Cierre dominical",
            Momento);

        franja.Desactivar();

        Assert.False(franja.Activa);
        Assert.False(franja.AplicaA(new DateOnly(2026, 8, 9)));
    }
}
