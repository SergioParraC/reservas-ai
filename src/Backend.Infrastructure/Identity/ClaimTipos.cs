namespace Backend.Infrastructure.Identity;

/// <summary>
/// Nombres de los claims del contrato de identidad compartido por los esquemas local y federado.
/// Se usan tal cual en el token (sin mapeo de tipos de claim).
/// </summary>
public static class ClaimTipos
{
    /// <summary>Identificador interno del usuario.</summary>
    public const string Sub = "sub";

    /// <summary>Identificador único del token.</summary>
    public const string Jti = "jti";

    /// <summary>Correo institucional.</summary>
    public const string Email = "email";

    /// <summary>Nombre completo del portador.</summary>
    public const string Nombre = "name";

    /// <summary>Roles concedidos al portador.</summary>
    public const string Roles = "roles";

    /// <summary>Identificador inmutable de la identidad federada (Entra ID).</summary>
    public const string Oid = "oid";

    /// <summary>Identificador del inquilino emisor (Entra ID).</summary>
    public const string Tid = "tid";

    /// <summary>Marca interna del esquema que autenticó la solicitud.</summary>
    public const string Esquema = "esquema";
}
