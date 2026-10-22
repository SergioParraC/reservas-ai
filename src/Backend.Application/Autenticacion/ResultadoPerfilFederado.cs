using Backend.Application.Common;

namespace Backend.Application.Autenticacion;

/// <summary>
/// Resultado de resolver un perfil institucional a partir de una identidad federada.
/// </summary>
public sealed class ResultadoPerfilFederado
{
    private ResultadoPerfilFederado(PerfilUsuario? perfil, ErrorAutenticacion error)
    {
        Perfil = perfil;
        Error = error;
    }

    /// <summary>Perfil institucional resuelto cuando la operación es exitosa.</summary>
    public PerfilUsuario? Perfil { get; }

    /// <summary>Clase de fallo cuando no se pudo resolver el perfil.</summary>
    public ErrorAutenticacion Error { get; }

    /// <summary>Indica si se resolvió un perfil habilitado.</summary>
    public bool Exitoso => Perfil is not null;

    /// <summary>Crea un resultado exitoso con el perfil y sus roles efectivos.</summary>
    public static ResultadoPerfilFederado Exito(PerfilUsuario perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        return new ResultadoPerfilFederado(perfil, ErrorAutenticacion.Ninguno);
    }

    /// <summary>Crea un resultado fallido para la clase de error indicada.</summary>
    public static ResultadoPerfilFederado Fallo(ErrorAutenticacion error)
    {
        if (error == ErrorAutenticacion.Ninguno)
        {
            throw new ArgumentOutOfRangeException(nameof(error), error, "Un resultado fallido requiere una clase de error distinta de Ninguno.");
        }

        return new ResultadoPerfilFederado(null, error);
    }
}
