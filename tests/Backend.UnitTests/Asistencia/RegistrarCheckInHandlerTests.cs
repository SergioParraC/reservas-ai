using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Asistencia;
using Backend.Application.Modules.Asistencia.Dtos;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Moq;

namespace Backend.UnitTests.Asistencia;

/// <summary>
/// Pruebas del registro de ingreso: éxito, sesión ausente, reserva ajena o inexistente, ventana de
/// tolerancia tomada de la configuración, código inválido y transiciones no admitidas.
/// </summary>
public sealed class RegistrarCheckInHandlerTests
{
    private static RegistrarCheckInHandler Handler(
        Mock<IReservasRepository> repositorio,
        Mock<IConsultaCheckIn> consulta,
        int tolerancia = 15,
        Guid usuarioId = default,
        bool sinSesion = false,
        DateTimeOffset? ahora = null)
    {
        var titular = sinSesion
            ? (Guid?)null
            : usuarioId == Guid.Empty ? SoporteAsistencia.Titular : usuarioId;

        return new RegistrarCheckInHandler(
            repositorio.Object,
            consulta.Object,
            SoporteAsistencia.Opciones(tolerancia),
            SoporteAsistencia.Reloj(ahora ?? SoporteAsistencia.Inicio.AddMinutes(5)).Object,
            SoporteAsistencia.Usuario(titular).Object);
    }

    private static Task<Resultado<ReservaDetalle>> EjecutarAsync(
        Mock<IReservasRepository> repositorio,
        Mock<IConsultaCheckIn> consulta,
        Guid? reservaId = null,
        string codigoQr = SoporteAsistencia.CodigoQr,
        int tolerancia = 15,
        Guid usuarioId = default,
        bool sinSesion = false,
        DateTimeOffset? ahora = null) =>
        Handler(repositorio, consulta, tolerancia, usuarioId, sinSesion, ahora)
            .HandleAsync(new ComandoRegistrarCheckIn(reservaId ?? Guid.NewGuid(), codigoQr));

    [Fact]
    public async Task ElCheckInValidoDevuelveLaReservaEnUsoConSuMarcaDeIngreso()
    {
        var reserva = SoporteAsistencia.Nueva();
        var ahora = SoporteAsistencia.Inicio.AddMinutes(2);
        var repositorio = SoporteAsistencia.Repositorio(reserva);

        var resultado = await EjecutarAsync(repositorio, SoporteAsistencia.Consulta(reserva), reserva.Id, ahora: ahora);

        Assert.True(resultado.Exitoso);
        Assert.NotNull(resultado.Valor);
        Assert.Equal(EstadoReserva.EnUso, resultado.Valor.Estado);
        Assert.Equal(ahora, resultado.Valor.FechaCheckIn);
        Assert.Null(resultado.Valor.FechaCheckOut);
        Assert.Equal("PC-07", resultado.Valor.PuestoIdentificador);
        Assert.Equal(reserva.Inicio.AddHours(1), resultado.Valor.FinPlanificado);
        repositorio.Verify(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SinSesionDevuelve401()
    {
        var reserva = SoporteAsistencia.Nueva();

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            sinSesion: true);

        Assert.Equal(TipoFallo.NoAutorizado, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.SinSesion, resultado.Mensaje);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    public async Task UnaReservaInexistenteDevuelve404()
    {
        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(null),
            SoporteAsistencia.Consulta(null));

        Assert.Equal(TipoFallo.NoEncontrado, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.ReservaNoEncontrada, resultado.Mensaje);
    }

    [Fact]
    public async Task UnaReservaDeOtroTitularDevuelve403()
    {
        var reserva = SoporteAsistencia.Nueva(SoporteAsistencia.Otro);

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id);

        Assert.Equal(TipoFallo.NoPermitido, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.ReservaAjena, resultado.Mensaje);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Theory]
    [InlineData(10, 11, "10:10")]
    [InlineData(15, 16, "10:15")]
    public async Task FueraDeLaVentanaConfiguradaDevuelve409ConElRangoAdmitido(
        int tolerancia,
        int minutosTarde,
        string limite)
    {
        var reserva = SoporteAsistencia.Nueva();
        var ahora = SoporteAsistencia.Inicio.AddMinutes(minutosTarde);

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            tolerancia: tolerancia,
            ahora: ahora);

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Contains(limite, resultado.Mensaje ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        Assert.Null(reserva.FechaCheckIn);
    }

    [Theory]
    [InlineData("QR-00000000000000000000000000")]
    [InlineData("codigo-manual-invalido")]
    [InlineData("")]
    public async Task UnCodigoQueNoEsElDelPuestoDevuelve409(string codigo)
    {
        var reserva = SoporteAsistencia.Nueva();

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            codigoQr: codigo);

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.CheckInCodigoInvalido, resultado.Mensaje);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    public async Task ElCodigoDelPuestoSeComparaNormalizandoMayusculas()
    {
        var reserva = SoporteAsistencia.Nueva();

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id,
            codigoQr: SoporteAsistencia.CodigoQr.ToLowerInvariant());

        Assert.True(resultado.Exitoso);
        Assert.Equal(EstadoReserva.EnUso, resultado.Valor!.Estado);
    }

    [Fact]
    public async Task UnCheckInRepetidoDevuelve409PorTransicionInvalida()
    {
        var reserva = SoporteAsistencia.Nueva();
        reserva.RegistrarCheckIn(SoporteAsistencia.Inicio.AddMinutes(1), TimeSpan.FromMinutes(15));

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id);

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.TransicionInvalida, resultado.Mensaje);
    }

    [Fact]
    public async Task UnaReservaLiberadaPorInasistenciaNoAdmiteCheckIn()
    {
        var reserva = SoporteAsistencia.Nueva();
        reserva.LiberarPorNoShow(SoporteAsistencia.Inicio.AddMinutes(16));

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id);

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.TransicionInvalida, resultado.Mensaje);
        Assert.Equal(EstadoReserva.NoShow, reserva.Estado);
    }

    [Fact]
    public async Task ElConflictoDeConcurrenciaAlGuardarDevuelve409()
    {
        var reserva = SoporteAsistencia.Nueva();

        var resultado = await EjecutarAsync(
            SoporteAsistencia.Repositorio(reserva, guarda: false),
            SoporteAsistencia.Consulta(reserva),
            reserva.Id);

        Assert.Equal(TipoFallo.Conflicto, resultado.TipoFallo);
        Assert.Equal(MensajesReserva.TransicionNoPersistida, resultado.Mensaje);
    }
}
