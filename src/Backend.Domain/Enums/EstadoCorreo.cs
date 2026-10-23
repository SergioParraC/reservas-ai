namespace Backend.Domain.Enums;

/// <summary>
/// Estado de un correo de la cola de salida. La cola es persistida para no perder la
/// notificación cuando el servidor SMTP no está disponible.
/// </summary>
public enum EstadoCorreo
{
    /// <summary>El correo espera su despacho.</summary>
    Pendiente = 1,

    /// <summary>El correo fue entregado al servidor SMTP.</summary>
    Enviado = 2,

    /// <summary>El correo agotó sus reintentos y se conserva para inspección.</summary>
    Fallido = 3
}
