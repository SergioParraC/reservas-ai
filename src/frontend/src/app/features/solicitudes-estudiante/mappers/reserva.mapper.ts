import { HttpErrorResponse } from '@angular/common/http';

import { leerProblema } from '../../../core/interceptors/problem-details.interceptor';

/** Zona horaria institucional con la que se muestran las franjas. */
export const ZONA_HORARIA = 'America/Bogota';

const FORMATO_FECHA_API = new Intl.DateTimeFormat('en-CA', {
  timeZone: ZONA_HORARIA,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit'
});

const FORMATO_HORA = new Intl.DateTimeFormat('es-CO', {
  timeZone: ZONA_HORARIA,
  hour: '2-digit',
  minute: '2-digit',
  hour12: false
});

const FORMATO_FECHA = new Intl.DateTimeFormat('es-CO', {
  timeZone: ZONA_HORARIA,
  weekday: 'short',
  day: '2-digit',
  month: 'short',
  year: 'numeric'
});

/** Fecha `yyyy-MM-dd` de la zona institucional para un instante dado. */
export function aFechaIso(instante: Date = new Date()): string {
  return FORMATO_FECHA_API.format(instante);
}

/** Fecha de la zona institucional desplazada en días. */
export function sumarDias(instante: Date, dias: number): Date {
  return new Date(instante.getTime() + dias * 24 * 60 * 60 * 1000);
}

/** Hora local de la zona institucional, por ejemplo `10:00`. */
export function formatearHora(instante: string): string {
  return FORMATO_HORA.format(new Date(instante));
}

/** Fecha local de la zona institucional, por ejemplo `lun, 03 nov 2026`. */
export function formatearFecha(instante: string): string {
  return FORMATO_FECHA.format(new Date(instante));
}

/** Rango horario legible de una reserva o franja. */
export function formatearRango(inicio: string, fin: string): string {
  return `${formatearHora(inicio)} - ${formatearHora(fin)}`;
}

/** Traduce el problema RFC 7807 de reservas a un mensaje accionable. */
export function mensajeDeErrorReserva(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const problema = leerProblema(error);
    const tipo = problema?.type ?? '';

    if (tipo.endsWith('/franja-no-disponible')) {
      return 'La franja ya fue ocupada por otra persona. Actualice la disponibilidad e intente de nuevo.';
    }

    if (tipo.endsWith('/limite-reservas-alcanzado')) {
      return problema?.detail ?? 'Alcanzó el máximo de reservas activas. Cancele una para continuar.';
    }

    if (tipo.endsWith('/fuera-de-plazo')) {
      return problema?.detail ?? 'La reserva ya no puede cancelarse: falta menos del plazo mínimo.';
    }

    if (tipo.endsWith('/puesto-no-reservable')) {
      return problema?.detail ?? 'El puesto no admite préstamo directo.';
    }

    if (problema?.detail) {
      return problema.detail;
    }
  }

  return 'No fue posible completar la operación. Intente nuevamente.';
}
