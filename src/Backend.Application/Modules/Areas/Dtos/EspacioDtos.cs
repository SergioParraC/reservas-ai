using Backend.Domain.Enums;

namespace Backend.Application.Modules.Areas.Dtos;

/// <summary>Puesto individual expuesto en las respuestas administrativas.</summary>
public sealed record PuestoDetalle(
    Guid Id,
    string Identificador,
    string? DescripcionEquipo,
    EstadoUnidad Estado,
    string CodigoQR,
    DateTimeOffset CreadoEn);

/// <summary>Resumen de un área para el listado administrativo.</summary>
public sealed record EspacioResumen(
    Guid Id,
    string Nombre,
    string Ubicacion,
    CategoriaEspacio Categoria,
    int Aforo,
    TipoAprobacion TipoAprobacion,
    EstadoUnidad Estado,
    string CodigoQR,
    string? FotografiaUrl,
    int PuestosActivos,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn,
    string RowVersion);

/// <summary>Detalle administrativo de un área con sus puestos.</summary>
public sealed record EspacioDetalle(
    Guid Id,
    string Nombre,
    string Ubicacion,
    CategoriaEspacio Categoria,
    int Aforo,
    TipoAprobacion TipoAprobacion,
    EstadoUnidad Estado,
    string CodigoQR,
    string? FotografiaUrl,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn,
    string RowVersion,
    IReadOnlyList<PuestoDetalle> Puestos);

/// <summary>Elemento del catálogo público; omite el código QR y la ruta interna del archivo.</summary>
public sealed record EspacioCatalogo(
    Guid Id,
    string Nombre,
    string Ubicacion,
    CategoriaEspacio Categoria,
    int Aforo,
    TipoAprobacion TipoAprobacion,
    string? FotografiaUrl);

/// <summary>Detalle público de un área activa.</summary>
public sealed record EspacioPublico(
    Guid Id,
    string Nombre,
    string Ubicacion,
    CategoriaEspacio Categoria,
    int Aforo,
    TipoAprobacion TipoAprobacion,
    string? FotografiaUrl,
    int PuestosActivos,
    IReadOnlyList<PuestoPublico> Puestos);

/// <summary>Puesto publicado en el detalle del catálogo, sin su código QR.</summary>
public sealed record PuestoPublico(
    Guid Id,
    string Identificador,
    string? DescripcionEquipo,
    EstadoUnidad Estado);

/// <summary>Datos de entrada para el alta de un área.</summary>
public sealed record ComandoCrearEspacio(
    string Nombre,
    string Ubicacion,
    CategoriaEspacio Categoria,
    int Aforo,
    TipoAprobacion TipoAprobacion);

/// <summary>Datos de entrada para la edición de un área; nunca incluye el código QR.</summary>
public sealed record ComandoActualizarEspacio(
    string Nombre,
    string Ubicacion,
    CategoriaEspacio Categoria,
    int Aforo,
    TipoAprobacion TipoAprobacion,
    EstadoUnidad Estado,
    string? RowVersion);

/// <summary>Datos de entrada para el alta de un puesto.</summary>
public sealed record ComandoCrearPuesto(string Identificador, string? DescripcionEquipo);

/// <summary>Datos de entrada para la edición de un puesto; nunca incluye el código QR.</summary>
public sealed record ComandoActualizarPuesto(string Identificador, string? DescripcionEquipo, EstadoUnidad Estado);

/// <summary>Filtros del listado administrativo de áreas.</summary>
public sealed record FiltroEspaciosAdmin(
    string? Q,
    CategoriaEspacio? Categoria,
    EstadoUnidad? Estado,
    int Pagina,
    int Tamano);

/// <summary>Filtros combinables del catálogo público.</summary>
public sealed record FiltroCatalogo(
    string? Q,
    CategoriaEspacio? Categoria,
    int? AforoMinimo,
    int? AforoMaximo,
    int Pagina,
    int Tamano);

/// <summary>Opciones de dibujo del SVG de un código QR.</summary>
public sealed record OpcionesQr(double LadoMilimetros = 40, int ModuloSilencio = 4);

/// <summary>Una etiqueta de impresión de la plantilla QR.</summary>
public sealed record EtiquetaQr(string Codigo, string Nombre, string Ubicacion, string? Detalle);

/// <summary>Hoja de etiquetas a componer.</summary>
public sealed record PlantillaQr(
    IReadOnlyList<EtiquetaQr> Etiquetas,
    int Hoja,
    int EtiquetasPorHoja,
    string Fecha);

/// <summary>Documento SVG generado y metadatos de paginación.</summary>
public sealed record PlantillaGenerada(string Svg, int TotalHojas, string NombreArchivo);

/// <summary>Selección de unidades para la plantilla de impresión.</summary>
public sealed record ComandoPlantillaQr(
    IReadOnlyCollection<Guid> EspacioIds,
    bool IncluirPuestos,
    int Hoja);
