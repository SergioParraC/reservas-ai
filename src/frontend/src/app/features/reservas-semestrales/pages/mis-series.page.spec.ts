import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { PaginaSeries, SerieResumen, TipoRecurrencia } from '../models/serie.models';
import { ReservasRecurrentesApi } from '../services/reservas-recurrentes.api';
import { MisSeriesPage } from './mis-series.page';

function serie(id: string, materia: string, creadaEn: string): SerieResumen {
  return {
    id,
    espacioId: 'espacio-1',
    espacioNombre: 'Laboratorio de Redes',
    materia,
    tipoRecurrencia: TipoRecurrencia.Semanal,
    diaSemana: 2,
    horaInicio: '14:00:00',
    horaFin: '16:00:00',
    periodoInicio: '2026-08-04',
    periodoFin: '2026-11-24',
    totalOcurrencias: 16,
    creadaEn
  };
}

const RECIENTE = serie('serie-2', 'Física II', '2026-07-10T08:00:00-05:00');
const ANTIGUA = serie('serie-1', 'Cálculo II', '2026-07-01T08:00:00-05:00');

describe('MisSeriesPage', () => {
  let api: jasmine.SpyObj<ReservasRecurrentesApi>;

  beforeEach(() => {
    api = jasmine.createSpyObj<ReservasRecurrentesApi>('ReservasRecurrentesApi', ['crear', 'misSeries']);

    TestBed.configureTestingModule({
      imports: [MisSeriesPage],
      providers: [provideRouter([]), { provide: ReservasRecurrentesApi, useValue: api }]
    });
  });

  it('muestra las series en el orden descendente que entrega la API', () => {
    const pagina: PaginaSeries = { elementos: [RECIENTE, ANTIGUA], total: 2, pagina: 1, tamano: 10 };
    api.misSeries.and.returnValue(of(pagina));

    const fixture = TestBed.createComponent(MisSeriesPage);
    fixture.detectChanges();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(texto).toContain('2 serie(s)');
    expect(texto.indexOf(RECIENTE.materia)).toBeLessThan(texto.indexOf(ANTIGUA.materia));
    expect(texto).toContain('16 ocurrencia(s)');
  });

  it('muestra el estado vacío cuando no hay series', () => {
    api.misSeries.and.returnValue(of({ elementos: [], total: 0, pagina: 1, tamano: 10 }));

    const fixture = TestBed.createComponent(MisSeriesPage);
    fixture.detectChanges();
    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(texto).toContain('Aún no tiene series registradas');
  });
});
