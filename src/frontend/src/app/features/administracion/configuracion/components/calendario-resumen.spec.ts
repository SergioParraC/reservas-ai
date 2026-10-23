import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { CalendarioStore } from '../estado/calendario.store';
import { CalendarioApi } from '../services/calendario.api';
import { FranjaNoOperativa, PeriodoAcademico } from '../models/calendario.models';
import { CalendarioResumen } from './calendario-resumen';

const PERIODO: PeriodoAcademico = {
  id: 'periodo-1',
  nombre: '2026-2',
  fechaInicio: '2026-08-01',
  fechaFin: '2026-12-15',
  estadoActivo: true,
  creadoEn: '2026-07-01T08:00:00-05:00',
  actualizadoEn: '2026-07-01T08:00:00-05:00',
  rowVersion: 'AAAA'
};

const DOMINGO: FranjaNoOperativa = {
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

describe('CalendarioResumen', () => {
  let api: jasmine.SpyObj<CalendarioApi>;
  let store: CalendarioStore;

  beforeEach(() => {
    api = jasmine.createSpyObj<CalendarioApi>('CalendarioApi', [
      'listarPeriodos',
      'listarFranjas',
      'vigente'
    ]);
    api.listarPeriodos.and.returnValue(of([PERIODO]));
    api.listarFranjas.and.returnValue(of([DOMINGO]));
    api.vigente.and.returnValue(
      of({ tienePeriodoActivo: true, periodoActivo: PERIODO, franjas: [DOMINGO] })
    );

    TestBed.configureTestingModule({ providers: [{ provide: CalendarioApi, useValue: api }] });
    store = TestBed.inject(CalendarioStore);
  });

  it('anuncia el periodo activo y el próximo cierre semanal', () => {
    store.vigente.set({ tienePeriodoActivo: true, periodoActivo: PERIODO, franjas: [DOMINGO] });

    const fixture = TestBed.createComponent(CalendarioResumen);
    fixture.detectChanges();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(texto).toContain('2026-2');
    expect(texto).toContain('Cierre dominical');
    expect(store.proximaSemana()?.diaCompleto).toBeTrue();
  });

  it('advierte cuando no hay periodo activo', () => {
    store.vigente.set({ tienePeriodoActivo: false, periodoActivo: null, franjas: [] });

    const fixture = TestBed.createComponent(CalendarioResumen);
    fixture.detectChanges();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(texto).toContain('No hay un periodo académico activo');
    expect(store.proximaSemana()).toBeNull();
  });
});
