using Backend.Application.Modules.Solicitudes;
using Backend.Application.Modules.Solicitudes.Dtos;
using Backend.Domain.Enums;

namespace Backend.UnitTests.Solicitudes;

/// <summary>
/// Pruebas de la plantilla de correo: contenido del estado, motivo del rechazo, escape del HTML
/// y ausencia de rutas internas.
/// </summary>
public sealed class PlantillaCorreoSolicitudTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 5, 14, 0, 0, TimeSpan.FromHours(-5));

    private static SolicitudDetalle Detalle(string materia = "Bases de datos") => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Laboratorio de Redes",
        TipoAprobacion.Docente,
        TipoModalidadPrestamo.AreaCompleta,
        Guid.NewGuid(),
        "Camila Restrepo",
        "camila@uninpahu.edu.co",
        Inicio,
        Inicio.AddHours(2),
        materia,
        "Ing. Laura Gómez",
        "Práctica de laboratorio.",
        EstadoSolicitud.EnRevision,
        Inicio.AddDays(-2),
        null,
        null,
        null,
        "informe.pdf",
        819_200,
        "solicitudes/abc/informe.pdf");

    [Fact]
    public void ElCuerpoIncluyeAreaFranjaYEstado()
    {
        var cuerpo = PlantillaCorreoSolicitud.Componer(Detalle(), EstadoSolicitud.Aprobado, null);

        Assert.Contains("Laboratorio de Redes", cuerpo, StringComparison.Ordinal);
        Assert.Contains("2026-11-05 14:00", cuerpo, StringComparison.Ordinal);
        Assert.Contains("Aprobado", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public void ElRechazoIncluyeElMotivo()
    {
        var cuerpo = PlantillaCorreoSolicitud.Componer(
            Detalle(),
            EstadoSolicitud.Rechazado,
            "La franja se cruza con una practica programada.");

        Assert.Contains("franja se cruza con una practica programada", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public void ElAcentoSeCodificaComoEntidadNumerica()
    {
        var cuerpo = PlantillaCorreoSolicitud.Componer(
            Detalle(),
            EstadoSolicitud.Rechazado,
            "Práctica programada");

        Assert.Contains("Pr&#225;ctica programada", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public void ElCuerpoNuncaExponeRutasInternas()
    {
        var cuerpo = PlantillaCorreoSolicitud.Componer(Detalle(), EstadoSolicitud.Aprobado, null);

        Assert.DoesNotContain("solicitudes/abc", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(_raizFisica, cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public void ElContenidoDelUsuarioSeEscapa()
    {
        var cuerpo = PlantillaCorreoSolicitud.Componer(
            Detalle("<script>alert('xss')</script>"),
            EstadoSolicitud.EnRevision,
            null);

        Assert.DoesNotContain("<script>", cuerpo, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", cuerpo, StringComparison.Ordinal);
    }

    private static readonly string _raizFisica = Path.Combine("var", "lib", "reservas");
}
