using Backend.Application.Common;
using Backend.Application.Interfaces;
using Backend.Application.Modules.Usuarios.Dtos;
using Backend.Domain.Exceptions;

namespace Backend.Application.Modules.Usuarios;

/// <summary>
/// Vincula la cuenta autenticada con la identidad institucional de Entra ID.
/// Exige coincidencia exacta de correo y que el <c>oid</c> no pertenezca a otra cuenta.
/// </summary>
public sealed class VincularEntraHandler
{
    private readonly IIdentityService _identidad;
    private readonly IValidadorTokenEntra _validador;

    /// <summary>Inicializa el caso de uso con el almacén de identidad y el validador federado.</summary>
    public VincularEntraHandler(IIdentityService identidad, IValidadorTokenEntra validador)
    {
        _identidad = identidad ?? throw new ArgumentNullException(nameof(identidad));
        _validador = validador ?? throw new ArgumentNullException(nameof(validador));
    }

    /// <summary>Valida el token de Entra ID y asocia el <c>oid</c> a la cuenta del portador.</summary>
    public async Task<Resultado<EstadoVinculacion>> HandleAsync(
        Guid usuarioId,
        bool sesionFederada,
        string tokenEntra,
        CancellationToken cancellationToken = default)
    {
        if (sesionFederada)
        {
            return Resultado<EstadoVinculacion>.Fallo(TipoFallo.Conflicto, MensajesIdentidad.SesionYaFederada);
        }

        if (string.IsNullOrWhiteSpace(tokenEntra))
        {
            return Resultado<EstadoVinculacion>.Fallo(
                TipoFallo.Validacion,
                "El token de Entra ID es obligatorio.",
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["tokenEntra"] = ["El token de Entra ID es obligatorio."] });
        }

        var identidad = await _validador.ValidarAsync(tokenEntra, cancellationToken);

        if (!identidad.Exitoso || identidad.Valor is null)
        {
            return Resultado<EstadoVinculacion>.Fallo(
                identidad.TipoFallo == TipoFallo.Ninguno ? TipoFallo.NoAutorizado : identidad.TipoFallo,
                identidad.Mensaje ?? "El token de Entra ID no es válido.");
        }

        var cuenta = await _identidad.BuscarPorIdAsync(usuarioId, cancellationToken);

        if (cuenta is null)
        {
            return Resultado<EstadoVinculacion>.Fallo(TipoFallo.NoEncontrado, "La cuenta autenticada no existe.");
        }

        var oid = identidad.Valor.Oid.Trim();

        if (!string.Equals(cuenta.CorreoInstitucional, identidad.Valor.CorreoInstitucional.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Resultado<EstadoVinculacion>.Fallo(TipoFallo.Conflicto, MensajesIdentidad.CorreoNoCoincide);
        }

        var propietaria = await _identidad.BuscarPorIdentificadorFederadoAsync(oid, cancellationToken);

        if (propietaria is not null && propietaria.Id != usuarioId)
        {
            return Resultado<EstadoVinculacion>.Fallo(TipoFallo.Conflicto, MensajesIdentidad.IdentidadYaVinculada);
        }

        if (!string.IsNullOrWhiteSpace(cuenta.IdentificadorFederado)
            && !string.Equals(cuenta.IdentificadorFederado, oid, StringComparison.Ordinal))
        {
            return Resultado<EstadoVinculacion>.Fallo(
                TipoFallo.Conflicto,
                "La cuenta ya tiene otra identidad institucional vinculada. Desvincúlela antes de asociar la nueva.");
        }

        if (string.Equals(cuenta.IdentificadorFederado, oid, StringComparison.Ordinal))
        {
            return Resultado<EstadoVinculacion>.Exito(new EstadoVinculacion(true, cuenta.CorreoInstitucional));
        }

        var resultado = await _identidad.VincularIdentidadAsync(usuarioId, oid, cancellationToken);

        return resultado.Exitoso
            ? Resultado<EstadoVinculacion>.Exito(new EstadoVinculacion(true, cuenta.CorreoInstitucional))
            : Resultado<EstadoVinculacion>.Fallo(TipoFallo.Conflicto, string.Join(" ", resultado.Errores));
    }
}

/// <summary>Retira el vínculo federado de la cuenta autenticada sin afectar su contraseña local.</summary>
public sealed class DesvincularEntraHandler
{
    private readonly IIdentityService _identidad;

    /// <summary>Inicializa el caso de uso con el almacén de identidad.</summary>
    public DesvincularEntraHandler(IIdentityService identidad)
    {
        _identidad = identidad ?? throw new ArgumentNullException(nameof(identidad));
    }

    /// <summary>Desvincula la identidad institucional; repetir la operación es idempotente.</summary>
    public async Task<Resultado> HandleAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var resultado = await _identidad.DesvincularIdentidadAsync(usuarioId, cancellationToken);

        return resultado.Exitoso
            ? Resultado.Exito()
            : Resultado.Fallo(TipoFallo.Conflicto, string.Join(" ", resultado.Errores));
    }
}
