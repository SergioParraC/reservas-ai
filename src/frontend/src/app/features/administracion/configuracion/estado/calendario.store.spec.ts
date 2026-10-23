import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { AVISO_FRANJA_GUARDADA, AVISO_PERIODO_ACTIVADO, CalendarioStore } from './calendario.store';
import { CalendarioVigente, FranjaNoOperativa, PeriodoAcademico } from '../models/calendario.models';
import { CalendarioApi } from '../services/calendario.api';

const PERIODO: PeriodoAcademico = {
  id: 'periodo-1',
  nombre: '2026-2',
  fechaInicio: '2026-08-01',
  fechaFin: '2026-12-15',
  estadoActivo: false,
  creadoEn: '2026-07-01T08:00:00-05:00',
  actualizadoEn: '2026-07-01T08:00:00-05:00',
  rowVersion: 'AAAA'
};

const FRANJA: FranjaNoOperativa = {
  id: 'franja-1',
  diaSemana: 0,
  fechaEspecifica: null,
  horaInicio: '00:00:00',
  horaFin: '23:59:00',
  esFeriado: false,
  motivo: 'Cierre dominical',
  activa: true,
  creadoEn: '2026-07-01T08:00:00-05:00',
  rowVersion: 'BBBB'
};

function problema(slug: string): HttpErrorResponse {
  return new HttpErrorResponse({
    status: 409,
    error: {
      type: `https://api.reservas-espacios.edu/problems/${slug}`,
      title: 'Conflicto',
      status: 409,
      detail: 'Conflicto de calendario.'
    }
  });
}

describe('CalendarioStore', () => {
  let api: jasmine.SpyObj<CalendarioApi>;
  let store: CalendarioStore;

  const vigente = (): CalendarioVigente => ({
    tienePeriodoActivo: true,
    periodoActivo: { ...PERIODO, estadoActivo: true },
    franjas: [FRANJA]
  });

  beforeEach(() => {
    api = jasmine.createSpyObj<CalendarioApi>('CalendarioApi', [
      'listarPeriodos',
      'crearPeriodo',
      'actualizarPeriodo',
      'activarPeriodo',
      'listarFranjas',
      'crearFranja',
      'actualizarFranja',
      'eliminarFranja',
      'vigente'
    ]);

    TestBed.configureTestingModule({ providers: [{ provide: CalendarioApi, useValue: api }] });

    store = TestBed.inject(CalendarioStore);
    api.listarPeriodos.and.returnValue(of([PERIODO]));
    api.listarFranjas.and.returnValue(of([FRANJA]));
    api.vigente.and.returnValue(of(vigente()));
  });

  it('activa el periodo y recarga el calendario vigente', () => {
    api.activarPeriodo.and.returnValue(of({ ...PERIODO, estadoActivo: true }));

    store.activarPeriodo('periodo-1').subscribe();

    expect(api.activarPeriodo).toHaveBeenCalledWith('periodo-1');
    expect(store.aviso()).toBe(AVISO_PERIODO_ACTIVADO);
    expect(store.periodoActivo()?.nombre).toBe('2026-2');
    expect(api.listarPeriodos).toHaveBeenCalled();
    expect(api.vigente).toHaveBeenCalled();
  });

  it('traduce el 409 periodo-solapado', () => {
    api.crearPeriodo.and.returnValue(throwError(() => problema('periodo-solapado')));

    store.crearPeriodo({ nombre: '2026-3', fechaInicio: '2026-05-01', fechaFin: '2026-09-30' }).subscribe();

    expect(store.error()).toContain('se cruza con otro ya registrado');
    expect(store.guardando()).toBeFalse();
  });

  it('invalida y recarga las franjas tras guardar una nueva', () => {
    api.crearFranja.and.returnValue(of(FRANJA));

    store
      .crearFranja({
        diaSemana: 'Sunday',
        fechaEspecifica: null,
        horaInicio: '00:00',
        horaFin: '23:59',
        esFeriado: false,
        motivo: 'Cierre dominical'
      })
      .subscribe();

    expect(store.aviso()).toBe(AVISO_FRANJA_GUARDADA);
    expect(store.franjas().length).toBe(1);
    expect(api.listarFranjas).toHaveBeenCalledTimes(1);
  });

  it('traduce el 409 franja-duplicada', () => {
    api.crearFranja.and.returnValue(throwError(() => problema('franja-duplicada')));

    store
      .crearFranja({
        diaSemana: 'Sunday',
        fechaEspecifica: null,
        horaInicio: '00:00',
        horaFin: '23:59',
        esFeriado: false,
        motivo: 'Cierre dominical'
      })
      .subscribe();

    expect(store.error()).toContain('mismo criterio y horario');
  });

  it('elimina una franja y recarga las listas', () => {
    api.eliminarFranja.and.returnValue(of(void 0));

    let eliminada = false;
    store.eliminarFranja('franja-1').subscribe((resultado) => (eliminada = resultado));

    expect(eliminada).toBeTrue();
    expect(api.eliminarFranja).toHaveBeenCalledWith('franja-1');
    expect(api.listarFranjas).toHaveBeenCalled();
  });
});
