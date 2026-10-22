using Backend.Application.Autenticacion;

namespace Backend.Application.Common;

/// <summary>
/// Resultado de un caso de uso de autenticación: o entrega el token y el perfil,
/// o identifica la clase de fallo. Nunca transporta datos del almacén de identidad.
/// </summary>
public sealed class ResultadoAutenticacion
{
    private ResultadoAutenticacion(bool exitoso, ErrorAutenticacion error, TokenEmitido? token, PerfilUsuario? perfil)
    {
        Exitoso = exitoso;
        Error = error;
        Token = token;
        Perfil = perfil;
    }

    /// <summary>Indica si la autenticación se completó y se emitió un token.</summary>
    public bool Exitoso { get; }

    /// <summary>Clase de fallo cuando <see cref="Exitoso"/> es <see langword="false"/>.</summary>
    public ErrorAutenticacion Error { get; }

    /// <summary>Token emitido cuando la operación es exitosa.</summary>
    public TokenEmitido? Token { get; }

    /// <summary>Perfil del usuario autenticado cuando la operación es exitosa.</summary>
    public PerfilUsuario? Perfil { get; }

    /// <summary>Crea un resultado exitoso con el token y el perfil emitidos.</summary>
    public static ResultadoAutenticacion Exito(TokenEmitido token, PerfilUsuario perfil)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(perfil);

        return new ResultadoAutenticacion(true, ErrorAutenticacion.Ninguno, token, perfil);
    }

    /// <summary>Crea un resultado fallido para la clase de error indicada.</summary>
    public static ResultadoAutenticacion Fallo(ErrorAutenticacion error)
    {
        if (error == ErrorAutenticacion.Ninguno)
        {
            throw new ArgumentOutOfRangeException(nameof(error), error, "Un resultado fallido requiere una clase de error distinta de Ninguno.");
        }

        return new ResultadoAutenticacion(false, error, null, null);
    }
}
