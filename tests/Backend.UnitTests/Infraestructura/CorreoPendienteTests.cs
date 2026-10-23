using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.UnitTests.Infraestructura;

/// <summary>
/// Pruebas de la cola de correos: envío exitoso, reintento con retroceso y agotamiento de intentos
/// sin revertir la decisión del revisor.
/// </summary>
public sealed class CorreoPendienteTests
{
    private static readonly DateTimeOffset Momento = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));

    private static CorreoPendiente Correo() => CorreoPendiente.Crear(
        "camila@uninpahu.edu.co",
        "Su solicitud fue aprobada",
        "<p>Aprobada</p>",
        Guid.NewGuid(),
        Momento);

    [Fact]
    public void ElCorreoNacePendienteSinIntentos()
    {
        var correo = Correo();

        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal(0, correo.Intentos);
        Assert.True(correo.EstaPendiente);
        Assert.Equal(Momento, correo.ProximoIntentoEn);
    }

    [Fact]
    public void ElEnvioExitosoRegistraLaFecha()
    {
        var correo = Correo();
        var envio = Momento.AddMinutes(1);

        correo.MarcarEnviado(envio);

        Assert.Equal(EstadoCorreo.Enviado, correo.Estado);
        Assert.Equal(envio, correo.EnviadoEn);
        Assert.False(correo.EstaPendiente);
    }

    [Fact]
    public void ElFalloProgramaElSiguienteIntentoConRetroceso()
    {
        var correo = Correo();

        correo.RegistrarFallo("SMTP no disponible", Momento);

        Assert.Equal(1, correo.Intentos);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.True(correo.ProximoIntentoEn > Momento);
        Assert.Equal("SMTP no disponible", correo.UltimoError);
    }

    [Fact]
    public void TrasAgotarLosIntentosElCorreoQuedaFallidoSinEliminarse()
    {
        var correo = Correo();

        for (var intento = 0; intento < CorreoPendiente.IntentosMaximos; intento++)
        {
            correo.RegistrarFallo("SMTP no disponible", Momento);
        }

        Assert.Equal(EstadoCorreo.Fallido, correo.Estado);
        Assert.Equal(CorreoPendiente.IntentosMaximos, correo.Intentos);
    }
}
