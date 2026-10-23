/** Categorías de área espejo del enum del dominio. */
export enum CategoriaEspacio {
  Aula = 1,
  Laboratorio = 2,
  Auditorio = 3,
  SalaComputo = 4,
  Biblioteca = 5,
  Deportivo = 6,
  Oficina = 7,
  Otro = 8
}

/** Responsable de aprobar las reservas de un área. */
export enum TipoAprobacion {
  Automatica = 1,
  Docente = 2,
  Administrativo = 3
}

/** Estado del ciclo de vida de un área o puesto. */
export enum EstadoUnidad {
  Activo = 1,
  Inactivo = 2
}

/** Forma en que se presta el área, espejo del enum de la API. */
export enum TipoModalidadPrestamo {
  AreaCompleta = 1,
  PuestoIndividual = 2,
  SubZonaCompartida = 3
}

/** Elemento físico que se presta en el área. */
export enum TipoElementoPrestamo {
  EquipoComputo = 1,
  LaboratorioEspecializado = 2,
  ZonaDescansoJuegos = 3,
  Auditorio = 4
}

/** Modalidades con su etiqueta legible. */
export const MODALIDADES: ReadonlyArray<{ valor: TipoModalidadPrestamo; etiqueta: string }> = [
  { valor: TipoModalidadPrestamo.AreaCompleta, etiqueta: 'Área completa' },
  { valor: TipoModalidadPrestamo.PuestoIndividual, etiqueta: 'Puesto individual' },
  { valor: TipoModalidadPrestamo.SubZonaCompartida, etiqueta: 'Sub-zona compartida' }
];

/** Elementos con su etiqueta legible. */
export const ELEMENTOS: ReadonlyArray<{ valor: TipoElementoPrestamo; etiqueta: string }> = [
  { valor: TipoElementoPrestamo.EquipoComputo, etiqueta: 'Equipo de cómputo' },
  { valor: TipoElementoPrestamo.LaboratorioEspecializado, etiqueta: 'Laboratorio especializado' },
  { valor: TipoElementoPrestamo.ZonaDescansoJuegos, etiqueta: 'Zona de descanso o juegos' },
  { valor: TipoElementoPrestamo.Auditorio, etiqueta: 'Auditorio' }
];

/** Matriz institucional de combinaciones válidas entre modalidad y elemento. */
export const COMBINACIONES_VALIDAS: Record<TipoModalidadPrestamo, readonly TipoElementoPrestamo[]> = {
  [TipoModalidadPrestamo.AreaCompleta]: [
    TipoElementoPrestamo.LaboratorioEspecializado,
    TipoElementoPrestamo.Auditorio,
    TipoElementoPrestamo.ZonaDescansoJuegos
  ],
  [TipoModalidadPrestamo.PuestoIndividual]: [TipoElementoPrestamo.EquipoComputo],
  [TipoModalidadPrestamo.SubZonaCompartida]: [
    TipoElementoPrestamo.EquipoComputo,
    TipoElementoPrestamo.ZonaDescansoJuegos
  ]
};


/** Política espejo de `Espacios:AforoMaximo` publicada por la API. */
export const AFORO_MAXIMO = 500;

export const CATEGORIAS: ReadonlyArray<{ valor: CategoriaEspacio; etiqueta: string }> = [
  { valor: CategoriaEspacio.Aula, etiqueta: 'Aula' },
  { valor: CategoriaEspacio.Laboratorio, etiqueta: 'Laboratorio' },
  { valor: CategoriaEspacio.Auditorio, etiqueta: 'Auditorio' },
  { valor: CategoriaEspacio.SalaComputo, etiqueta: 'Sala de cómputo' },
  { valor: CategoriaEspacio.Biblioteca, etiqueta: 'Biblioteca' },
  { valor: CategoriaEspacio.Deportivo, etiqueta: 'Deportivo' },
  { valor: CategoriaEspacio.Oficina, etiqueta: 'Oficina' },
  { valor: CategoriaEspacio.Otro, etiqueta: 'Otro' }
];

export const TIPOS_APROBACION: ReadonlyArray<{ valor: TipoAprobacion; etiqueta: string }> = [
  { valor: TipoAprobacion.Automatica, etiqueta: 'Automática' },
  { valor: TipoAprobacion.Docente, etiqueta: 'Docente' },
  { valor: TipoAprobacion.Administrativo, etiqueta: 'Administrativo' }
];

export const ESTADOS: ReadonlyArray<{ valor: EstadoUnidad; etiqueta: string }> = [
  { valor: EstadoUnidad.Activo, etiqueta: 'Activo' },
  { valor: EstadoUnidad.Inactivo, etiqueta: 'Inactivo' }
];

/** Página de resultados de la API. */
export interface PaginaResultado<T> {
  elementos: T[];
  total: number;
  pagina: number;
  tamano: number;
}

/** Puesto individual en respuestas administrativas. */
export interface PuestoDetalle {
  id: string;
  identificador: string;
  descripcionEquipo: string | null;
  estado: EstadoUnidad;
  codigoQR: string;
  creadoEn: string;
}

