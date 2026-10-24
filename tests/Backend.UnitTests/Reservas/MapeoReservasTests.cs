using Backend.Application.Modules.Asistencia.Dtos;
using Backend.Application.Modules.Reservas.Dtos;
using Backend.Domain.Enums;
using Backend.UnitTests.Asistencia;

namespace Backend.UnitTests.Reservas;

/// <summary>
/// Pruebas del mapeo de reservas al contrato de detalle: el fin planificado y las marcas de
/// check-in/check-out llegan a la API sin alterar el fin efectivo de la franja.
/// </summary>
public sealed class MapeoReservasTests
{
    private static PuestoReservable Puesto() => new(
        SoporteAsistencia.PuestoId,
        "PC-07",
        EstadoUnidad.Activo,
        SoporteAsistencia.EspacioId,
        "Laboratorio de Cómputo",
        EstadoUnidad.Activo,
        TipoAprobacion.Automatica);

    [Fact]
    public void ElDetalleDeUnaReservaConfirmadaExponeElFinPlanificadoYLasFechasVacias()
    {
        var reserva = SoporteAsistencia.Nueva();

        var detalle = MapeoReservas.ADetalle(reserva, Puesto());

        Assert.Equal(reserva.Inicio, detalle.Inicio);
        Assert.Equal(reserva.Fin, detalle.Fin);
        Assert.Equal(reserva.FinPlanificado, detalle.FinPlanificado);
        Assert.Equal(EstadoReserva.Confirmada, detalle.Estado);
        Assert.Null(detalle.FechaCheckIn);
        Assert.Null(detalle.FechaCheckOut);
        Assert.Equal("PC-07", detalle.PuestoIdentificador);
    }

    [Fact]
    public void ElDetalleTrasElCheckOutConservaElPlanificadoYMuestraElFinAcortado()
    {
        var reserva = SoporteAsistencia.Nueva();
        var ingreso = reserva.Inicio.AddMinutes(2);
        var salida = reserva.Inicio.AddMinutes(45);

        reserva.RegistrarCheckIn(ingreso, TimeSpan.FromMinutes(15));
        reserva.Finalizar(salida);

        var detalle = MapeoAsistencia.ADetalle(reserva, SoporteAsistencia.Vista(reserva));

        Assert.Equal(EstadoReserva.Finalizada, detalle.Estado);
        Assert.Equal(salida, detalle.Fin);
        Assert.Equal(reserva.Inicio.AddHours(1), detalle.FinPlanificado);
        Assert.Equal(ingreso, detalle.FechaCheckIn);
        Assert.Equal(salida, detalle.FechaCheckOut);
        Assert.Equal("Laboratorio de Cómputo", detalle.EspacioNombre);
    }
}
