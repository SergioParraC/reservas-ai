/** Cuerpo de `POST /api/reservas/{id}/check-in`: el código impreso en la placa del puesto. */
export interface SolicitudCheckIn {
  codigoQr: string;
}

/**
 * Datos de la reserva que la pantalla presencial necesita: los comparten el resumen de la agenda
 * (`ReservaResumen`), el detalle (`ReservaDetalle`) y las respuestas de check-in/check-out.
 */
export interface ReservaAsistencia {
  id: string;
  puestoId: string;
  puestoIdentificador: string;
  espacioNombre: string;
  inicio: string;
  fin: string;
  finPlanificado: string;
  estado: number;
  fechaCheckIn: string | null;
  fechaCheckOut: string | null;
}

/** Ventana admitida para escanear: `inicio - tolerancia` a `inicio + tolerancia`. */
export interface VentanaCheckIn {
  desde: Date;
  hasta: Date;
  toleranciaMinutos: number;
}

/** Paso del flujo presencial que la página usa para decidir qué mostrar. */
export type PasoCheckIn = 'cargando' | 'ingreso' | 'en-uso' | 'finalizada' | 'sin-reserva';

/** Motivos de rechazo del check-in y del check-out con su `type` estable de RFC 7807. */
export const SLUGS_ASISTENCIA = {
  fueraDeVentana: 'check-in-fuera-de-ventana',
  codigoInvalido: 'check-in-codigo-invalido',
  transicionInvalida: 'transicion-invalida',
  reservaAjena: 'reserva-ajena'
} as const;

/** Calcula la ventana simétrica de check-in a partir del inicio de la franja. */
export function calcularVentanaCheckIn(inicio: string, toleranciaMinutos: number): VentanaCheckIn {
  const centro = new Date(inicio).getTime();
  const margen = toleranciaMinutos * 60 * 1000;

  return {
    desde: new Date(centro - margen),
    hasta: new Date(centro + margen),
    toleranciaMinutos
  };
}

/** Indica si el instante cae dentro de la ventana admitida (extremos inclusive). */
export function dentroDeVentana(ventana: VentanaCheckIn, instante: Date = new Date()): boolean {
  const momento = instante.getTime();

  return momento >= ventana.desde.getTime() && momento <= ventana.hasta.getTime();
}
