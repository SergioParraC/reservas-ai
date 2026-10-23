import {
  bloqueosDelDia,
  CalendarioVigente,
  esDiaCompleto,
  FranjaNoOperativa,
  horaAMinutos
} from '../../administracion/configuracion/models/calendario.models';
import { fechaHoraIso, generarFechasPatron, OcurrenciaPreview, PatronSeleccion } from './serie.models';

/** Indica si el bloqueo se cruza con la franja del patrón. */
function seCruza(franja: FranjaNoOperativa, horaInicio: string, horaFin: string): boolean {
  const inicioBloqueo = horaAMinutos(franja.horaInicio);
  const finBloqueo = horaAMinutos(franja.horaFin);

  return inicioBloqueo < horaAMinutos(horaFin) && horaAMinutos(horaInicio) < finBloqueo;
}

/**
 * Vista previa de las ocurrencias del patrón con el calendario vigente: omite los días cerrados,
 * marca los cruces parciales y deja intactas las fechas operativas.
 */
export function previsualizarOcurrencias(
  patron: PatronSeleccion,
  calendario: CalendarioVigente | null
): OcurrenciaPreview[] {
  return generarFechasPatron(patron).map((fecha) => {
    const bloqueos = bloqueosDelDia(calendario, fecha);
    const cierre = bloqueos.find(esDiaCompleto);
    const cruce = cierre ? null : bloqueos.find((franja) => seCruza(franja, patron.horaInicio, patron.horaFin));

    return {
      fecha,
      inicio: fechaHoraIso(fecha, patron.horaInicio),
      fin: fechaHoraIso(fecha, patron.horaFin),
      omitida: Boolean(cierre),
      conflicto: Boolean(cruce),
      motivo: cierre?.motivo ?? cruce?.motivo ?? null
    } satisfies OcurrenciaPreview;
  });
}
