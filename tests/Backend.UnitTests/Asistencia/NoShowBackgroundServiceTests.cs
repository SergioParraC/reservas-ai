using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Asistencia;
using Backend.Domain.Enums;
using Backend.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Backend.UnitTests.Asistencia;

/// <summary>
/// Pruebas del servicio en segundo plano: el primer ciclo corre al arrancar, un fallo no detiene el
/// servicio, <c>Habilitado = false</c> no barre y la tolerancia configurada mueve el corte.
/// </summary>
public sealed class NoShowBackgroundServiceTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 3, 10, 20, 0, TimeSpan.FromHours(-5));

    private sealed class RelojFijo : IRelojSistema
    {
        public RelojFijo(DateTimeOffset ahora) => Ahora = ahora;

        public DateTimeOffset Ahora { get; }
    }

    private static ServiceProvider Proveedor(Mock<IReservasRepository> repositorio, int tolerancia = 15)
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton(repositorio.Object);
        servicios.AddSingleton(SoporteAsistencia.Opciones(tolerancia));
        servicios.AddSingleton(new OpcionesCheckIn { MaximoReservasPorBarrido = 10 });
        servicios.AddSingleton<IRelojSistema>(new RelojFijo(Ahora));
        servicios.AddScoped<LiberarReservasVencidasHandler>();

        return servicios.BuildServiceProvider();
    }

    private static NoShowBackgroundService Servicio(
        IServiceProvider proveedor,
        OpcionesCheckIn opciones,
        int tolerancia = 15)
    {
        return new NoShowBackgroundService(
            proveedor.GetRequiredService<IServiceScopeFactory>(),
            SoporteAsistencia.Opciones(tolerancia),
            opciones,
            NullLogger<NoShowBackgroundService>.Instance);
    }

    [Fact]
    public async Task DeshabilitadoNoBarreYElServicioTerminaDeInmediato()
    {
        var repositorio = new Mock<IReservasRepository>();
        using var proveedor = Proveedor(repositorio);
        var servicio = Servicio(proveedor, new OpcionesCheckIn { Habilitado = false });

        await servicio.StartAsync(CancellationToken.None);
        await Task.Delay(150);

        Assert.NotNull(servicio.ExecuteTask);
        Assert.True(servicio.ExecuteTask!.IsCompleted);
        repositorio.Verify(
            servicio2 => servicio2.ListarSinCheckInVencidasAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        await servicio.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task UnFalloDelCicloNoDetieneElServicio()
    {
        var repositorio = new Mock<IReservasRepository>();
        repositorio
            .Setup(servicio => servicio.ListarSinCheckInVencidasAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("caída simulada de la base de datos"));

        using var proveedor = Proveedor(repositorio);
        var servicio = Servicio(proveedor, new OpcionesCheckIn());

        await servicio.StartAsync(CancellationToken.None);
        await Task.Delay(250);

        Assert.False(servicio.ExecuteTask!.IsCompleted);
        repositorio.Verify(
            servicio2 => servicio2.ListarSinCheckInVencidasAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        await servicio.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ElPrimerCicloLiberaLoVencidoSinEsperarLaCadencia()
    {
        var reserva = SoporteAsistencia.Nueva(inicio: Ahora.AddMinutes(-60));
        var repositorio = new Mock<IReservasRepository>();
        repositorio
            .Setup(servicio => servicio.ListarSinCheckInVencidasAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([reserva]);
        repositorio.Setup(servicio => servicio.GuardarCambiosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        using var proveedor = Proveedor(repositorio);
        var servicio = Servicio(proveedor, new OpcionesCheckIn());

        await servicio.StartAsync(CancellationToken.None);

        var limite = DateTime.UtcNow.AddSeconds(5);
        while (reserva.Estado != EstadoReserva.NoShow && DateTime.UtcNow < limite)
        {
            await Task.Delay(25);
        }

        Assert.Equal(EstadoReserva.NoShow, reserva.Estado);
        Assert.Equal(Ahora, reserva.CanceladaEn);

        await servicio.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LaToleranciaInyectadaEsLaQueDefineElCorteDelCiclo()
    {
        var repositorio = new Mock<IReservasRepository>();
        repositorio
            .Setup(servicio => servicio.ListarSinCheckInVencidasAsync(
                It.IsAny<DateTimeOffset>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var proveedor = Proveedor(repositorio, tolerancia: 20);
        var servicio = Servicio(proveedor, new OpcionesCheckIn(), tolerancia: 20);

        await servicio.EjecutarCicloAsync();

        repositorio.Verify(
            servicio2 => servicio2.ListarSinCheckInVencidasAsync(
                Ahora,
                TimeSpan.FromMinutes(20),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
