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
