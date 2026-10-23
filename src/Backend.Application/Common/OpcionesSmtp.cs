namespace Backend.Application.Common;

/// <summary>Política del calendario institucional: exigencia de periodo activo y TTL de la caché.</summary>
public sealed class OpcionesCalendario
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Calendario";

    /// <summary>Indica si toda reserva exige un periodo académico activo.</summary>
    public bool PeriodoActivoRequerido { get; set; } = true;

    /// <summary>Minutos de vigencia de la caché del periodo y las franjas.</summary>
    public int MinutosCacheCalendario { get; set; } = 1;

    /// <summary>Feriados nacionales opcionales para la carga inicial.</summary>
    public IReadOnlyList<string> FeriadosNacionales { get; set; } = [];

    /// <summary>Valida la coherencia de la configuración y falla el arranque si es inválida.</summary>
    public void Validar()
    {
        if (MinutosCacheCalendario < 0)
        {
            throw new InvalidOperationException(
                "Calendario:MinutosCacheCalendario no puede ser negativo.");
        }

        foreach (var feriado in FeriadosNacionales)
        {
            if (!DateOnly.TryParseExact(feriado, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
            {
                throw new InvalidOperationException(
                    $"Calendario:FeriadosNacionales contiene un valor inválido ('{feriado}'). Use el formato yyyy-MM-dd.");
            }
        }
    }

    /// <summary>Vigencia de la caché como <see cref="TimeSpan"/>.</summary>
    public TimeSpan ObtenerVigenciaCache() => TimeSpan.FromMinutes(MinutosCacheCalendario);
}

/// <summary>Configuración del servidor SMTP y del despachador de la cola de correos.</summary>
public sealed class OpcionesSmtp
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string Seccion = "Smtp";

    /// <summary>Host del servidor SMTP.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Puerto SMTP.</summary>
    public int Puerto { get; set; } = 1025;

    /// <summary>Indica si la conexión usa TLS implícito.</summary>
    public bool UsarSsl { get; set; }

    /// <summary>Usuario de autenticación; vacío para servidores sin autenticación.</summary>
    public string Usuario { get; set; } = string.Empty;

    /// <summary>Contraseña de autenticación.</summary>
    public string Contrasena { get; set; } = string.Empty;

    /// <summary>Remitente de las notificaciones.</summary>
    public string RemitenteCorreo { get; set; } = "reservas@uninpahu.edu.co";

    /// <summary>Nombre visible del remitente.</summary>
    public string RemitenteNombre { get; set; } = "Reservas de Espacios";

    /// <summary>Interruptor del despachador; en desarrollo puede quedar apagado.</summary>
    public bool Habilitado { get; set; }

    /// <summary>Cadencia del despachador en segundos.</summary>
    public int IntervaloDespachoSegundos { get; set; } = 15;

    /// <summary>Indica si el servidor SMTP exige credenciales.</summary>
    public bool RequiereAutenticacion =>
        !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(Contrasena);

    /// <summary>Valida la coherencia de la configuración y falla el arranque si es inválida.</summary>
    public void Validar()
    {
        if (Puerto is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "Smtp:Puerto debe estar entre 1 y 65535.");
        }

        if (IntervaloDespachoSegundos < 1)
        {
            throw new InvalidOperationException(
                "Smtp:IntervaloDespachoSegundos debe ser al menos 1 segundo.");
        }

        if (Habilitado && string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException(
                "Smtp:Host es obligatorio cuando Smtp:Habilitado es true.");
        }

        if (string.IsNullOrWhiteSpace(RemitenteCorreo))
        {
            throw new InvalidOperationException(
                "Smtp:RemitenteCorreo es obligatorio.");
        }
    }
}
