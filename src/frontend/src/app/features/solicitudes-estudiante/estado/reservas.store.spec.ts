import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { AVISO_RESERVA_CANCELADA, AVISO_RESERVA_CONFIRMADA, ReservasStore } from './reservas.store';
import {
  DisponibilidadPuesto,
  EstadoReserva,
  PaginaReservas,
  ReservaDetalle
} from '../models/reserva.models';
import { ReservasApi } from '../services/reservas.api';

const DISPONIBILIDAD: DisponibilidadPuesto = {
  puestoId: 'puesto-1',
  identificador: 'PC-01',
  espacioId: 'espacio-1',
  espacioNombre: 'Sala de Cómputo 1',
  zonaHoraria: 'America/Bogota',
  franjas: [
    {
      inicio: '2026-11-03T10:00:00-05:00',
      fin: '2026-11-03T11:00:00-05:00',
      disponible: true,
      motivo: null
    }
  ]
};

const RESERVA: ReservaDetalle = {
  id: 'reserva-1',
  puestoId: 'puesto-1',
  puestoIdentificador: 'PC-01',
  espacioId: 'espacio-1',
  espacioNombre: 'Sala de Cómputo 1',
  inicio: '2026-11-03T10:00:00-05:00',
  fin: '2026-11-03T11:00:00-05:00',
  estado: EstadoReserva.Confirmada,
  creadaEn: '2026-11-02T08:00:00-05:00',
  canceladaEn: null,
  rowVersion: 'AAAA'
};

const PAGINA: PaginaReservas = {
  elementos: [
    {
      id: 'reserva-1',
      puestoId: 'puesto-1',
      puestoIdentificador: 'PC-01',
      espacioNombre: 'Sala de Cómputo 1',
      inicio: '2026-11-03T10:00:00-05:00',
      fin: '2026-11-03T11:00:00-05:00',
      estado: EstadoReserva.Confirmada,
      creadaEn: '2026-11-02T08:00:00-05:00',
      canceladaEn: null,
      cancelable: true
    }
  ],
  total: 1,
  pagina: 1,
  tamano: 10
};

function problema(slug: string, detalle: string): HttpErrorResponse {
  return new HttpErrorResponse({
    status: 409,
    statusText: 'Conflict',
    error: {
      type: `https://api.reservas-espacios.edu/problems/${slug}`,
      title: 'Conflicto',
      status: 409,
      detail: detalle
    }
  });
}

describe('ReservasStore', () => {
  let api: jasmine.SpyObj<ReservasApi>;
  let store: ReservasStore;

  beforeEach(() => {
    api = jasmine.createSpyObj<ReservasApi>('ReservasApi', [
      'disponibilidad',
      'crear',
      'misReservas',
      'cancelar'
    ]);

    TestBed.configureTestingModule({
      providers: [{ provide: ReservasApi, useValue: api }]
    });

    store = TestBed.inject(ReservasStore);
  });

  it('carga la disponibilidad del puesto', () => {
    api.disponibilidad.and.returnValue(of(DISPONIBILIDAD));

    store.cargarDisponibilidad('puesto-1', { fecha: '2026-11-03', duracionMinutos: 60 }).subscribe();

    expect(api.disponibilidad).toHaveBeenCalledWith('puesto-1', {
      fecha: '2026-11-03',
      duracionMinutos: 60
    });
    expect(store.disponibilidad()).toEqual(DISPONIBILIDAD);
    expect(store.cargando()).toBeFalse();
  });

  it('traduce el conflicto de franja ocupada', () => {
    api.disponibilidad.and.returnValue(
      throwError(() => problema('franja-no-disponible', 'La franja ya está ocupada.'))
    );

    store.cargarDisponibilidad('puesto-1', { fecha: '2026-11-03' }).subscribe();

    expect(store.error()).toContain('franja ya fue ocupada');
    expect(store.disponibilidad()).toBeNull();
  });

  it('informa el cupo agotado al reservar', () => {
    api.crear.and.returnValue(
      throwError(() => problema('limite-reservas-alcanzado', 'La cuenta alcanzó el máximo de 2 reservas activas.'))
    );

    store
      .reservar({
        puestoId: 'puesto-1',
        inicio: '2026-11-03T10:00:00-05:00',
        fin: '2026-11-03T11:00:00-05:00'
      })
      .subscribe();

    expect(store.error()).toContain('máximo de 2 reservas activas');
    expect(store.aviso()).toBeNull();
  });

  it('confirma la reserva y publica el aviso', () => {
    api.crear.and.returnValue(of(RESERVA));

    let creada: ReservaDetalle | null = null;
    store
      .reservar({
        puestoId: 'puesto-1',
        inicio: RESERVA.inicio,
        fin: RESERVA.fin
      })
      .subscribe((reserva) => (creada = reserva));

    expect(creada!).toEqual(RESERVA);
    expect(store.aviso()).toBe(AVISO_RESERVA_CONFIRMADA);
  });

  it('carga la agenda propia y calcula el total de páginas', () => {
    api.misReservas.and.returnValue(of(PAGINA));

    store.cargarMisReservas({ estado: null, pagina: 1, tamano: 10 }).subscribe();

    expect(store.reservas()).toEqual(PAGINA);
    expect(store.totalPaginas()).toBe(1);
    expect(store.reservasActivas()).toBe(1);
  });

  it('cancela una reserva y publica el aviso', () => {
    api.cancelar.and.returnValue(of(void 0));

    let cancelada = false;
    store.cancelar('reserva-1').subscribe((resultado) => (cancelada = resultado));

    expect(cancelada).toBeTrue();
    expect(store.aviso()).toBe(AVISO_RESERVA_CANCELADA);
    expect(store.cancelando()).toBeNull();
  });

  it('traduce la cancelación fuera de plazo', () => {
    api.cancelar.and.returnValue(
      throwError(() => problema('fuera-de-plazo', 'La reserva ya no puede cancelarse.'))
    );

    let cancelada = true;
    store.cancelar('reserva-1').subscribe((resultado) => (cancelada = resultado));

    expect(cancelada).toBeFalse();
    expect(store.error()).toContain('ya no puede cancelarse');
  });
});
