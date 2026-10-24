using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Asistencia.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.ValueObjects;
using Moq;

namespace Backend.UnitTests.Asistencia;

/// <summary>
/// Datos y dobles compartidos por las pruebas de asistencia: una franja de 10:00 a 11:00 del
/// 3 de noviembre, su puesto con QR válido y los puertos simulados del caso de uso.
/// </summary>
internal static class SoporteAsistencia
{
    public static readonly DateTimeOffset Creada = new(2026, 11, 3, 9, 0, 0, TimeSpan.FromHours(-5));
    public static readonly DateTimeOffset Inicio = new(2026, 11, 3, 10, 0, 0, TimeSpan.FromHours(-5));
    public static readonly Guid PuestoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid EspacioId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Titular = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Otro = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public const string CodigoQr = "QR-7T3K9M2XQ4ZP8V6BN1C5HJ0WRS";
    public const string OtroCodigoQr = "QR-00000000000000000000000000";

    public static Reserva Nueva(Guid? usuarioId = null, DateTimeOffset? inicio = null)
    {
        var comienzo = inicio ?? Inicio;

        return Reserva.Confirmar(
            PuestoId,
            usuarioId ?? Titular,
            FranjaHoraria.Desde(comienzo, comienzo.AddHours(1)),
            Creada);
    }

    public static ReservaParaCheckIn Vista(Reserva reserva, string codigoQr = CodigoQr) => new(
        reserva.Id,
        reserva.PuestoId,
        "PC-07",
        EspacioId,
        "Laboratorio de Cómputo",
        reserva.UsuarioId,
        "Ana Titular",
        reserva.Inicio,
        reserva.Fin,
        reserva.FinPlanificado,
        reserva.Estado,
        reserva.FechaCheckIn,
        reserva.FechaCheckOut,
        codigoQr);

    public static OpcionesReservas Opciones(int tolerancia = 15) => new()
    {
        VentanaToleranciaCheckInMinutos = tolerancia,
        ZonaHoraria = "America/Bogota"
    };

    public static PoliticaReserva Politica() => new(
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(4),
        IntervaloMinutos: 15,
        TimeSpan.FromMinutes(30),
        TimeSpan.FromDays(14),
        TimeSpan.FromMinutes(30),
        new TimeOnly(7, 0),
        new TimeOnly(21, 0));

    public static Mock<IRelojSistema> Reloj(DateTimeOffset ahora)
    {
        var reloj = new Mock<IRelojSistema>();
        reloj.SetupGet(servicio => servicio.Ahora).Returns(ahora);
        return reloj;
    }

    public static Mock<IUsuarioActual> Usuario(Guid? usuarioId)
    {
        var usuario = new Mock<IUsuarioActual>();
        usuario.SetupGet(servicio => servicio.UsuarioId).Returns(usuarioId);
        usuario.SetupGet(servicio => servicio.EstaAutenticado).Returns(usuarioId is not null);
        return usuario;
    }

    public static Mock<IConsultaCheckIn> Consulta(Reserva? reserva, string codigoQr = CodigoQr)
    {
        var consulta = new Mock<IConsultaCheckIn>();
        consulta.Setup(servicio => servicio.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => reserva is null ? null : Vista(reserva, codigoQr));
        return consulta;
    }

    public static Mock<IReservasRepository> Repositorio(Reserva? reserva, bool guarda = true)
    {
        var repositorio = new Mock<IReservasRepository>();
        repositorio.Setup(servicio => servicio.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reserva);
        repositorio.Setup(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guarda);
        return repositorio;
    }
}
