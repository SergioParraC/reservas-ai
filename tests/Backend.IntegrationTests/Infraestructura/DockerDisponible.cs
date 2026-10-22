using System.Diagnostics;

namespace Backend.IntegrationTests.Infraestructura;

/// <summary>Detector de disponibilidad de Docker, evaluado una sola vez por proceso.</summary>
internal static class DockerDisponible
{
    private static readonly Lazy<bool> Deteccion = new(Detectar, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Indica si el motor de Docker responde en este equipo.</summary>
    public static bool Si => Deteccion.Value;

    private static bool Detectar()
    {
        try
        {
            using var proceso = Process.Start(new ProcessStartInfo("docker", "info --format {{.ServerVersion}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (proceso is null)
            {
                return false;
            }

            if (!proceso.WaitForExit(5000))
            {
                proceso.Kill(entireProcessTree: true);
                return false;
            }

            return proceso.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
