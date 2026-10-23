using System.Text;
using Backend.Application.Common;
using Backend.Application.Modules.Areas;
using Backend.Application.Modules.Areas.Dtos;
using Backend.Domain.Enums;
using Backend.Presentation.Api.Contracts;
using Backend.Presentation.Api.Problemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Presentation.Api.Controllers;

/// <summary>
/// Endpoints administrativos de áreas, puestos, fotografías y plantillas QR.
/// Todas las mutaciones exigen el rol <c>Administrativo</c>.
/// </summary>
[ApiController]
[Route("api/espacios")]
[Authorize(Roles = RolesAplicacion.Administrativo)]
public sealed class EspaciosController : ControllerBase
{
    private const string TipoSvg = "image/svg+xml";
    private const string CacheInmutable = "public, max-age=31536000, immutable";

    private readonly ListarEspaciosHandler _listar;
    private readonly ObtenerEspacioHandler _obtener;
    private readonly CrearEspacioHandler _crear;
    private readonly ActualizarEspacioHandler _actualizar;
    private readonly DesactivarEspacioHandler _desactivar;
    private readonly CrearPuestoHandler _crearPuesto;
    private readonly ActualizarPuestoHandler _actualizarPuesto;
    private readonly DesactivarPuestoHandler _desactivarPuesto;
    private readonly SubirFotografiaHandler _subirFotografia;
    private readonly EliminarFotografiaHandler _eliminarFotografia;
    private readonly GenerarQrEspacioHandler _qrEspacio;
    private readonly GenerarPlantillaQrHandler _plantilla;

    /// <summary>Inicializa el controlador con los casos de uso del módulo de áreas.</summary>
    public EspaciosController(
        ListarEspaciosHandler listar,
        ObtenerEspacioHandler obtener,
        CrearEspacioHandler crear,
        ActualizarEspacioHandler actualizar,
        DesactivarEspacioHandler desactivar,
        CrearPuestoHandler crearPuesto,
        ActualizarPuestoHandler actualizarPuesto,
        DesactivarPuestoHandler desactivarPuesto,
        SubirFotografiaHandler subirFotografia,
        EliminarFotografiaHandler eliminarFotografia,
        GenerarQrEspacioHandler qrEspacio,
        GenerarPlantillaQrHandler plantilla)
    {
        _listar = listar ?? throw new ArgumentNullException(nameof(listar));
        _obtener = obtener ?? throw new ArgumentNullException(nameof(obtener));
        _crear = crear ?? throw new ArgumentNullException(nameof(crear));
        _actualizar = actualizar ?? throw new ArgumentNullException(nameof(actualizar));
        _desactivar = desactivar ?? throw new ArgumentNullException(nameof(desactivar));
        _crearPuesto = crearPuesto ?? throw new ArgumentNullException(nameof(crearPuesto));
        _actualizarPuesto = actualizarPuesto ?? throw new ArgumentNullException(nameof(actualizarPuesto));
        _desactivarPuesto = desactivarPuesto ?? throw new ArgumentNullException(nameof(desactivarPuesto));
        _subirFotografia = subirFotografia ?? throw new ArgumentNullException(nameof(subirFotografia));
        _eliminarFotografia = eliminarFotografia ?? throw new ArgumentNullException(nameof(eliminarFotografia));
        _qrEspacio = qrEspacio ?? throw new ArgumentNullException(nameof(qrEspacio));
        _plantilla = plantilla ?? throw new ArgumentNullException(nameof(plantilla));
    }

