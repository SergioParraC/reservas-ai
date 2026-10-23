/** Estado transaccional de una reserva, espejo del enum de la API. */
export enum EstadoReserva {
  Confirmada = 1,
  Cancelada = 2,
  CanceladaPorNoShow = 3
}

/** Franja de la jornada con su disponibilidad calculada por el backend. */
export interface FranjaDisponible {
  inicio: string;
  fin: string;
  disponible: boolean;
  motivo: string | null;
}

/** Disponibilidad de un puesto dentro de una fecha y jornada. */
export interface DisponibilidadPuesto {
  puestoId: string;
  identificador: string;
  espacioId: string;
  espacioNombre: string;
  zonaHoraria: string;
  franjas: FranjaDisponible[];
}

/** Detalle de una reserva confirmada o cancelada. */
export interface ReservaDetalle {
  id: string;
  puestoId: string;
  puestoIdentificador: string;
  espacioId: string;
  espacioNombre: string;
  inicio: string;
  fin: string;
  estado: EstadoReserva;
  creadaEn: string;
  canceladaEn: string | null;
  rowVersion: string;
}

/** Resumen de una reserva del titular para el listado paginado. */
export interface ReservaResumen {
  id: string;
  puestoId: string;
  puestoIdentificador: string;
  espacioNombre: string;
  inicio: string;
  fin: string;
  estado: EstadoReserva;
  creadaEn: string;
  canceladaEn: string | null;
  cancelable: boolean;
}

/** Página de reservas devuelta por `GET /api/reservas/me`. */
export interface PaginaReservas {
  elementos: ReservaResumen[];
  total: number;
  pagina: number;
  tamano: number;
}

/** Cuerpo del alta de una reserva: la franja se reenvía tal como la entrega la API. */
export interface SolicitudReserva {
  puestoId: string;
  inicio: string;
  fin: string;
}

/** Filtros de la consulta de disponibilidad. */
export interface FiltrosDisponibilidad {
  fecha: string;
  duracionMinutos?: number | null;
}

/** Filtros de la agenda propia. */
export interface FiltrosMisReservas {
  estado?: EstadoReserva | null;
  desde?: string | null;
  hasta?: string | null;
  pagina: number;
  tamano: number;
}

export const ESTADOS_RESERVA: ReadonlyArray<{ valor: EstadoReserva; etiqueta: string }> = [
  { valor: EstadoReserva.Confirmada, etiqueta: 'Confirmada' },
  { valor: EstadoReserva.Cancelada, etiqueta: 'Cancelada' },
  { valor: EstadoReserva.CanceladaPorNoShow, etiqueta: 'Liberada por inasistencia' }
];

export const DURACIONES: ReadonlyArray<{ valor: number; etiqueta: string }> = [
  { valor: 30, etiqueta: '30 minutos' },
  { valor: 60, etiqueta: '1 hora' },
  { valor: 90, etiqueta: '1 hora 30 minutos' },
  { valor: 120, etiqueta: '2 horas' }
];

/** Etiqueta legible del estado de la reserva. */
export function etiquetaEstadoReserva(estado: EstadoReserva): string {
  return ESTADOS_RESERVA.find((item) => item.valor === estado)?.etiqueta ?? 'Desconocido';
}

/** Etiqueta legible del motivo por el que una franja no está disponible. */
export function etiquetaMotivo(motivo: string | null): string {
  switch (motivo) {
    case 'Ocupada':
      return 'Ocupada';
    case 'FueraDeAntelacion':
      return 'Fuera de antelación';
    case 'FueraDeHorizonte':
      return 'Fuera del horizonte';
    default:
      return '';
  }
}
