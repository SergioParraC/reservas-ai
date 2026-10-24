import { HttpErrorResponse } from '@angular/common/http';
import { TestBed, discardPeriodicTasks, fakeAsync, tick } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';

import { OcupacionActual } from '../models/monitoreo.models';
import { MonitoreoApi } from '../services/monitoreo.api';
import { ERROR_OCUPACION, INTERVALO_ACTUALIZACION_MS, MonitoreoStore } from './monitoreo.store';

const OCUPACION: OcupacionActual = {
  generadoEn: '2026-11-03T10:30:00-05:00',
  toleranciaMinutos: 15,
  totales: {
    aforo: 20,
    puestosActivos: 20,
    enUso: 12,
    pendientesCheckIn: 3,
    sesionesSinCerrar: 0,
    libres: 5,
    porcentajeOcupacion: 60
  },
  areas: [
    {
      espacioId: 'espacio-1',
      nombre: 'Laboratorio A',
      aforo: 20,
      puestosActivos: 20,
      enUso: 12,
      pendientesCheckIn: 3,
      sesionesSinCerrar: 0,
      libres: 5,
      porcentajeOcupacion: 60,
      proximaLiberacion: '2026-11-03T11:00:00-05:00',
      enCurso: []
    }
  ]
};

describe('MonitoreoStore', () => {
  let api: jasmine.SpyObj<MonitoreoApi>;
  let store: MonitoreoStore;

  const errorRed = () =>
    new HttpErrorResponse({ status: 500, error: { title: 'Error', status: 500 } });

  beforeEach(() => {
    api = jasmine.createSpyObj<MonitoreoApi>('MonitoreoApi', ['ocupacionActual']);
    TestBed.configureTestingModule({ providers: [{ provide: MonitoreoApi, useValue: api }] });

    store = TestBed.inject(MonitoreoStore);
  });

  it('consulta de inmediato y refresca cada 15 segundos', () => {
    fakeAsync(() => {
      let ciclos = 0;
      api.ocupacionActual.and.callFake(() => {
        ciclos += 1;
        return of(OCUPACION);
      });

      const recibidas: Array<OcupacionActual | null> = [];
      store.iniciar().subscribe((ocupacion) => recibidas.push(ocupacion));

      tick(0);

      expect(ciclos).toBe(1);
      expect(store.ocupacion()?.totales.enUso).toBe(12);
      expect(recibidas.length).toBe(1);

      tick(INTERVALO_ACTUALIZACION_MS);

      expect(ciclos).toBe(2);
      expect(recibidas.length).toBe(2);

      discardPeriodicTasks();
    });
  });

  it('marca la carga mientras espera la primera respuesta', () => {
    const respuesta = new Subject<OcupacionActual>();
    api.ocupacionActual.and.returnValue(respuesta.asObservable());

    fakeAsync(() => {
      store.iniciar().subscribe();
      tick(0);

      expect(store.cargando()).toBeTrue();
      expect(store.ocupacion()).toBeNull();

      respuesta.next(OCUPACION);
      respuesta.complete();

      expect(store.cargando()).toBeFalse();
      expect(store.ocupacion()).not.toBeNull();

      discardPeriodicTasks();
    });
  });

  it('publica el error del ciclo y se recupera en el siguiente', () => {
    api.ocupacionActual.and.returnValues(throwError(errorRed), of(OCUPACION));

    fakeAsync(() => {
      store.iniciar().subscribe();
      tick(0);

      expect(store.error()).toBe(ERROR_OCUPACION);
      expect(store.ocupacion()).toBeNull();
      expect(store.cargando()).toBeFalse();

      tick(INTERVALO_ACTUALIZACION_MS);

      expect(store.error()).toBeNull();
      expect(store.ocupacion()?.toleranciaMinutos).toBe(15);

      discardPeriodicTasks();
    });
  });

  it('actualiza actualizadoEn en cada ciclo', () => {
    api.ocupacionActual.and.returnValue(of(OCUPACION));

    fakeAsync(() => {
      store.iniciar().subscribe();
      tick(0);

      const primera = store.actualizadoEn();
      expect(primera).not.toBeNull();

      tick(INTERVALO_ACTUALIZACION_MS);

      const segunda = store.actualizadoEn();
      expect(segunda!.getTime()).toBeGreaterThan(primera!.getTime());

      discardPeriodicTasks();
    });
  });
});
