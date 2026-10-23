import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { AVISO_SERIE_CREADA, SeriesStore } from './series.store';
import { PaginaSeries, SerieDetalle, TipoRecurrencia } from '../models/serie.models';
import { ReservasRecurrentesApi } from '../services/reservas-recurrentes.api';

const SERIE: SerieDetalle = {
  id: 'serie-1',
  espacioId: 'espacio-1',
  espacioNombre: 'Laboratorio de Redes',
  materia: 'Cálculo II',
  tipoRecurrencia: TipoRecurrencia.Semanal,
  diaSemana: 2,
  fechasFijas: [],
  horaInicio: '14:00:00',
  horaFin: '16:00:00',
  periodoInicio: '2026-08-04',
  periodoFin: '2026-11-24',
  creadaEn: '2026-07-01T08:00:00-05:00',
  totalOcurrencias: 16,
  ocurrencias: []
};

const PAGINA: PaginaSeries = { elementos: [], total: 0, pagina: 1, tamano: 10 };

function problema(slug: string): HttpErrorResponse {
  return new HttpErrorResponse({
    status: 409,
    error: {
      type: `https://api.reservas-espacios.edu/problems/${slug}`,
      title: 'Conflicto',
      status: 409,
      detail: 'Conflicto de la serie.'
    }
  });
}

const SOLICITUD = {
  espacioId: 'espacio-1',
  tipoRecurrencia: TipoRecurrencia.Semanal,
  diaSemana: 2,
  fechasFijas: [],
  horaInicio: '14:00',
  horaFin: '16:00',
  periodoInicio: '2026-08-04',
  periodoFin: '2026-11-24',
  materia: 'Cálculo II'
};

describe('SeriesStore', () => {
  let api: jasmine.SpyObj<ReservasRecurrentesApi>;
  let store: SeriesStore;

  beforeEach(() => {
    api = jasmine.createSpyObj<ReservasRecurrentesApi>('ReservasRecurrentesApi', ['crear', 'misSeries']);

    TestBed.configureTestingModule({ providers: [{ provide: ReservasRecurrentesApi, useValue: api }] });
    store = TestBed.inject(SeriesStore);
  });

  it('confirma la serie y publica su aviso', () => {
    api.crear.and.returnValue(of(SERIE));

    let creada: SerieDetalle | null = null;
    store.crear(SOLICITUD).subscribe((serie) => (creada = serie));

    expect(creada!).toEqual(SERIE);
    expect(store.ultimaSerie()).toEqual(SERIE);
    expect(store.aviso()).toBe(AVISO_SERIE_CREADA);
    expect(store.guardando()).toBeFalse();
  });

  it('traduce el 409 colision-serie sin persistir', () => {
    api.crear.and.returnValue(throwError(() => problema('colision-serie')));

    store.crear(SOLICITUD).subscribe();

    expect(store.error()).toContain('se cruza con la agenda vigente');
    expect(store.ultimaSerie()).toBeNull();
  });

  it('traduce la franja no operativa y el periodo', () => {
    api.crear.and.returnValue(throwError(() => problema('franja-no-operativa')));
    store.crear(SOLICITUD).subscribe();
    expect(store.error()).toContain('franja no operativa');

    api.crear.and.returnValue(throwError(() => problema('fuera-de-periodo-academico')));
    store.crear(SOLICITUD).subscribe();
    expect(store.error()).toContain('fuera del periodo académico');
  });

  it('carga las series propias y calcula el total de páginas', () => {
    api.misSeries.and.returnValue(of({ ...PAGINA, total: 23 }));

    store.cargarSeries(1, 10).subscribe();

    expect(api.misSeries).toHaveBeenCalledWith(1, 10);
    expect(store.totalPaginas()).toBe(3);
  });
});