    /// <summary>Lista áreas con filtros administrativos, incluidas las inactivas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaResultado<EspacioResumen>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? q,
        [FromQuery] string? categoria,
        [FromQuery] string? estado,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 20,
        CancellationToken cancellationToken = default)
    {
        var filtro = new FiltroEspaciosAdmin(
            q,
            LeerEnum<CategoriaEspacio>(categoria),
            LeerEnum<EstadoUnidad>(estado),
            pagina,
            tamano);

        return Ok(await _listar.HandleAsync(filtro, cancellationToken));
    }

    /// <summary>Obtiene el detalle administrativo de un área con sus puestos.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EspacioDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _obtener.HandleAsync(id, cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Registra un área activa y le asigna su código QR inmutable.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EspacioDetalle), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] SolicitudCrearEspacio solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!ParseoContratos.TryParseEnum<CategoriaEspacio>(solicitud.Categoria, out var categoria))
        {
            return EnumInvalido("categoria", "La categoría del área no es válida.");
        }

        if (!ParseoContratos.TryParseEnum<TipoAprobacion>(solicitud.TipoAprobacion, out var tipoAprobacion))
        {
            return EnumInvalido("tipoAprobacion", "El tipo de aprobación del área no es válido.");
        }

        if (!ParseoContratos.TryParseEnum<TipoModalidadPrestamo>(solicitud.TipoModalidadPrestamo, out var modalidad))
        {
            return EnumInvalido("tipoModalidadPrestamo", "La modalidad de préstamo del área no es válida.");
        }

        if (!ParseoContratos.TryParseEnum<TipoElementoPrestamo>(solicitud.TipoElementoPrestamo, out var elemento))
        {
            return EnumInvalido("tipoElementoPrestamo", "El elemento de préstamo del área no es válido.");
        }

        var resultado = await _crear.HandleAsync(
            new ComandoCrearEspacio(
                solicitud.Nombre,
                solicitud.Ubicacion,
                categoria,
                solicitud.Aforo,
                tipoAprobacion,
                modalidad,
                elemento),
            cancellationToken);

        return resultado is { Exitoso: true, Valor: { } detalle }
            ? CreatedAtAction(nameof(Obtener), new { id = detalle.Id }, detalle)
            : CatalogoProblemas.DesdeResultado(
                resultado,
                HttpContext,
                ProblemasSolicitud.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>Actualiza los datos del área conservando su código QR.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EspacioDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(
        Guid id,
        [FromBody] SolicitudActualizarEspacio solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!ParseoContratos.TryParseEnum<CategoriaEspacio>(solicitud.Categoria, out var categoria))
        {
            return EnumInvalido("categoria", "La categoría del área no es válida.");
        }

        if (!ParseoContratos.TryParseEnum<TipoAprobacion>(solicitud.TipoAprobacion, out var tipoAprobacion))
        {
            return EnumInvalido("tipoAprobacion", "El tipo de aprobación del área no es válido.");
        }

        if (!ParseoContratos.TryParseEnum<EstadoUnidad>(solicitud.Estado, out var estado))
        {
            return EnumInvalido("estado", "El estado del área no es válido.");
        }

        if (!ParseoContratos.TryParseEnum<TipoModalidadPrestamo>(solicitud.TipoModalidadPrestamo, out var modalidad))
        {
            return EnumInvalido("tipoModalidadPrestamo", "La modalidad de préstamo del área no es válida.");
        }

        if (!ParseoContratos.TryParseEnum<TipoElementoPrestamo>(solicitud.TipoElementoPrestamo, out var elemento))
        {
            return EnumInvalido("tipoElementoPrestamo", "El elemento de préstamo del área no es válido.");
        }

        var resultado = await _actualizar.HandleAsync(
            id,
            new ComandoActualizarEspacio(
                solicitud.Nombre,
                solicitud.Ubicacion,
                categoria,
                solicitud.Aforo,
                tipoAprobacion,
                estado,
                solicitud.RowVersion,
                modalidad,
                elemento),
            cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(
                resultado,
                HttpContext,
                ProblemasSolicitud.SlugConflicto(resultado.Mensaje));
    }

    /// <summary>Da de baja lógica un área conservando su código QR.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desactivar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _desactivar.HandleAsync(id, cancellationToken);

        return resultado.Exitoso
            ? NoContent()
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Agrega un puesto al área validando el aforo y el identificador.</summary>
    [HttpPost("{id:guid}/puestos")]
    [ProducesResponseType(typeof(PuestoDetalle), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearPuesto(
        Guid id,
        [FromBody] SolicitudCrearPuesto solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var resultado = await _crearPuesto.HandleAsync(
            id,
            new ComandoCrearPuesto(solicitud.Identificador, solicitud.DescripcionEquipo),
            cancellationToken);

        return resultado is { Exitoso: true, Valor: { } puesto }
            ? StatusCode(StatusCodes.Status201Created, puesto)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Actualiza un puesto del área conservando su código QR.</summary>
    [HttpPut("{id:guid}/puestos/{puestoId:guid}")]
    [ProducesResponseType(typeof(PuestoDetalle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarPuesto(
        Guid id,
        Guid puestoId,
        [FromBody] SolicitudActualizarPuesto solicitud,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!ParseoContratos.TryParseEnum<EstadoUnidad>(solicitud.Estado, out var estado))
        {
            return EnumInvalido("estado", "El estado del puesto no es válido.");
        }

        var resultado = await _actualizarPuesto.HandleAsync(
            id,
            puestoId,
            new ComandoActualizarPuesto(solicitud.Identificador, solicitud.DescripcionEquipo, estado),
            cancellationToken);

        return resultado.Exitoso
            ? Ok(resultado.Valor)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Da de baja lógica un puesto del área.</summary>
    [HttpDelete("{id:guid}/puestos/{puestoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesactivarPuesto(Guid id, Guid puestoId, CancellationToken cancellationToken)
    {
        var resultado = await _desactivarPuesto.HandleAsync(id, puestoId, cancellationToken);

        return resultado.Exitoso
            ? NoContent()
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Carga la fotografía del área en el almacén administrado.</summary>
    [HttpPost("{id:guid}/imagen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubirImagen(Guid id, [FromForm] IFormFile? imagen, CancellationToken cancellationToken)
    {
        if (imagen is null)
        {
            return EnumInvalido("imagen", "Debe adjuntar una imagen.");
        }

        await using var flujo = imagen.OpenReadStream();

        var resultado = await _subirFotografia.HandleAsync(
            id,
            flujo,
            imagen.FileName,
            imagen.ContentType,
            imagen.Length,
            cancellationToken);

        return resultado.Exitoso
            ? Ok(new { urlFotografia = resultado.Valor })
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Elimina la fotografía del área y el archivo del almacén.</summary>
    [HttpDelete("{id:guid}/imagen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarImagen(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _eliminarFotografia.HandleAsync(id, cancellationToken);

        return resultado.Exitoso
            ? NoContent()
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Descarga el código QR del área como SVG vectorial inmutable.</summary>
    [HttpGet("{id:guid}/qr.svg")]
    [Produces(TipoSvg)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DescargarQr(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _qrEspacio.HandleAsync(id, cancellationToken);

        return resultado is { Exitoso: true, Valor: { } svg }
            ? Svg(svg)
            : CatalogoProblemas.DesdeResultado(resultado, HttpContext);
    }

    /// <summary>Genera una hoja A4 con las etiquetas QR de las unidades seleccionadas.</summary>
    [HttpGet("qr/plantilla")]
    [Produces(TipoSvg)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DescargarPlantilla(
        [FromQuery] Guid[]? espacioIds,
        [FromQuery] bool incluirPuestos = true,
        [FromQuery] int hoja = 1,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _plantilla.HandleAsync(
            new ComandoPlantillaQr(espacioIds ?? [], incluirPuestos, hoja),
            cancellationToken);

        if (resultado is not { Exitoso: true, Valor: { } plantilla })
        {
            return CatalogoProblemas.DesdeResultado(resultado, HttpContext);
        }

        Response.Headers["X-Total-Hojas"] = plantilla.TotalHojas.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers.CacheControl = "no-store";

        return File(Encoding.UTF8.GetBytes(plantilla.Svg), TipoSvg, plantilla.NombreArchivo);
    }

    private FileContentResult Svg(string contenido)
    {
        Response.Headers.CacheControl = CacheInmutable;

        return File(Encoding.UTF8.GetBytes(contenido), TipoSvg);
    }

    private static TEnum? LeerEnum<TEnum>(string? texto)
        where TEnum : struct, Enum =>
        ParseoContratos.TryParseEnum<TEnum>(texto, out var valor) ? valor : null;

    private ObjectResult EnumInvalido(string campo, string mensaje) =>
        CatalogoProblemas.Crear(
            StatusCodes.Status400BadRequest,
            "solicitud-invalida",
            "Solicitud inválida",
            mensaje,
            HttpContext,
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [campo] = [mensaje] });
}
