using System.Globalization;

namespace Backend.Application.Common;

/// <summary>
/// Política de las solicitudes de laboratorio: límite del pre-informe, cupo de solicitudes
/// activas y longitud mínima del motivo de rechazo.
/// </summary>
public sealed class OpcionesSolicitudes
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Solicitudes";

    /// <summary>Tamaño máximo admitido para el pre-informe en bytes (5 MB por omisión).</summary>
    public long TamanoMaximoPdfBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Solicitudes simultáneas en <c>Radicado</c> o <c>EnRevision</c> por cuenta.</summary>
    public int MaxSolicitudesActivasPorUsuario { get; set; } = 3;

    /// <summary>Longitud mínima del motivo de rechazo.</summary>
    public int LongitudMinimaMotivoRechazo { get; set; } = 10;

    /// <summary>Antelación mínima, en minutos, para radicar una solicitud.</summary>
    public int AntelacionMinimaMinutos { get; set; } = 30;

    /// <summary>Tamaño máximo en megabytes, redondeado hacia arriba, para los mensajes.</summary>
    public int TamanoMaximoPdfMegabytes => (int)Math.Ceiling(TamanoMaximoPdfBytes / 1024d / 1024d);

    /// <summary>Valida la coherencia de la configuración y falla el arranque si es inválida.</summary>
    public void Validar()
    {
        if (TamanoMaximoPdfBytes < 1024)
        {
            throw new InvalidOperationException(
                "Solicitudes:TamanoMaximoPdfBytes debe ser mayor o igual a 1024 bytes.");
        }

        if (MaxSolicitudesActivasPorUsuario < 1)
        {
            throw new InvalidOperationException(
                "Solicitudes:MaxSolicitudesActivasPorUsuario debe ser al menos 1.");
        }

        if (LongitudMinimaMotivoRechazo < 10)
        {
            throw new InvalidOperationException(
                "Solicitudes:LongitudMinimaMotivoRechazo debe ser al menos 10 caracteres.");
        }

        if (AntelacionMinimaMinutos < 0)
        {
            throw new InvalidOperationException(
                "Solicitudes:AntelacionMinimaMinutos no puede ser negativo.");
        }
    }
}

/// <summary>Política de las series semestrales: tope de semanas y antelación de la primera ocurrencia.</summary>
public sealed class OpcionesSemestre
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Semestre";

    /// <summary>Máximo de semanas que puede cubrir una serie.</summary>
    public int SemanasMaximasPorSerie { get; set; } = 20;

    /// <summary>Antelación mínima, en días, de la primera ocurrencia.</summary>
    public int AntelacionMinimaDias { get; set; } = 3;

    /// <summary>Valida la coherencia de la configuración y falla el arranque si es inválida.</summary>
    public void Validar()
    {
        if (SemanasMaximasPorSerie is < 1 or > 52)
        {
            throw new InvalidOperationException(
                "Semestre:SemanasMaximasPorSerie debe estar entre 1 y 52.");
        }

        if (AntelacionMinimaDias < 0)
        {
            throw new InvalidOperationException(
                "Semestre:AntelacionMinimaDias no puede ser negativo.");
        }
    }

    /// <summary>Antelación mínima como <see cref="TimeSpan"/>.</summary>
    public TimeSpan ObtenerAntelacion() => TimeSpan.FromDays(AntelacionMinimaDias);
}
