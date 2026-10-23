import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ReservasRecurrentesApi } from './reservas-recurrentes.api';
import { TipoRecurrencia } from '../models/serie.models';

describe('ReservasRecurrentesApi', () => {
  let api: ReservasRecurrentesApi;
  let control: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    api = TestBed.inject(ReservasRecurrentesApi);
    control = TestBed.inject(HttpTestingController);
  });

  afterEach(() => control.verify());

  it('envía la serie con los nombres de enum y el patrón', () => {
    api
      .crear({
        espacioId: 'espacio-1',
        tipoRecurrencia: TipoRecurrencia.Semanal,
        diaSemana: 2,
        fechasFijas: [],
        horaInicio: '14:00',
        horaFin: '16:00',
        periodoInicio: '2026-08-04',
        periodoFin: '2026-11-24',
        materia: 'Cálculo II'
      })
      .subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/reservas/recurrentes'));

    expect(solicitud.request.method).toBe('POST');
    expect(solicitud.request.body).toEqual({
      espacioId: 'espacio-1',
      tipoRecurrencia: 'Semanal',
      diaSemana: 'Tuesday',
      fechasFijas: [],
      horaInicio: '14:00',
      horaFin: '16:00',
      periodoInicio: '2026-08-04',
      periodoFin: '2026-11-24',
      materia: 'Cálculo II'
    });
    solicitud.flush({});
  });

  it('envía las fechas fijas sin día de la semana', () => {
    api
      .crear({
        espacioId: 'espacio-1',
        tipoRecurrencia: TipoRecurrencia.FechasFijas,
        diaSemana: null,
        fechasFijas: ['2026-09-07', '2026-10-19'],
        horaInicio: '14:00',
        horaFin: '16:00',
        periodoInicio: '2026-08-04',
        periodoFin: '2026-11-24',
        materia: 'Cálculo II'
      })
      .subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/reservas/recurrentes'));

    expect(solicitud.request.body.diaSemana).toBeNull();
    expect(solicitud.request.body.fechasFijas).toEqual(['2026-09-07', '2026-10-19']);
    solicitud.flush({});
  });

  it('consulta las series propias con paginación', () => {
    api.misSeries(2, 5).subscribe();

    const solicitud = control.expectOne((peticion) =>
      peticion.url.endsWith('/api/reservas/recurrentes/me')
    );

    expect(solicitud.request.params.get('pagina')).toBe('2');
    expect(solicitud.request.params.get('tamano')).toBe('5');
    solicitud.flush({ elementos: [], total: 0, pagina: 2, tamano: 5 });
  });
});
