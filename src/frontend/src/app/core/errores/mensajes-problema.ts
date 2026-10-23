import { HttpErrorResponse } from '@angular/common/http';

import { leerProblema } from '../interceptors/problem-details.interceptor';

/** Mensajes por `type` estable del contrato RFC 7807. */
export const MENSAJES_PROBLEMA: Record<string, string> = {
  'periodo-solapado': 'El periodo se cruza con otro ya registrado.',
  'periodo-con-ocupacion': 'El periodo tiene reservas o solicitudes vigentes: no puede acortarse.',
  'franja-duplicada': 'Ya existe una franja no operativa con el mismo criterio y horario.',
  'franja-no-operativa': 'La franja se cruza con una franja no operativa del calendario.',
  'fuera-de-periodo-academico': 'La franja queda fuera del periodo académico activo.',
  'sin-periodo-activo': 'No existe un periodo académico activo. Active uno antes de continuar.',
  'colision-serie': 'La serie se cruza con la agenda vigente del área en una o más fechas.',
  'serie-sin-ocurrencias': 'La serie no genera ninguna ocurrencia operativa en el periodo solicitado.',
  'modalidad-incompatible': 'La modalidad de préstamo del área no admite esta operación.',
  'modalidad-con-ocupacion': 'El área tiene ocupación futura: no es posible cambiar la modalidad.',
  'espacio-no-disponible': 'El área no está disponible: se encuentra inactiva.',
  'espacio-no-solicitable': 'El área no admite solicitudes: su aprobación es automática.',
  'solicitud-ajena': 'No tiene competencia sobre esta solicitud.',
  'transicion-invalida': 'La transición de estado no es válida para la solicitud.',
  'franja-no-disponible': 'La franja ya está ocupada en la agenda del área.',
  'limite-solicitudes-alcanzado': 'Alcanzó el máximo de solicitudes activas.',
  'archivo-no-valido': 'El archivo adjunto no es un PDF válido.',
  'archivo-demasiado-grande': 'El archivo adjunto supera el tamaño máximo de 5 MB.',
  'limite-reservas-alcanzado': 'Alcanzó el máximo de reservas activas.',
  'fuera-de-plazo': 'La reserva ya no puede cancelarse: falta menos del plazo mínimo.'
};

/** Slug estable del problema (`colision-serie`, `archivo-no-valido`, …). */
export function slugProblema(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return '';
  }

  return (leerProblema(error)?.type ?? '').split('/').pop() ?? '';
}

/** Mensaje legible del error, priorizando el catálogo de slugs sobre el `detail`. */
export function mensajeDeProblema(
  error: unknown,
  mensajes: Record<string, string> = MENSAJES_PROBLEMA
): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Ocurrió un error inesperado. Intente nuevamente.';
  }

  const problema = leerProblema(error);
  const slug = slugProblema(error);

  return (
    mensajes[slug] ??
    problema?.detail ??
    'Ocurrió un error inesperado. Intente nuevamente.'
  );
}
