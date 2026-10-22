namespace Backend.Application.Autenticacion;

/// <summary>Token de acceso emitido, listo para serializar en la respuesta HTTP.</summary>
public sealed record TokenEmitido(string Token, string TipoToken, DateTimeOffset ExpiraEn, IReadOnlyCollection<string> Roles)
{
    /// <summary>Esquema de autorización esperado en el encabezado <c>Authorization</c>.</summary>
    public const string EsquemaBearer = "Bearer";

    /// <summary>Crea un token con el esquema Bearer.</summary>
    public static TokenEmitido Bearer(string token, DateTimeOffset expiraEn, IReadOnlyCollection<string> roles) =>
        new(token, EsquemaBearer, expiraEn, roles);
}
