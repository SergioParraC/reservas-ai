using Backend.Application.Common;

namespace Backend.UnitTests.Calendario;

/// <summary>
/// Pruebas de validación de la configuración del calendario y del SMTP: el arranque debe fallar
/// con un TTL inválido o un puerto fuera de rango.
/// </summary>
public sealed class OpcionesCalendarioTests
{
    [Fact]
    public void UnTtlDeCacheNegativoFallaElArranque()
    {
        var opciones = new OpcionesCalendario { MinutosCacheCalendario = -1 };

        var excepcion = Assert.Throws<InvalidOperationException>(opciones.Validar);

        Assert.Contains("MinutosCacheCalendario", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnFeriadoConFormatoInvalidoFallaElArranque()
    {
        var opciones = new OpcionesCalendario { FeriadosNacionales = ["13/10/2026"] };

        var excepcion = Assert.Throws<InvalidOperationException>(opciones.Validar);

        Assert.Contains("FeriadosNacionales", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnPuertoSmtpFueraDeRangoFallaElArranque()
    {
        var opciones = new OpcionesSmtp { Puerto = 70000 };

        var excepcion = Assert.Throws<InvalidOperationException>(opciones.Validar);

        Assert.Contains("Puerto", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnIntervaloDeDespachoInvalidoFallaElArranque()
    {
        var opciones = new OpcionesSmtp { IntervaloDespachoSegundos = 0 };

        var excepcion = Assert.Throws<InvalidOperationException>(opciones.Validar);

        Assert.Contains("IntervaloDespachoSegundos", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnMotivoDeRechazoMenorQueDiezFallaElArranque()
    {
        var opciones = new OpcionesSolicitudes { LongitudMinimaMotivoRechazo = 5 };

        var excepcion = Assert.Throws<InvalidOperationException>(opciones.Validar);

        Assert.Contains("LongitudMinimaMotivoRechazo", excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LasOpcionesValidasNoLanzan()
    {
        new OpcionesCalendario().Validar();
        new OpcionesSmtp().Validar();
        new OpcionesSolicitudes().Validar();
        new OpcionesSemestre().Validar();
    }
}
