/** Periodo académico del calendario institucional. */
export interface PeriodoAcademico {
  id: string;
  nombre: string;
  fechaInicio: string;
  fechaFin: string;
  estadoActivo: boolean;
  creadoEn: string;
  actualizadoEn: string;
  rowVersion: string;
}

/** Franja en la que la institución no opera. */
export interface FranjaNoOperativa {
  id: string;
  diaSemana: number | null;
  fechaEspecifica: string | null;
  horaInicio: string;
  horaFin: string;
  esFeriado: boolean;
  motivo: string;
  activa: boolean;
  creadoEn: string;
  rowVersion: string;
}

/** Calendario vigente expuesto a cualquier usuario autenticado. */
export interface CalendarioVigente {
  tienePeriodoActivo: boolean;
  periodoActivo: PeriodoAcademico | null;
  franjas: FranjaNoOperativa[];
}

/** Alta de un periodo académico. */
export interface SolicitudPeriodo {
  nombre: string;
  fechaInicio: string;
  fechaFin: string;
}

/** Edición de un periodo académico con su token de concurrencia. */
export interface SolicitudActualizarPeriodo extends SolicitudPeriodo {
  rowVersion?: string | null;
}

/** Alta o edición de una franja no operativa. */
export interface SolicitudFranja {
  diaSemana?: string | null;
  fechaEspecifica?: string | null;
  horaInicio: string;
  horaFin: string;
  esFeriado: boolean;
  motivo: string;
  rowVersion?: string | null;
}

/** Días de la semana con su valor numérico y su nombre para la API. */
export const DIAS_SEMANA: ReadonlyArray<{ valor: number; nombre: string; etiqueta: string }> = [
  { valor: 0, nombre: 'Sunday', etiqueta: 'Domingo' },
  { valor: 1, nombre: 'Monday', etiqueta: 'Lunes' },
  { valor: 2, nombre: 'Tuesday', etiqueta: 'Martes' },
  { valor: 3, nombre: 'Wednesday', etiqueta: 'Miércoles' },
  { valor: 4, nombre: 'Thursday', etiqueta: 'Jueves' },
  { valor: 5, nombre: 'Friday', etiqueta: 'Viernes' },
  { valor: 6, nombre: 'Saturday', etiqueta: 'Sábado' }
];

/** Próximo día cerrado dentro de los siguientes siete días. */
export interface ProximoBloqueo {
  fecha: string;
  motivo: string;
  diaCompleto: boolean;
}

/** Minutos transcurridos desde medianoche a partir de `HH:mm[:ss]`. */
export function horaAMinutos(hora: string | null | undefined): number {
  if (!hora) {
    return 0;
  }

  const [horas, minutos] = hora.split(':').map((parte) => Number(parte));
  return (horas || 0) * 60 + (minutos || 0);
}

/** Normaliza una hora de la API a `HH:mm`. */
export function horaCorta(hora: string | null | undefined): string {
  const minutos = horaAMinutos(hora);
  const horas = Math.floor(minutos / 60);
  return `${String(horas).padStart(2, '0')}:${String(minutos % 60).padStart(2, '0')}`;
}

/** Indica si la franja cubre el día completo (00:00 a 23:59). */
export function esDiaCompleto(franja: FranjaNoOperativa): boolean {
  return horaAMinutos(franja.horaInicio) === 0 && horaAMinutos(franja.horaFin) >= 23 * 60 + 59;
}

/** Nombre del día de la semana que espera la API. */
export function nombreDia(valor: number | null | undefined): string | null {
  return DIAS_SEMANA.find((dia) => dia.valor === valor)?.nombre ?? null;
}

/** Etiqueta legible del día de la semana. */
export function etiquetaDia(valor: number | null | undefined): string {
  return DIAS_SEMANA.find((dia) => dia.valor === valor)?.etiqueta ?? '—';
}

/** Fecha local en formato `yyyy-MM-dd`. */
export function fechaIso(fecha: Date): string {
  const anio = fecha.getFullYear();
  const mes = String(fecha.getMonth() + 1).padStart(2, '0');
  const dia = String(fecha.getDate()).padStart(2, '0');
  return `${anio}-${mes}-${dia}`;
}

/** Suma días a una fecha conservando la hora local. */
export function sumarDias(fecha: Date, dias: number): Date {
  const copia = new Date(fecha);
  copia.setDate(copia.getDate() + dias);
  return copia;
}

/** Indica si la franja no operativa aplica a la fecha indicada. */
export function aplicaAFecha(franja: FranjaNoOperativa, fecha: Date): boolean {
  if (!franja.activa) {
    return false;
  }

  return franja.fechaEspecifica === fechaIso(fecha) || franja.diaSemana === fecha.getDay();
}

/** Franjas no operativas que aplican a una fecha `yyyy-MM-dd`. */
export function bloqueosDelDia(
  calendario: CalendarioVigente | null,
  fecha: string
): FranjaNoOperativa[] {
  if (!calendario) {
    return [];
  }

  const referencia = new Date(`${fecha}T12:00:00`);

  return calendario.franjas.filter((franja) => aplicaAFecha(franja, referencia));
}

/** Indica si la fecha queda cerrada por completo según el calendario. */
export function esDiaCerrado(calendario: CalendarioVigente | null, fecha: string): boolean {
  return bloqueosDelDia(calendario, fecha).some(esDiaCompleto);
}


/**
 * Busca el primer día no operativo dentro de la próxima semana para que las pantallas de
 * reserva lo anticipen antes de enviar la solicitud.
 */
export function proximaSemanaNoOperativa(
  calendario: CalendarioVigente | null,
  desde: Date = new Date()
): ProximoBloqueo | null {
  if (!calendario) {
    return null;
  }

  for (let indice = 0; indice < 7; indice += 1) {
    const fecha = sumarDias(desde, indice);
    const delDia = calendario.franjas.filter((franja) => aplicaAFecha(franja, fecha));

    if (delDia.length === 0) {
      continue;
    }

    return {
      fecha: fechaIso(fecha),
      motivo: delDia[0].motivo,
      diaCompleto: delDia.some(esDiaCompleto)
    };
  }

  return null;
}
