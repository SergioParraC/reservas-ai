import { CalendarioVigente, FranjaNoOperativa } from '../../administracion/configuracion/models/calendario.models';
import { previsualizarOcurrencias } from './serie.preview';
import { PatronSeleccion, TipoRecurrencia } from './serie.models';

const DOMINGO: FranjaNoOperativa = {
  id: 'franja-domingo',
  diaSemana: 0,
  fechaEspecifica: null,
  horaInicio: '00:00:00',
  horaFin: '23:59:00',
  esFeriado: false,
  motivo: 'Cierre dominical',
  activa: true,
  creadoEn: '2026-07-01T08:00:00-05:00',
  rowVersion: 'AAAA'
};

const SABADO: FranjaNoOperativa = {
  id: 'franja-sabado',
  diaSemana: 6,
  fechaEspecifica: null,
  horaInicio: '13:00:00',
  horaFin: '21:00:00',
  esFeriado: false,
  motivo: 'Mantenimiento',
  activa: true,
  creadoEn: '2026-07-01T08:00:00-05:00',
  rowVersion: 'BBBB'
};

const CALENDARIO: CalendarioVigente = {
  tienePeriodoActivo: true,
  periodoActivo: null,
  franjas: [DOMINGO, SABADO]
};

function patron(cambios: Partial<PatronSeleccion> = {}): PatronSeleccion {
  return {
    tipoRecurrencia: TipoRecurrencia.Semanal,
    diaSemana: 0,
    fechasFijas: [],
    horaInicio: '08:00',
    horaFin: '10:00',
    periodoInicio: '2026-08-02',
    periodoFin: '2026-08-30',
    ...cambios
  };
}

describe('previsualizarOcurrencias', () => {
  it('omite los domingos cerrados y conserva los días operativos', () => {
    const ocurrencias = previsualizarOcurrencias(patron(), CALENDARIO);

    expect(ocurrencias.length).toBe(5);
    expect(ocurrencias.every((ocurrencia) => ocurrencia.omitida)).toBeTrue();
    expect(ocurrencias[0].motivo).toBe('Cierre dominical');
  });

  it('marca el conflicto parcial del sábado con su fecha y causa', () => {
    const ocurrencias = previsualizarOcurrencias(
      patron({ diaSemana: 6, horaInicio: '12:00', horaFin: '14:00' }),
      CALENDARIO
    );

    const conflicto = ocurrencias.find((ocurrencia) => ocurrencia.conflicto);

    expect(conflicto).toBeDefined();
    expect(conflicto!.fecha).toBe('2026-08-08');
    expect(conflicto!.motivo).toBe('Mantenimiento');
  });

  it('deja intacto el sábado por la mañana', () => {
    const ocurrencias = previsualizarOcurrencias(
      patron({ diaSemana: 6, horaInicio: '09:00', horaFin: '10:00' }),
      CALENDARIO
    );

    expect(ocurrencias.length).toBeGreaterThan(0);
    expect(ocurrencias.every((ocurrencia) => !ocurrencia.omitida && !ocurrencia.conflicto)).toBeTrue();
  });

  it('bloquea la serie vacía cuando todas las ocurrencias se omiten', () => {
    const ocurrencias = previsualizarOcurrencias(patron(), CALENDARIO);
    const confirmadas = ocurrencias.filter(
      (ocurrencia) => !ocurrencia.omitida && !ocurrencia.conflicto
    );

    expect(confirmadas.length).toBe(0);
  });

  it('genera exactamente las fechas fijas indicadas', () => {
    const ocurrencias = previsualizarOcurrencias(
      patron({
        tipoRecurrencia: TipoRecurrencia.FechasFijas,
        diaSemana: null,
        fechasFijas: ['2026-09-07', '2026-10-19'],
        periodoInicio: '2026-09-01',
        periodoFin: '2026-10-31'
      }),
      CALENDARIO
    );

    expect(ocurrencias.map((ocurrencia) => ocurrencia.fecha)).toEqual(['2026-09-07', '2026-10-19']);
  });
});
