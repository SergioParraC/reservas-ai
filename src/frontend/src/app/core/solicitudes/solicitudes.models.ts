/** Estado del ciclo de vida de una solicitud, espejo del enum de la API. */
export enum EstadoSolicitud {
  Radicado = 1,
  EnRevision = 2,
  Aprobado = 3,
  Rechazado = 4
}

/** Estados con su etiqueta legible. */
export const ESTADOS_SOLICITUD: ReadonlyArray<{ valor: EstadoSolicitud; etiqueta: string }> = [
  { valor: EstadoSolicitud.Radicado, etiqueta: 'Radicado' },
  { valor: EstadoSolicitud.EnRevision, etiqueta: 'En revisión' },
  { valor: EstadoSolicitud.Aprobado, etiqueta: 'Aprobado' },
  { valor: EstadoSolicitud.Rechazado, etiqueta: 'Rechazado' }
];

/** Página de resultados de la API. */
export interface PaginaSolicitudes<T> {
  elementos: T[];
  total: number;
  pagina: number;
  tamano: number;
}

/** Resumen de una solicitud en el historial propio. */
export interface SolicitudResumen {
  id: string;
  espacioId: string;
  espacioNombre: string;
  inicio: string;
  fin: string;
  estado: EstadoSolicitud;
  motivoRechazo: string | null;
  radicadaEn: string;
}

/** Elemento de la bandeja de aprobaciones. */
export interface SolicitudBandejaItem {
  id: string;
  espacioId: string;
  espacioNombre: string;
  solicitanteId: string;
  solicitanteNombre: string;
  solicitanteCorreo: string;
  inicio: string;
  fin: string;
  materia: string;
  estado: EstadoSolicitud;
  radicadaEn: string;
}

/** Detalle completo de una solicitud, sin la ruta física del pre-informe. */
export interface SolicitudDetalle {
  id: string;
  espacioId: string;
  espacioNombre: string;
  tipoAprobacion: number;
  tipoModalidadPrestamo: number;
  solicitanteId: string;
  solicitanteNombre: string;
  solicitanteCorreo: string;
  inicio: string;
  fin: string;
  materia: string;
  docenteResponsable: string;
  justificacion: string;
  estado: EstadoSolicitud;
  radicadaEn: string;
  revisadaEn: string | null;
  revisorId: string | null;
  motivoRechazo: string | null;
  archivoNombreOriginal: string;
  archivoTamanoBytes: number;
  esTerminal: boolean;
}

/** Respuesta del alta de una solicitud. */
export interface SolicitudRadicada {
  id: string;
  espacioId: string;
  estado: EstadoSolicitud;
  inicio: string;
  fin: string;
  radicadaEn: string;
  archivoNombreOriginal: string;
  archivoTamanoBytes: number;
}

/** Datos del formulario de radicación con su pre-informe. */
export interface SolicitudRadicacion {
  espacioId: string;
  inicio: string;
  fin: string;
  materia: string;
  docenteResponsable: string;
  justificacion: string;
  archivo: File;
}

/** Filtros del historial propio. */
export interface FiltrosSolicitudes {
  estado?: EstadoSolicitud | null;
  pagina: number;
  tamano: number;
}

/** Filtros combinables de la bandeja de aprobaciones. */
export interface FiltrosBandeja {
  estado?: EstadoSolicitud | null;
  espacioId?: string | null;
  desde?: string | null;
  hasta?: string | null;
  pagina: number;
  tamano: number;
}

/** Cambio de estado solicitado al revisor. */
export interface ComandoEstadoSolicitud {
  estado: EstadoSolicitud;
  motivo?: string | null;
}

/** Nombres de estado que espera la API en los cuerpos de solicitud. */
export function nombreEstadoSolicitud(valor: EstadoSolicitud): string {
  return EstadoSolicitud[valor] ?? 'Radicado';
}

/** Etiqueta legible del estado. */
export function etiquetaEstadoSolicitud(valor: EstadoSolicitud): string {
  return ESTADOS_SOLICITUD.find((estado) => estado.valor === valor)?.etiqueta ?? 'Desconocido';
}

/** Tamaño legible en megabytes de un archivo. */
export function tamanoLegible(bytes: number): string {
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
