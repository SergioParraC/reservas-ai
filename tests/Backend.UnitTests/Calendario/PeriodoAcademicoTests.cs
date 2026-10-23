using Backend.Domain.Entities;
using Backend.Domain.Exceptions;

namespace Backend.UnitTests.Calendario;

/// <summary>
/// Pruebas del periodo académico: ventana activa, solapes, fechas invertidas y activación
/// con un único periodo vigente.
/// </summary>
public sealed class PeriodoAcademicoTests
{
    private static readonly DateTimeOffset Momento = new(2026, 7, 1, 8, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void UnPeriodoNuevoNaceInactivo()
    {
        var periodo = PeriodoAcademico.Crear("2026-2", new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 15), Momento);

        Assert.False(periodo.EstadoActivo);
        Assert.Equal(new DateOnly(2026, 8, 1), periodo.FechaInicio);
        Assert.Equal(new DateOnly(2026, 12, 15), periodo.FechaFin);
    }

    [Fact]
    public void UnPeriodoConFechasInvertidasSeRechaza()
    {
        var excepcion = Assert.Throws<DomainException>(() => PeriodoAcademico.Crear(
            "2026-2",
            new DateOnly(2026, 12, 15),
            new DateOnly(2026, 8, 1),
            Momento));

        Assert.Equal(MensajesCalendario.PeriodoFechasInvertidas, excepcion.Message);
    }

    [Fact]
    public void LaVentanaIncluyeAmbosExtremos()
    {
        var periodo = PeriodoAcademico.Crear("2026-2", new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 15), Momento);

        Assert.True(periodo.Contiene(new DateOnly(2026, 8, 1)));
        Assert.True(periodo.Contiene(new DateOnly(2026, 12, 15)));
        Assert.False(periodo.Contiene(new DateOnly(2026, 12, 16)));
        Assert.True(periodo.ContieneVentana(new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 30)));
        Assert.False(periodo.ContieneVentana(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public void LosPeriodosSolapadosSeDetectanConLosExtremosInclusivos()
    {
        var periodo = PeriodoAcademico.Crear("2026-1", new DateOnly(2026, 2, 1), new DateOnly(2026, 6, 15), Momento);

        Assert.True(periodo.SeSolapaCon(new DateOnly(2026, 5, 1), new DateOnly(2026, 9, 30)));
        Assert.True(periodo.SeSolapaCon(new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 20)));
        Assert.False(periodo.SeSolapaCon(new DateOnly(2026, 6, 16), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void LaActivacionYDesactivacionCambianElEstado()
    {
        var periodo = PeriodoAcademico.Crear("2026-2", new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 15), Momento);

        periodo.Activar(Momento);
        Assert.True(periodo.EstadoActivo);

        periodo.Desactivar(Momento);
        Assert.False(periodo.EstadoActivo);
    }
}
