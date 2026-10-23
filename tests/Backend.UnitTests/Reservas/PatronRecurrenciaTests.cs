using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas de la generación de fechas del patrón: semanal, bisemanal, fechas fijas y rechazo
/// de fechas fuera del periodo semestral.
/// </summary>
public sealed class PatronRecurrenciaTests
{
    private static readonly PeriodoSemestral Periodo = PeriodoSemestral.Crear(
        new DateOnly(2026, 8, 4),
        new DateOnly(2026, 11, 24));

    private static PatronRecurrencia Semanal() => PatronRecurrencia.Crear(
        TipoRecurrencia.Semanal,
        DayOfWeek.Tuesday,
        null,
        new TimeOnly(14, 0),
        new TimeOnly(16, 0));

    [Fact]
    public void ElPatronSemanalGeneraUnaFechaPorSemana()
    {
        var fechas = Semanal().GenerarFechas(Periodo);

        Assert.Equal(17, fechas.Count);
        Assert.All(fechas, fecha => Assert.Equal(DayOfWeek.Tuesday, fecha.DayOfWeek));
        Assert.Equal(new DateOnly(2026, 8, 4), fechas[0]);
        Assert.Equal(new DateOnly(2026, 11, 24), fechas[^1]);
    }

    [Fact]
    public void ElPatronBisemanalGeneraUnaFechaCadaCatorceDias()
    {
        var patron = PatronRecurrencia.Crear(
            TipoRecurrencia.Bisemanal,
            DayOfWeek.Tuesday,
            null,
            new TimeOnly(14, 0),
            new TimeOnly(16, 0));

        var fechas = patron.GenerarFechas(Periodo);

        Assert.Equal(9, fechas.Count);
        Assert.Equal(new DateOnly(2026, 8, 18), fechas[1]);
        Assert.Equal(14, fechas[1].DayNumber - fechas[0].DayNumber);
    }

    [Fact]
    public void ElPatronDeFechasFijasRespetaExactamenteLasFechasIndicadas()
    {
        var patron = PatronRecurrencia.Crear(
            TipoRecurrencia.FechasFijas,
            null,
            [new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 19), new DateOnly(2026, 11, 16)],
            new TimeOnly(14, 0),
            new TimeOnly(16, 0));

        var fechas = patron.GenerarFechas(Periodo);

        Assert.Equal(3, fechas.Count);
        Assert.Equal([new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 19), new DateOnly(2026, 11, 16)], fechas);
    }

    [Fact]
    public void UnaFechaFijaFueraDelPeriodoSeRechaza()
    {
        var patron = PatronRecurrencia.Crear(
            TipoRecurrencia.FechasFijas,
            null,
            [new DateOnly(2027, 2, 15)],
            new TimeOnly(14, 0),
            new TimeOnly(16, 0));

        var excepcion = Assert.Throws<DomainException>(() => patron.GenerarFechas(Periodo));

        Assert.Contains("2027-02-15", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ElPatronRecurrenteRequiereDiaDeLaSemana()
    {
        var excepcion = Assert.Throws<DomainException>(() => PatronRecurrencia.Crear(
            TipoRecurrencia.Semanal,
            null,
            null,
            new TimeOnly(14, 0),
            new TimeOnly(16, 0)));

        Assert.Equal(MensajesSemestre.DiaSemanaRequerido, excepcion.Message);
    }

    [Fact]
    public void ElPatronDeFechasFijasNoAdmiteDiaDeLaSemana()
    {
        var excepcion = Assert.Throws<DomainException>(() => PatronRecurrencia.Crear(
            TipoRecurrencia.FechasFijas,
            DayOfWeek.Monday,
            [new DateOnly(2026, 9, 7)],
            new TimeOnly(14, 0),
            new TimeOnly(16, 0)));

        Assert.Equal(MensajesSemestre.FechasFijasSinDia, excepcion.Message);
    }
}
