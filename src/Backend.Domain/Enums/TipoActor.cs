namespace Backend.Domain.Enums;

/// <summary>
/// Tipo de actor institucional que representa un usuario dentro de la plataforma.
/// Es un atributo descriptivo del perfil: la autorización se resuelve con los roles de Identity.
/// </summary>
public enum TipoActor
{
    /// <summary>
    /// Personal administrativo y de soporte: parametriza el campus, aprueba solicitudes
    /// y atiende incidencias de la plataforma.
    /// </summary>
    Administrativo = 1,

    /// <summary>Docente que solicita espacios y aprueba reservas de laboratorio.</summary>
    Docente = 2,

    /// <summary>Estudiante que reserva puestos individuales.</summary>
    Estudiante = 3
}
