using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Asistencia;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Moq;

namespace Backend.UnitTests.Asistencia;

/// <summary>
/// Pruebas del registro de salida: no exige ventana horaria, acorta el fin de la franja y devuelve
/// el remanente al motor de disponibilidad; rechaza sesiones que no están en uso.
/// </summary>
public sealed class RegistrarCheckOutHandlerTests
{
    private static Task<Resultado<ReservaDetalle>> EjecutarAsync(
        Mock<IReservasRepository> repositorio,
        Mock<IConsultaCheckIn> consulta,
        Guid reservaId,
        DateTimeOffset ahora,
        Guid usuarioId = default,
        bool sinSesion = false)
    {
        var titular = sinSesion
            ? (Guid?)null
            : usuarioId == Guid.Empty ? SoporteAsistencia.Titular : usuarioId;

        var handler = new RegistrarCheckOutHandler(
            repositorio.Object,
            consulta.Object,
            SoporteAsistencia.Reloj(ahora).Object,
            SoporteAsistencia.Usuario(titular).Object);

        return handler.HandleAsync(reservaId);
    }

    private static Domain.Entities.Reserva EnUso()
    {
        var reserva = SoporteAsistencia.Nueva();
        reserva.RegistrarCheckIn(SoporteAsistencia.Inicio.AddMinutes(2), TimeSpan.FromMinutes(15));
        return reserva;
    }

    [Fact]
    public async Task LaSalidaAnticipadaFinalizaLaReservaYAcortaElFin()
    {
        var reserva = EnUso();
        var salida = SoporteAsistencia.Inicio.AddMinutes(45);
        var repositorio = SoporteAsistencia.Repositorio(reserva);

        var resultado = await EjecutarAsync(
            repositorio,
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            salida);

        Assert.True(resultado.Exitoso);
        Assert.NotNull(resultado.Valor);
        Assert.Equal(EstadoReserva.Finalizada, resultado.Valor.Estado);
        Assert.Equal(salida, resultado.Valor.Fin);
        Assert.Equal(SoporteAsistencia.Inicio.AddHours(1), resultado.Valor.FinPlanificado);
        Assert.Equal(salida, resultado.Valor.FechaCheckOut);
        Assert.Equal(SoporteAsistencia.Inicio.AddMinutes(2), resultado.Valor.FechaCheckIn);
        Assert.False(reserva.EstaVigente);
        repositorio.Verify(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LaSalidaTardiaRegistraLaHoraRealSinExtenderLaFranja()
    {
        var reserva = EnUso();
        var salida = SoporteAsistencia.Inicio.AddMinutes(90);

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            salida);

        Assert.True(resultado.Exitoso);
        Assert.Equal(salida, resultado.Valor!.FechaCheckOut);
        Assert.Equal(SoporteAsistencia.Inicio.AddHours(1), resultado.Valor.Fin);
        Assert.Equal(EstadoReserva.Finalizada, resultado.Valor.Estado);
    }

    [Fact]
    public async Task SinSesionDevuelve401()
    {
        var reserva = EnUso();

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            SoporteAsistencia.Inicio.AddMinutes(45),
            sinSesion: true);

        Assert.Equal(TipoFallo.NoAutorizado, resultado.TipoFallo);
        Assert.Equal(EstadoReserva.EnUso, reserva.Estado);
    }

    [Fact]
    public async Task UnaReservaInexistenteDevuelve404()
    {
        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(null),
            SoporteAsistencia.Consulta(null),
            Guid.NewGuid(),
            SoporteAsistencia.Inicio.AddMinutes(45));

        Assert.Equal(TipoFallo.NoEncontrado, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.ReservaNoEncontrada, resultado.Mensaje);
    }

    [Fact]
    public async Task LaSalidaDeUnaReservaAjenaDevuelve403()
    {
        var reserva = EnUso();

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            SoporteAsistencia.Inicio.AddMinutes(45),
            usuarioId: SoporteAsistencia.Otro);

        Assert.Equal(TipoFallo.NoPermitido, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.ReservaAjena, resultado.Mensaje);
        Assert.Equal(EstadoReserva.EnUso, reserva.Estado);
    }

    [Theory]
    [InlineData(EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.NoShow)]
    [InlineData(EstadoReserva.Cancelada)]
    public async Task UnaReservaQueNoEstaEnUsoDevuelve409(EstadoReserva estado)
    {
        var reserva = estado == EstadoReserva.Cancelada
            ? SoporteAsistencia.Nueva(inicio: SoporteAsistencia.Inicio.AddHours(4))
            : SoporteAsistencia.Nueva();

        if (estado == EstadoReserva.NoShow)
        {
            reserva.LiberarPorNoShow(SoporteAsistencia.Inicio.AddMinutes(16));
        }
        else if (estado == EstadoReserva.Cancelada)
        {
            reserva.Cancelar(SoporteAsistencia.Creada, SoporteAsistencia.Politica());
        }

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            SoporteAsistencia.Inicio.AddMinutes(45));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.TransicionInvalida, resultado.Mensaje);
        Assert.Equal(estado, reserva.Estado);
    }

    [Fact]
    public async Task UnaSegundaSalidaDevuelve409()
    {
        var reserva = EnUso();
        reserva.Finalizar(SoporteAsistencia.Inicio.AddMinutes(45));

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            SoporteAsistencia.Inicio.AddMinutes(50));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.TransicionInvalida, resultado.Mensaje);
        Assert.Equal(EstadoReserva.Finalizada, reserva.Estado);
    }

    [Fact]
    public async Task ElConflictoDeConcurrenciaAlGuardarDevuelve409()
    {
        var reserva = EnUso();

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva, guarda: false),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            SoporteAsistencia.Inicio.AddMinutes(45));

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.TransicionNoPersistida, resultado.Mensaje);
    }
}