/** Resumen de área en el listado administrativo. */
export interface EspacioResumen {
  id: string;
  nombre: string;
  ubicacion: string;
  categoria: CategoriaEspacio;
  aforo: number;
  tipoAprobacion: TipoAprobacion;
  estado: EstadoUnidad;
  codigoQR: string;
  fotografiaUrl: string | null;
  puestosActivos: number;
  creadoEn: string;
  actualizadoEn: string;
  rowVersion: string;
  tipoModalidadPrestamo: TipoModalidadPrestamo;
  tipoElementoPrestamo: TipoElementoPrestamo;
}

/** Detalle administrativo con sus puestos. */
export interface EspacioDetalle extends Omit<EspacioResumen, 'puestosActivos'> {
  puestos: PuestoDetalle[];
}

/** Elemento del catálogo público (sin código QR ni ruta interna). */
export interface EspacioCatalogo {
  id: string;
  nombre: string;
  ubicacion: string;
  categoria: CategoriaEspacio;
  aforo: number;
  tipoAprobacion: TipoAprobacion;
  fotografiaUrl: string | null;
  tipoModalidadPrestamo: TipoModalidadPrestamo;
  tipoElementoPrestamo: TipoElementoPrestamo;
}

/** Puesto publicado en el detalle del catálogo, sin su código QR. */
export interface PuestoPublico {
  id: string;
  identificador: string;
  descripcionEquipo: string | null;
  estado: EstadoUnidad;
}

/** Detalle público de un área activa. */
export interface EspacioPublico extends EspacioCatalogo {
  puestosActivos: number;
  puestos: PuestoPublico[];
}

/** Filtros combinables del catálogo. */
export interface FiltrosCatalogo {
  q?: string | null;
  categoria?: CategoriaEspacio | null;
  aforoMinimo?: number | null;
  aforoMaximo?: number | null;
  pagina: number;
  tamano: number;
}

/** Filtros del listado administrativo. */
export interface FiltrosEspaciosAdmin {
  q?: string | null;
  categoria?: CategoriaEspacio | null;
  estado?: EstadoUnidad | null;
  pagina: number;
  tamano: number;
}

/** Alta de un área. */
export interface SolicitudCrearEspacio {
  nombre: string;
  ubicacion: string;
  categoria: CategoriaEspacio;
  aforo: number;
  tipoAprobacion: TipoAprobacion;
  tipoModalidadPrestamo: TipoModalidadPrestamo;
  tipoElementoPrestamo: TipoElementoPrestamo;
}

/** Edición de un área; el código QR nunca se envía. */
export interface SolicitudActualizarEspacio extends SolicitudCrearEspacio {
  estado: EstadoUnidad;
  rowVersion?: string | null;
}

/** Alta o edición de un puesto. */
export interface SolicitudPuesto {
  identificador: string;
  descripcionEquipo?: string | null;
  estado?: EstadoUnidad;
}

/** Descarga de la plantilla QR con su total de hojas. */
export interface PlantillaQrDescarga {
  blob: Blob;
  totalHojas: number;
}

/** Nombre estable del enum que espera la API en los cuerpos de solicitud. */
export function nombreCategoria(valor: CategoriaEspacio): string {
  return CategoriaEspacio[valor] ?? 'Otro';
}

/** Nombre estable del tipo de aprobación. */
export function nombreTipoAprobacion(valor: TipoAprobacion): string {
  return TipoAprobacion[valor] ?? 'Automatica';
}

/** Nombre estable del estado de la unidad. */
export function nombreEstado(valor: EstadoUnidad): string {
  return EstadoUnidad[valor] ?? 'Activo';
}

/** Etiqueta legible de una categoría. */
export function etiquetaCategoria(valor: CategoriaEspacio): string {
  return CATEGORIAS.find((categoria) => categoria.valor === valor)?.etiqueta ?? 'Otro';
}

/** Etiqueta legible de un tipo de aprobación. */
export function etiquetaTipoAprobacion(valor: TipoAprobacion): string {
  return TIPOS_APROBACION.find((tipo) => tipo.valor === valor)?.etiqueta ?? 'Automática';
}

/** Nombre del enum de modalidad que espera la API. */
export function nombreModalidad(valor: TipoModalidadPrestamo): string {
  return TipoModalidadPrestamo[valor] ?? 'AreaCompleta';
}

/** Nombre del enum de elemento que espera la API. */
export function nombreElemento(valor: TipoElementoPrestamo): string {
  return TipoElementoPrestamo[valor] ?? 'LaboratorioEspecializado';
}

/** Etiqueta legible de la modalidad. */
export function etiquetaModalidad(valor: TipoModalidadPrestamo): string {
  return MODALIDADES.find((modalidad) => modalidad.valor === valor)?.etiqueta ?? 'Área completa';
}

/** Etiqueta legible del elemento de préstamo. */
export function etiquetaElemento(valor: TipoElementoPrestamo): string {
  return ELEMENTOS.find((elemento) => elemento.valor === valor)?.etiqueta ?? 'Laboratorio especializado';
}

/** Elementos admitidos para la modalidad indicada. */
export function elementosDe(modalidad: TipoModalidadPrestamo): readonly TipoElementoPrestamo[] {
  return COMBINACIONES_VALIDAS[modalidad] ?? [];
}

/** Indica si la combinación de modalidad y elemento es coherente. */
export function combinacionValida(
  modalidad: TipoModalidadPrestamo,
  elemento: TipoElementoPrestamo
): boolean {
  return elementosDe(modalidad).includes(elemento);
}
