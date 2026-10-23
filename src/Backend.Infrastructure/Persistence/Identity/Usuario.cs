using Backend.Domain.Enums;
using Backend.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace Backend.Infrastructure.Persistence.Identity;

/// <summary>
/// Perfil institucional que representa a cualquier actor de la comunidad universitaria.
/// Vive en la capa de infraestructura porque hereda de <see cref="IdentityUser{TKey}"/>,
/// una dependencia de framework que no puede filtrarse al dominio.
/// </summary>
public sealed class Usuario : IdentityUser<Guid>
{
    /// <summary>Constructor sin parámetros requerido por EF Core.</summary>
    private Usuario()
    {
    }

    private Usuario(string nombreCompleto, string correoInstitucional, TipoActor tipoActor, DateTimeOffset creadoEn)
    {
        Id = Guid.NewGuid();
        NombreCompleto = nombreCompleto;
        TipoActor = tipoActor;
        Activo = true;
        CreadoEn = creadoEn;
        UserName = correoInstitucional;
        Email = correoInstitucional;
    }

    /// <summary>Nombre y apellidos del actor institucional.</summary>
    public string NombreCompleto { get; private set; } = string.Empty;

    /// <summary>Tipo de actor descriptivo del perfil.</summary>
    public TipoActor TipoActor { get; private set; }

    /// <summary>Claim <c>oid</c> inmutable de Entra ID; nulo para cuentas locales.</summary>
    public string? IdentificadorFederado { get; private set; }

    /// <summary>Indica si la cuenta puede iniciar sesión.</summary>
    public bool Activo { get; private set; }

    /// <summary>Momento en que se creó el perfil.</summary>
    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Momento del último acceso exitoso.</summary>
    public DateTimeOffset? UltimoAccesoEn { get; private set; }

    /// <summary>Crea un perfil institucional activo y valida los datos mínimos exigidos.</summary>
    public static Usuario Crear(string nombreCompleto, string correoInstitucional, TipoActor tipoActor, DateTimeOffset creadoEn)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            throw new DomainException(MensajesIdentidad.NombreCompletoRequerido);
        }

        if (string.IsNullOrWhiteSpace(correoInstitucional))
        {
            throw new DomainException(MensajesIdentidad.CorreoInstitucionalRequerido);
        }

        return new Usuario(nombreCompleto.Trim(), correoInstitucional.Trim(), tipoActor, creadoEn);
    }

    /// <summary>Vincula la cuenta con su identidad federada inmutable (<c>oid</c>).</summary>
    public void VincularIdentidadFederada(string identificadorFederado)
    {
        if (string.IsNullOrWhiteSpace(identificadorFederado))
        {
            throw new DomainException(MensajesIdentidad.IdentificadorFederadoRequerido);
        }

        IdentificadorFederado = identificadorFederado.Trim();
    }

    /// <summary>Retira el vínculo federado sin afectar la contraseña local.</summary>
    public void RetirarIdentidadFederada() => IdentificadorFederado = null;

    /// <summary>Sincroniza el tipo de actor descriptivo cuando cambia el rol vigente.</summary>
    public void SincronizarTipoActor(TipoActor tipoActor) => TipoActor = tipoActor;

    /// <summary>Registra la marca de tiempo del último acceso autorizado.</summary>
    public void RegistrarAcceso(DateTimeOffset momento) => UltimoAccesoEn = momento;

    /// <summary>Deshabilita la cuenta e impide nuevos inicios de sesión.</summary>
    public void Desactivar() => Activo = false;

    /// <summary>Habilita nuevamente la cuenta.</summary>
    public void Activar() => Activo = true;
}
