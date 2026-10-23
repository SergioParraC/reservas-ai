using Backend.Domain.Entities;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;

namespace Backend.UnitTests.Calendario;

/// <summary>
/// Pruebas del calendario institucional: expansión de bloqueos, ventana académica y evaluación
/// de motivo con omisión de días completamente cerrados.
/// </summary>
public sealed class CalendarioInstitucionalTests
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly DateTimeOffset Momento = new(2026, 7, 1, 8, 0, 0, TimeSpan.FromHours(-5));

    private static PeriodoAcademico PeriodoActivo()
    {
        var periodo = PeriodoAcademico.Crear(
            "2026-2",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 12, 15),
            Momento);
        periodo.Activar(Momento);

        return periodo;
    }

    private static FranjaNoOperativa Domingo() => FranjaNoOperativa.Crear(
        DayOfWeek.Sunday,
        null,
        new TimeOnly(0, 0),
        new TimeOnly(23, 59),
        esFeriado: false,
        "Cierre dominical",
        Momento);

    private static FranjaHoraria Franja(DateOnly fecha, int horaInicio, int horaFin) =>
        FranjaHoraria.Desde(
            MotorDisponibilidad.InicioEnZona(fecha, new TimeOnly(horaInicio, 0), Zona),
            MotorDisponibilidad.InicioEnZona(fecha, new TimeOnly(horaFin, 0), Zona));

    [Fact]
    public void ElCalendarioSinPeriodoReportaSinPeriodoActivo()
    {
        var calendario = CalendarioInstitucional.Crear(
            null,
            [Domingo()],
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            Zona);

        var evaluacion = calendario.Evaluar(Franja(new DateOnly(2026, 8, 10), 8, 10), Zona);

        Assert.False(calendario.TienePeriodoActivo);
        Assert.Equal(MotivoCalendario.SinPeriodoActivo, evaluacion.Motivo);
    }

    [Fact]
    public void UnaFranjaFueraDeLaVentanaSeReportaFueraDePeriodo()
    {
        var calendario = CalendarioInstitucional.Crear(
            PeriodoActivo(),
            [],
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 12, 31),
            Zona);

        var evaluacion = calendario.Evaluar(Franja(new DateOnly(2026, 12, 20), 8, 10), Zona);

        Assert.Equal(MotivoCalendario.FueraDePeriodo, evaluacion.Motivo);
    }

    [Fact]
    public void UnDomingoCompletoOmiteLaOcurrenciaSinRechazarla()
    {
        var calendario = CalendarioInstitucional.Crear(
            PeriodoActivo(),
            [Domingo()],
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 31),
            Zona);

        var evaluacion = calendario.Evaluar(Franja(new DateOnly(2026, 8, 9), 8, 10), Zona);

        Assert.Equal(MotivoCalendario.NoOperativa, evaluacion.Motivo);
        Assert.True(evaluacion.CubiertaTotalmente);
        Assert.Equal("Cierre dominical", evaluacion.Bloqueo?.Motivo);
    }

    [Fact]
    public void UnBloqueoParcialNoCubreLaFranjaCompleta()
    {
        var sabado = FranjaNoOperativa.Crear(
            DayOfWeek.Saturday,
            null,
            new TimeOnly(13, 0),
            new TimeOnly(21, 0),
            esFeriado: false,
            "Mantenimiento",
            Momento);

        var calendario = CalendarioInstitucional.Crear(
            PeriodoActivo(),
            [sabado],
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            Zona);

        var conflicto = calendario.Evaluar(Franja(new DateOnly(2026, 9, 12), 12, 14), Zona);
        var operativa = calendario.Evaluar(Franja(new DateOnly(2026, 9, 12), 9, 10), Zona);

        Assert.Equal(MotivoCalendario.NoOperativa, conflicto.Motivo);
        Assert.False(conflicto.CubiertaTotalmente);
        Assert.Equal(MotivoCalendario.Ninguno, operativa.Motivo);
    }

    [Fact]
    public void LaExpansionSoloGeneraBloqueosDeLaVentanaConsultada()
    {
        var calendario = CalendarioInstitucional.Crear(
            PeriodoActivo(),
            [Domingo()],
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            Zona);

        Assert.Equal(4, calendario.Bloqueos.Count);
        Assert.All(calendario.Bloqueos, bloqueo => Assert.Equal(DayOfWeek.Sunday, bloqueo.Inicio.DayOfWeek));
        Assert.True(calendario.DiaNoOperativo(new DateOnly(2026, 9, 6), Zona));
        Assert.False(calendario.DiaNoOperativo(new DateOnly(2026, 9, 7), Zona));
    }
}
