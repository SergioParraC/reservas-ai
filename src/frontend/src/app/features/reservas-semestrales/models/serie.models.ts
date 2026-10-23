import { fechaIso, sumarDias } from '../../administracion/configuracion/models/calendario.models';

/** Cadencia de la serie, espejo del enum de la API. */
export enum TipoRecurrencia {
  Semanal = 1,
  Bisemanal = 2,
  FechasFijas = 3
}

export const TIPOS_RECURRENCIA: ReadonlyArray<{ valor: TipoRecurrencia; etiqueta: string }> = [
  { valor: TipoRecurrencia.Semanal, etiqueta: 'Semanal' },
  { valor: TipoRecurrencia.Bisemanal, etiqueta: 'Bisemanal' },
  { valor: TipoRecurrencia.FechasFijas, etiqueta: 'Fechas fijas' }
];

/** Ocurrencia confirmada u omitida por el calendario. */
export interface OcurrenciaSerie {
  inicio: string;
  fin: string;
  omitida: boolean;
  motivo: string | null;
}

/** Detalle de una serie recién creada. */
export interface SerieDetalle {
  id: string;
  espacioId: string;
  espacioNombre: string;
  materia: string;
  tipoRecurrencia: TipoRecurrencia;
  diaSemana: number | null;
  fechasFijas: string[];
  horaInicio: string;
  horaFin: string;
  periodoInicio: string;
  periodoFin: string;
  creadaEn: string;
  totalOcurrencias: number;
  ocurrencias: OcurrenciaSerie[];
}

/** Resumen de una serie en el listado propio. */
export interface SerieResumen {
  id: string;
  espacioId: string;
  espacioNombre: string;
  materia: string;
  tipoRecurrencia: TipoRecurrencia;
  diaSemana: number | null;
  horaInicio: string;
  horaFin: string;
  periodoInicio: string;
  periodoFin: string;
  totalOcurrencias: number;
  creadaEn: string;
}

/** Página de series. */
export interface PaginaSeries {
  elementos: SerieResumen[];
  total: number;
  pagina: number;
  tamano: number;
}

/** Patrón declarado por el docente. */
export interface PatronSeleccion {
  tipoRecurrencia: TipoRecurrencia;
  diaSemana: number | null;
  fechasFijas: string[];
  horaInicio: string;
  horaFin: string;
  periodoInicio: string;
  periodoFin: string;
}

/** Cuerpo del alta de una serie. */
export interface SolicitudSerie extends PatronSeleccion {
  espacioId: string;
  materia: string;
}

/** Fila de la vista previa calculada en el cliente. */
export interface OcurrenciaPreview {
  fecha: string;
  inicio: string;
  fin: string;
  omitida: boolean;
  conflicto: boolean;
  motivo: string | null;
}

/** Nombre del tipo de recurrencia que espera la API. */
export function nombreTipoRecurrencia(valor: TipoRecurrencia): string {
  return TipoRecurrencia[valor] ?? 'Semanal';
}

/** Etiqueta legible del tipo de recurrencia. */
export function etiquetaTipoRecurrencia(valor: TipoRecurrencia): string {
  return TIPOS_RECURRENCIA.find((tipo) => tipo.valor === valor)?.etiqueta ?? 'Semanal';
}

/** Convierte una fecha `yyyy-MM-dd` en `Date` local a mediodía para evitar saltos de zona. */
export function fechaLocal(fecha: string): Date {
  return new Date(`${fecha}T12:00:00`);
}

/** Compone un instante ISO conservando la hora local del navegador. */
export function fechaHoraIso(fecha: string, hora: string): string {
  const [horas, minutos] = hora.split(':').map((parte) => Number(parte));
  const referencia = fechaLocal(fecha);
  const desplazamiento = -referencia.getTimezoneOffset();
  const signo = desplazamiento >= 0 ? '+' : '-';
  const absoluto = Math.abs(desplazamiento);
  const offset = `${signo}${String(Math.floor(absoluto / 60)).padStart(2, '0')}:${String(
    absoluto % 60
  ).padStart(2, '0')}`;

  return `${fecha}T${String(horas).padStart(2, '0')}:${String(minutos).padStart(2, '0')}:00${offset}`;
}

/** Genera las fechas candidatas del patrón dentro del periodo semestral. */
export function generarFechasPatron(patron: PatronSeleccion): string[] {
  if (!patron.periodoInicio || !patron.periodoFin) {
    return [];
  }

  if (patron.tipoRecurrencia === TipoRecurrencia.FechasFijas) {
    return [...patron.fechasFijas]
      .filter((fecha) => fecha >= patron.periodoInicio && fecha <= patron.periodoFin)
      .sort();
  }

  if (patron.diaSemana === null) {
    return [];
  }

  const paso = patron.tipoRecurrencia === TipoRecurrencia.Bisemanal ? 14 : 7;
  const fin = fechaLocal(patron.periodoFin);
  let cursor = fechaLocal(patron.periodoInicio);

  while (cursor.getDay() !== patron.diaSemana) {
    cursor = sumarDias(cursor, 1);

    if (cursor > fin) {
      return [];
    }
  }

  const fechas: string[] = [];

  while (cursor <= fin) {
    fechas.push(fechaIso(cursor));
    cursor = sumarDias(cursor, paso);
  }

  return fechas;
}
