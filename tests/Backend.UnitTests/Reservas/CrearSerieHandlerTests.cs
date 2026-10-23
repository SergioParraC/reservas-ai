using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Application.Modules.Reservas;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.Services;
using Backend.Domain.ValueObjects;
using Moq;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas del alta de series: modalidad incompatible, fuera de periodo académico y alta válida
/// sin lecturas innecesarias de la agenda.
/// </summary>
public sealed class CrearSerieHandlerTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 7, 1, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid EspacioId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DocenteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private static ComandoCrearSerie Comando() => new(
        EspacioId,
        TipoRecurrencia.Semanal,
        DayOfWeek.Tuesday,
        null,
        new TimeOnly(14, 0),
        new TimeOnly(16, 0),
        new DateOnly(2026, 8, 4),
        new DateOnly(2026, 11, 24),
        "Cálculo II");

    private static PeriodoAcademico Periodo(DateOnly inicio, DateOnly fin)
    {
        var periodo = PeriodoAcademico.Crear($"Periodo {inicio:yyyy}", inicio, fin, Ahora);
        periodo.Activar(Ahora);

        return periodo;
    }

    private static CalendarioInstitucional Calendario(PeriodoAcademico periodo) =>
        CalendarioInstitucional.Crear(periodo, [], new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 15), Zona);

    private static EspacioParaReserva Espacio(TipoModalidadPrestamo modalidad) => new(
        EspacioId,
        modalidad == TipoModalidadPrestamo.AreaCompleta ? "Laboratorio" : "Sala de cómputo",
        EstadoUnidad.Activo,
        TipoAprobacion.Docente,
        modalidad,
        modalidad == TipoModalidadPrestamo.AreaCompleta
            ? TipoElementoPrestamo.LaboratorioEspecializado
            : TipoElementoPrestamo.EquipoComputo);

    private static CrearSerieHandler Handler(
        EspacioParaReserva espacio,
        CalendarioInstitucional calendario,
        out Mock<IConsultaOcupacionEspacio> ocupacion)
    {
        var espacios = new Mock<IConsultaEspacios>();
        espacios.Setup(servicio => servicio.ObtenerParaReservaAsync(EspacioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(espacio);

        var consultaCalendario = new Mock<IConsultaCalendario>();
        consultaCalendario.Setup(servicio => servicio.ObtenerCalendarioAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(calendario);

        ocupacion = new Mock<IConsultaOcupacionEspacio>();
        ocupacion.Setup(servicio => servicio.ObtenerAsync(
                It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var repositorio = new Mock<IReservasRecurrentesRepository>();
        repositorio.Setup(servicio => servicio.GuardarSerieAsync(
                It.IsAny<ReservaRecurrente>(),
                It.IsAny<IReadOnlyList<FranjaHoraria>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoPersistenciaSerie(ResultadoGuardadoSerie.Guardada, []));

        var reloj = new Mock<IRelojSistema>();
        reloj.SetupGet(servicio => servicio.Ahora).Returns(Ahora);

        var usuario = new Mock<IUsuarioActual>();
        usuario.SetupGet(servicio => servicio.UsuarioId).Returns(DocenteId);

        return new CrearSerieHandler(
            espacios.Object,
            consultaCalendario.Object,
            ocupacion.Object,
            repositorio.Object,
            new OpcionesSemestre { SemanasMaximasPorSerie = 20, AntelacionMinimaDias = 3 },
            new OpcionesReservas { ZonaHoraria = "America/Bogota" },
            reloj.Object,
            usuario.Object);
    }

    [Fact]
    public async Task LaSerieSobreUnAreaPorPuestoIndividualDevuelve409()
    {
        var resultado = await Handler(
                Espacio(TipoModalidadPrestamo.PuestoIndividual),
                Calendario(Periodo(new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 15))),
                out var ocupacion)
            .HandleAsync(Comando());

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesSemestre.ModalidadIncompatible, resultado.Mensaje);
        ocupacion.Verify(
            servicio => servicio.ObtenerAsync(
                It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LaSerieFueraDelPeriodoAcademicoDevuelve409SinLeerLaAgenda()
    {
        var resultado = await Handler(
                Espacio(TipoModalidadPrestamo.AreaCompleta),
                Calendario(Periodo(new DateOnly(2026, 2, 1), new DateOnly(2026, 6, 15))),
                out var ocupacion)
            .HandleAsync(Comando());

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesCalendario.FueraDePeriodoAcademico, resultado.Mensaje);
        ocupacion.Verify(
            servicio => servicio.ObtenerAsync(
                It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LaSerieValidaSeConfirma()
    {
        var resultado = await Handler(
                Espacio(TipoModalidadPrestamo.AreaCompleta),
                Calendario(Periodo(new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 15))),
                out _)
            .HandleAsync(Comando());

        Assert.True(resultado.Exitoso);
        Assert.Equal(17, resultado.Valor?.TotalOcurrencias);
    }
}
