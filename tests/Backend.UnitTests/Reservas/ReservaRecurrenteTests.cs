using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas de la generación de ocurrencias de la serie: semana con feriado, bisemanal, fechas
/// fijas, serie completamente cerrada y ocurrencia parcialmente cubierta.
/// </summary>
public sealed class ReservaRecurrenteTests
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly DateTimeOffset Ahora = new(2026, 7, 1, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly PeriodoSemestral Periodo = PeriodoSemestral.Crear(
        new DateOnly(2026, 8, 4),
        new DateOnly(2026, 11, 24));

    private static readonly Guid EspacioId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DocenteId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static PeriodoAcademico PeriodoAcademicoActivo()
    {
        var periodo = PeriodoAcademico.Crear(
            "2026-2",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 12, 15),
            Ahora);
        periodo.Activar(Ahora);

        return periodo;
    }

    private static FranjaNoOperativa Feriado(DateOnly fecha) => FranjaNoOperativa.Crear(
        null,
        fecha,
        new TimeOnly(0, 0),
        new TimeOnly(23, 59),
        esFeriado: true,
        "Feriado institucional",
        Ahora);

    private static FranjaNoOperativa Domingo() => FranjaNoOperativa.Crear(
        DayOfWeek.Sunday,
        null,
        new TimeOnly(0, 0),
        new TimeOnly(23, 59),
        esFeriado: false,
        "Cierre dominical",
        Ahora);

    private static ReservaRecurrente Serie(
        TipoRecurrencia tipo,
        DayOfWeek? dia,
        IEnumerable<DateOnly>? fechas,
        TimeOnly? inicio = null,
        TimeOnly? fin = null)
    {
        var patron = PatronRecurrencia.Crear(
            tipo,
            dia,
            fechas,
            inicio ?? new TimeOnly(14, 0),
            fin ?? new TimeOnly(16, 0));

        return ReservaRecurrente.Crear(EspacioId, DocenteId, patron, Periodo, "Cálculo II", Ahora);
    }

    private static CalendarioInstitucional Calendario(params FranjaNoOperativa[] franjas) =>
        CalendarioInstitucional.Crear(
            PeriodoAcademicoActivo(),
            franjas,
            Periodo.Inicio,
            Periodo.Fin,
            Zona);

    private static OcurrenciasGeneradas Generar(ReservaRecurrente serie, CalendarioInstitucional calendario) =>
        serie.GenerarOcurrencias(calendario, Zona, 20, Ahora, TimeSpan.FromDays(3));

    [Fact]
    public void LaSerieSemanalOmiteElFeriadoYConservaDieciseisOcurrencias()
    {
        var serie = Serie(TipoRecurrencia.Semanal, DayOfWeek.Tuesday, null);
        var generadas = Generar(serie, Calendario(Feriado(new DateOnly(2026, 10, 13))));

        Assert.Equal(16, generadas.Ocurrencias.Count);
        Assert.Single(generadas.Omitidas);
        Assert.DoesNotContain(
            generadas.Ocurrencias,
            franja => franja.Inicio.Date == new DateTime(2026, 10, 13));
    }

    [Fact]
    public void LaSerieBisemanalOmiteElFeriadoYConservaOchoOcurrencias()
    {
        var serie = Serie(TipoRecurrencia.Bisemanal, DayOfWeek.Tuesday, null);
        var generadas = Generar(serie, Calendario(Feriado(new DateOnly(2026, 10, 13))));

        Assert.Equal(8, generadas.Ocurrencias.Count);
        Assert.Equal(14, (generadas.Ocurrencias[1].Inicio - generadas.Ocurrencias[0].Inicio).Days);
    }

    [Fact]
    public void LaSerieDeFechasFijasGeneraExactamenteTresOcurrencias()
    {
        var serie = Serie(
            TipoRecurrencia.FechasFijas,
            null,
            [new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 19), new DateOnly(2026, 11, 16)]);

        var generadas = Generar(serie, Calendario());

        Assert.Equal(3, generadas.Ocurrencias.Count);
    }

    [Fact]
    public void UnaSerieCompletamenteEnDomingoNoGeneraOcurrencias()
    {
        var serie = Serie(TipoRecurrencia.Semanal, DayOfWeek.Sunday, null);

        var excepcion = Assert.Throws<DomainException>(() => Generar(serie, Calendario(Domingo())));

        Assert.Equal(MensajesSemestre.SerieSinOcurrencias, excepcion.Message);
    }

    [Fact]
    public void UnaOcurrenciaParcialmenteCubiertaSeRechazaConLaFecha()
    {
        var sabado = FranjaNoOperativa.Crear(
            DayOfWeek.Saturday,
            null,
            new TimeOnly(13, 0),
            new TimeOnly(21, 0),
            esFeriado: false,
            "Mantenimiento",
            Ahora);

        var serie = Serie(
            TipoRecurrencia.Semanal,
            DayOfWeek.Saturday,
            null,
            new TimeOnly(12, 0),
            new TimeOnly(14, 0));

        var excepcion = Assert.Throws<ConflictoException>(() => Generar(serie, Calendario(sabado)));

        Assert.Contains("2026-08-08", excepcion.Message, StringComparison.Ordinal);
        Assert.Contains("Mantenimiento", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaSerieFueraDelPeriodoAcademicoSeRechaza()
    {
        var serie = Serie(TipoRecurrencia.Semanal, DayOfWeek.Tuesday, null);
        var periodo = PeriodoAcademico.Crear("2026-1", new DateOnly(2026, 2, 1), new DateOnly(2026, 6, 15), Ahora);
        periodo.Activar(Ahora);

        var calendario = CalendarioInstitucional.Crear(periodo, [], Periodo.Inicio, Periodo.Fin, Zona);

        var excepcion = Assert.Throws<ConflictoException>(() => Generar(serie, calendario));

        Assert.Equal(MensajesCalendario.FueraDePeriodoAcademico, excepcion.Message);
    }

    [Fact]
    public void ElPeriodoSemestralNoPuedeSuperarElMaximoDeSemanas()
    {
        var periodo = PeriodoSemestral.Crear(new DateOnly(2026, 1, 5), new DateOnly(2026, 7, 5));

        var excepcion = Assert.Throws<DomainException>(() => ReservaRecurrente.ValidarSemanas(periodo, 20));

        Assert.Contains("20", excepcion.Message, StringComparison.Ordinal);
    }
}
