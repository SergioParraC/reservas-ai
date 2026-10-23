import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { FranjaNoOperativa, PeriodoAcademico } from '../models/calendario.models';
import { CalendarioApi } from './calendario.api';

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

describe('CalendarioApi', () => {
  let api: CalendarioApi;
  let control: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    api = TestBed.inject(CalendarioApi);
    control = TestBed.inject(HttpTestingController);
  });

  afterEach(() => control.verify());

  it('lista periodos y franjas en sus rutas administrativas', () => {
    api.listarPeriodos().subscribe();
    api.listarFranjas().subscribe();

    control.expectOne((peticion) => peticion.url.endsWith('/api/periodos-academicos')).flush([PERIODO]);
    control.expectOne((peticion) => peticion.url.endsWith('/api/franjas-no-operativas')).flush([FRANJA]);
  });

  it('envía el alta y la activación del periodo', () => {
    api
      .crearPeriodo({ nombre: '2026-2', fechaInicio: '2026-08-01', fechaFin: '2026-12-15' })
      .subscribe();
    api.activarPeriodo('periodo-1').subscribe();

    const alta = control.expectOne((peticion) => peticion.url.endsWith('/api/periodos-academicos'));
    expect(alta.request.method).toBe('POST');
    expect(alta.request.body).toEqual({
      nombre: '2026-2',
      fechaInicio: '2026-08-01',
      fechaFin: '2026-12-15'
    });
    alta.flush(PERIODO);

    const activar = control.expectOne((peticion) =>
      peticion.url.endsWith('/api/periodos-academicos/periodo-1/activar')
    );
    expect(activar.request.method).toBe('PUT');
    activar.flush(PERIODO);
  });

  it('envía la franja recurrente con el nombre del día y su horario', () => {
    api
      .crearFranja({
        diaSemana: 'Saturday',
        fechaEspecifica: null,
        horaInicio: '13:00',
        horaFin: '21:00',
        esFeriado: false,
        motivo: 'Mantenimiento'
      })
      .subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/franjas-no-operativas'));

    expect(solicitud.request.method).toBe('POST');
    expect(solicitud.request.body).toEqual(
      jasmine.objectContaining({
        diaSemana: 'Saturday',
        fechaEspecifica: null,
        horaInicio: '13:00',
        horaFin: '21:00',
        motivo: 'Mantenimiento'
      })
    );
    solicitud.flush(FRANJA);
  });

  it('consulta el calendario vigente y elimina una franja', () => {
    api.vigente().subscribe();
    api.eliminarFranja('franja-1').subscribe();

    control
      .expectOne((peticion) => peticion.url.endsWith('/api/calendario/vigente'))
      .flush({ tienePeriodoActivo: true, periodoActivo: PERIODO, franjas: [FRANJA] });

    const baja = control.expectOne((peticion) => peticion.url.endsWith('/api/franjas-no-operativas/franja-1'));
    expect(baja.request.method).toBe('DELETE');
    baja.flush(null);
  });
});
