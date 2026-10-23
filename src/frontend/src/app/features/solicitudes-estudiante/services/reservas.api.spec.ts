import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { EstadoReserva } from '../models/reserva.models';
import { ReservasApi } from './reservas.api';

describe('ReservasApi', () => {
  let api: ReservasApi;
  let control: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    api = TestBed.inject(ReservasApi);
    control = TestBed.inject(HttpTestingController);
  });

  afterEach(() => control.verify());

  it('consulta la disponibilidad con fecha y duración', () => {
    api.disponibilidad('puesto-1', { fecha: '2026-11-03', duracionMinutos: 60 }).subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/puestos/puesto-1/disponibilidad'));

    expect(solicitud.request.method).toBe('GET');
    expect(solicitud.request.params.get('fecha')).toBe('2026-11-03');
    expect(solicitud.request.params.get('duracionMinutos')).toBe('60');
    solicitud.flush({
      puestoId: 'puesto-1',
      identificador: 'PC-01',
      espacioId: 'espacio-1',
      espacioNombre: 'Sala',
      zonaHoraria: 'America/Bogota',
      franjas: []
    });
  });

  it('envía el alta con los instantes entregados por la API', () => {
    api
      .crear({
        puestoId: 'puesto-1',
        inicio: '2026-11-03T10:00:00-05:00',
        fin: '2026-11-03T11:00:00-05:00'
      })
      .subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/reservas'));

    expect(solicitud.request.method).toBe('POST');
    expect(solicitud.request.body).toEqual({
      puestoId: 'puesto-1',
      inicio: '2026-11-03T10:00:00-05:00',
      fin: '2026-11-03T11:00:00-05:00'
    });
    solicitud.flush({});
  });

  it('consulta la agenda propia con estado y paginación', () => {
    api
      .misReservas({ estado: EstadoReserva.Cancelada, pagina: 2, tamano: 10 })
      .subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/reservas/me'));

    expect(solicitud.request.method).toBe('GET');
    expect(solicitud.request.params.get('estado')).toBe('Cancelada');
    expect(solicitud.request.params.get('pagina')).toBe('2');
    expect(solicitud.request.params.get('tamano')).toBe('10');
    solicitud.flush({ elementos: [], total: 0, pagina: 2, tamano: 10 });
  });

  it('cancela la reserva por su identificador', () => {
    api.cancelar('reserva-1').subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/reservas/reserva-1'));

    expect(solicitud.request.method).toBe('DELETE');
    solicitud.flush(null, { status: 204, statusText: 'No Content' });
  });
});
