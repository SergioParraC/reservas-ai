/** Estado transaccional de una reserva, espejo del enum de la API. */
export enum EstadoReserva {
  Confirmada = 1,
  Cancelada = 2,
  NoShow = 3,
  EnUso = 4,
  Finalizada = 5
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

/** Disponibilidad de un área completa dentro de una fecha y jornada. */
export interface DisponibilidadEspacio {
  espacioId: string;
  espacioNombre: string;
  zonaHoraria: string;
  franjas: FranjaDisponible[];
}

/** Detalle de una reserva con su franja efectiva, la planificada y las marcas de asistencia. */
export interface ReservaDetalle {
  id: string;
  puestoId: string;
  puestoIdentificador: string;
  espacioId: string;
  espacioNombre: string;
  inicio: string;
  fin: string;
  finPlanificado: string;
  estado: EstadoReserva;
  creadaEn: string;
  canceladaEn: string | null;
  fechaCheckIn: string | null;
  fechaCheckOut: string | null;
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
  finPlanificado: string;
  estado: EstadoReserva;
  creadaEn: string;
  canceladaEn: string | null;
  fechaCheckIn: string | null;
  fechaCheckOut: string | null;
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
  { valor: EstadoReserva.NoShow, etiqueta: 'No-show' },
  { valor: EstadoReserva.EnUso, etiqueta: 'En uso' },
  { valor: EstadoReserva.Finalizada, etiqueta: 'Finalizada' }
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

/** Indica si la reserva admite el ingreso presencial (solo desde `Confirmada`). */
export function puedeRegistrarIngreso(estado: EstadoReserva): boolean {
  return estado === EstadoReserva.Confirmada;
}

/** Indica si la reserva admite la salida (solo desde `EnUso`, sin ventana horaria). */
export function puedeRegistrarSalida(estado: EstadoReserva): boolean {
  return estado === EstadoReserva.EnUso;
}

/** Indica si la franja sigue bloqueada por la reserva. */
export function estaVigente(estado: EstadoReserva): boolean {
  return estado === EstadoReserva.Confirmada || estado === EstadoReserva.EnUso;
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
    case 'FueraDeOperacion':
      return 'Fuera de operación';
    case 'FueraDePeriodoAcademico':
      return 'Fuera del periodo académico';
    default:
      return motivo ? 'No disponible' : '';
  }
}
