using Backend.Application.Autenticacion;
using Backend.Application.Common;

namespace Backend.Application.Interfaces;

/// <summary>
/// Puerto de validación de un token de Entra ID presentado en el cuerpo de la solicitud
/// de vinculación. Usa exactamente las mismas reglas que el esquema federado de la API.
/// </summary>
public interface IValidadorTokenEntra
{
    /// <summary>Valida la firma, el inquilino, la audiencia y la vigencia del token.</summary>
    Task<Resultado<IdentidadFederada>> ValidarAsync(string token, CancellationToken cancellationToken = default);
}
