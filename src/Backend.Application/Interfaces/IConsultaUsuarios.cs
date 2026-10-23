using Backend.Application.Common;
using Backend.Application.Modules.Usuarios.Dtos;

namespace Backend.Application.Interfaces;

/// <summary>Puerto de lectura de cuentas para la consola administrativa.</summary>
public interface IConsultaUsuarios
{
    /// <summary>Lista cuentas con filtros por texto, rol y estado de vínculo federado.</summary>
    Task<PaginaResultado<UsuarioAdministrativo>> ListarAsync(FiltroUsuarios filtro, CancellationToken cancellationToken = default);
}
