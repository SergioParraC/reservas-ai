using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Backend.Domain.ValueObjects;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas de la máquina de estados de asistencia: ventana simétrica de check-in con tolerancia
/// variable, transiciones inválidas y acortado del fin de la franja al registrar la salida.
/// </summary>
public sealed class ReservaAsistenciaTests
{
    private static readonly DateTimeOffset Creada = new(2026, 11, 3, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset Inicio = new(2026, 11, 3, 10, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Puesto = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Titular = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Reserva Nueva(DateTimeOffset? inicio = null)
    {
        var comienzo = inicio ?? Inicio;

        return Reserva.Confirmar(
            Puesto,
            Titular,
            FranjaHoraria.Desde(comienzo, comienzo.AddHours(1)),
            Creada);
    }

    private static PoliticaReserva Politica() => new(
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(4),
        IntervaloMinutos: 15,
        TimeSpan.FromMinutes(30),
        TimeSpan.FromDays(14),
        TimeSpan.FromMinutes(30),
        new TimeOnly(7, 0),
        new TimeOnly(21, 0));

    [Fact]
    public void ElAltaFijaElFinPlanificadoYDejaLasMarcasDeAsistenciaVacias()
    {
        var reserva = Nueva();

        Assert.Equal(Inicio, reserva.Inicio);
        Assert.Equal(Inicio.AddHours(1), reserva.Fin);
        Assert.Equal(reserva.Fin, reserva.FinPlanificado);
        Assert.Null(reserva.FechaCheckIn);
        Assert.Null(reserva.FechaCheckOut);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        Assert.True(reserva.EstaVigente);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    public void LaVentanaEsSimetricaEIncluyeSusExtremos(int minutos)
    {
        var reserva = Nueva();
        var tolerancia = TimeSpan.FromMinutes(minutos);

        Assert.True(reserva.EstaEnVentanaDeCheckIn(Inicio - tolerancia, tolerancia));
        Assert.True(reserva.EstaEnVentanaDeCheckIn(Inicio, tolerancia));
        Assert.True(reserva.EstaEnVentanaDeCheckIn(Inicio + tolerancia, tolerancia));
        Assert.False(reserva.EstaEnVentanaDeCheckIn(Inicio - tolerancia - TimeSpan.FromMinutes(1), tolerancia));
        Assert.False(reserva.EstaEnVentanaDeCheckIn(Inicio + tolerancia + TimeSpan.FromMinutes(1), tolerancia));
    }

    [Fact]
    public void ElCheckInDentroDeLaVentanaRegistraLaMarcaYPasaAEnUso()
    {
        var reserva = Nueva();
        var momento = Inicio.AddMinutes(14);

        reserva.RegistrarCheckIn(momento, TimeSpan.FromMinutes(15));

        Assert.Equal(EstadoReserva.EnUso, reserva.Estado);
        Assert.Equal(momento, reserva.FechaCheckIn);
        Assert.Null(reserva.FechaCheckOut);
        Assert.True(reserva.EstaVigente);
    }

    [Fact]
    public void ElCheckInAnticipadoDentroDeLaVentanaEsValido()
    {
        var reserva = Nueva();
        var momento = Inicio.AddMinutes(-14);

        reserva.RegistrarCheckIn(momento, TimeSpan.FromMinutes(15));

        Assert.Equal(EstadoReserva.EnUso, reserva.Estado);
        Assert.Equal(momento, reserva.FechaCheckIn);
    }

    [Fact]
    public void ElCheckInFueraDeLaVentanaIndicaElRangoAdmitidoSinModificarLaReserva()
    {
        var reserva = Nueva();

        var excepcion = Assert.Throws<ConflictoException>(
            () => reserva.RegistrarCheckIn(Inicio.AddMinutes(-16), TimeSpan.FromMinutes(15)));

        Assert.Contains("09:45", excepcion.Message, StringComparison.Ordinal);
        Assert.Contains("10:15", excepcion.Message, StringComparison.Ordinal);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        Assert.Null(reserva.FechaCheckIn);
    }

    [Fact]
    public void UnaVentanaMasCortaRechazaUnEscaneoAntesValido()
    {
        var reserva = Nueva();
        var momento = Inicio.AddMinutes(-12);

        reserva.EstaEnVentanaDeCheckIn(momento, TimeSpan.FromMinutes(15));
        Assert.Throws<ConflictoException>(
            () => reserva.RegistrarCheckIn(momento, TimeSpan.FromMinutes(10)));
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Theory]
    [InlineData(EstadoReserva.EnUso)]
    [InlineData(EstadoReserva.NoShow)]
    [InlineData(EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.Finalizada)]
    public void ElCheckInDesdeUnEstadoNoConfirmadoLanzaConflicto(EstadoReserva estado)
    {
        var reserva = EnEstado(estado);

        var excepcion = Assert.Throws<ConflictoException>(
            () => reserva.RegistrarCheckIn(Inicio.AddMinutes(1), TimeSpan.FromMinutes(15)));

        Assert.Equal(MensajesReserva.TransicionInvalida, excepcion.Message);
        Assert.Equal(estado, reserva.Estado);
    }

    [Fact]
    public void ElCheckOutAnticipadoAcortaElFinYConservaElPlanificado()
    {
        var reserva = Nueva();
        var ingreso = Inicio.AddMinutes(2);
        var salida = Inicio.AddMinutes(45);

        reserva.RegistrarCheckIn(ingreso, TimeSpan.FromMinutes(15));
        reserva.Finalizar(salida);

        Assert.Equal(EstadoReserva.Finalizada, reserva.Estado);
        Assert.Equal(salida, reserva.FechaCheckOut);
        Assert.Equal(salida, reserva.Fin);
        Assert.Equal(Inicio.AddHours(1), reserva.FinPlanificado);
        Assert.Equal(ingreso, reserva.FechaCheckIn);
        Assert.False(reserva.EstaVigente);
    }

    [Fact]
    public void ElCheckOutTardioNoExtiendeLaFranja()
    {
        var reserva = Nueva();
        var salida = Inicio.AddMinutes(90);

        reserva.RegistrarCheckIn(Inicio.AddMinutes(1), TimeSpan.FromMinutes(15));
        reserva.Finalizar(salida);

        Assert.Equal(salida, reserva.FechaCheckOut);
        Assert.Equal(Inicio.AddHours(1), reserva.Fin);
        Assert.Equal(Inicio.AddHours(1), reserva.FinPlanificado);
        Assert.Equal(EstadoReserva.Finalizada, reserva.Estado);
    }

    [Fact]
    public void ElCheckOutAntesDelInicioDejaLaFranjaCompletaDisponibleSinInvertirla()
    {
        var reserva = Nueva();

        reserva.RegistrarCheckIn(Inicio.AddMinutes(-20), TimeSpan.FromMinutes(30));
        reserva.Finalizar(Inicio.AddMinutes(-15));

        Assert.Equal(Inicio, reserva.Fin);
        Assert.Equal(Inicio.AddHours(1), reserva.FinPlanificado);
        Assert.False(reserva.EstaVigente);
    }

    [Theory]
    [InlineData(EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.NoShow)]
    [InlineData(EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.Finalizada)]
    public void ElCheckOutFueraDeEnUsoLanzaConflicto(EstadoReserva estado)
    {
        var reserva = EnEstado(estado);

        var excepcion = Assert.Throws<ConflictoException>(() => reserva.Finalizar(Inicio.AddMinutes(30)));

        Assert.Equal(MensajesReserva.TransicionInvalida, excepcion.Message);
        Assert.Equal(estado, reserva.Estado);
    }

    [Fact]
    public void LaLiberacionPorInasistenciaNoTocaUnaSesionEnUso()
    {
        var reserva = EnEstado(EstadoReserva.EnUso);

        Assert.Throws<ConflictoException>(() => reserva.LiberarPorNoShow(Inicio.AddMinutes(30)));
        Assert.Equal(EstadoReserva.EnUso, reserva.Estado);
    }

    [Fact]
    public void LaCancelacionNoAplicaAUnaSesionEnUso()
    {
        var reserva = EnEstado(EstadoReserva.EnUso);

        Assert.Throws<ConflictoException>(() => reserva.Cancelar(Creada, Politica()));
        Assert.Equal(EstadoReserva.EnUso, reserva.Estado);
    }

    [Theory]
    [InlineData(EstadoReserva.Confirmada, true)]
    [InlineData(EstadoReserva.EnUso, true)]
    [InlineData(EstadoReserva.Cancelada, false)]
    [InlineData(EstadoReserva.NoShow, false)]
    [InlineData(EstadoReserva.Finalizada, false)]
    public void SoloConfirmadaYEnUsoBloqueanLaFranja(EstadoReserva estado, bool vigente) =>
        Assert.Equal(vigente, EnEstado(estado).EstaVigente);

    private static Reserva EnEstado(EstadoReserva estado)
    {
        var reserva = estado == EstadoReserva.Cancelada
            ? Nueva(Inicio.AddHours(3))
            : Nueva();

        switch (estado)
        {
            case EstadoReserva.Confirmada:
                break;

            case EstadoReserva.EnUso:
                reserva.RegistrarCheckIn(reserva.Inicio.AddMinutes(1), TimeSpan.FromMinutes(15));
                break;

            case EstadoReserva.NoShow:
                reserva.LiberarPorNoShow(reserva.Inicio.AddMinutes(16));
                break;

            case EstadoReserva.Cancelada:
                reserva.Cancelar(Creada, Politica());
                break;

            case EstadoReserva.Finalizada:
                reserva.RegistrarCheckIn(reserva.Inicio.AddMinutes(1), TimeSpan.FromMinutes(15));
                reserva.Finalizar(reserva.Inicio.AddMinutes(30));
                break;
        }

        Assert.Equal(estado, reserva.Estado);
        return reserva;
    }
}

