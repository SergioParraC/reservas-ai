import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { CalendarioResumen } from '../../administracion/configuracion/components/calendario-resumen';
import {
  CalendarioVigente,
  PeriodoAcademico
} from '../../administracion/configuracion/models/calendario.models';
import { CalendarioApi } from '../../administracion/configuracion/services/calendario.api';
import {
  CategoriaEspacio,
  EspacioCatalogo,
  TipoAprobacion,
  TipoElementoPrestamo,
  TipoModalidadPrestamo
} from '../../areas/models/espacio.models';
import { EspaciosApi } from '../../areas/services/espacios.api';
import { PatronSeleccion, TipoRecurrencia } from '../models/serie.models';
import { ReservasRecurrentesApi } from '../services/reservas-recurrentes.api';
import { NuevaSeriePage } from './nueva-serie.page';

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

const VIGENTE: CalendarioVigente = {
  tienePeriodoActivo: true,
  periodoActivo: PERIODO,
  franjas: []
};

const AREA: EspacioCatalogo = {
  id: 'area-1',
  nombre: 'Laboratorio Química 1',
  ubicacion: 'O-301',
  categoria: CategoriaEspacio.Laboratorio,
  aforo: 30,
  tipoAprobacion: TipoAprobacion.Docente,
  fotografiaUrl: null,
  tipoModalidadPrestamo: TipoModalidadPrestamo.AreaCompleta,
  tipoElementoPrestamo: TipoElementoPrestamo.LaboratorioEspecializado
};

/** Jueves bisemanal 07:00–09:00: seis ocurrencias entre el 01/10/2026 y el 12/12/2026. */
const PATRON: PatronSeleccion = {
  tipoRecurrencia: TipoRecurrencia.Bisemanal,
  diaSemana: 4,
  fechasFijas: [],
  horaInicio: '07:00',
  horaFin: '09:00',
  periodoInicio: '2026-10-01',
  periodoFin: '2026-12-12'
};

describe('NuevaSeriePage', () => {
  let espaciosApi: jasmine.SpyObj<EspaciosApi>;

  beforeEach(() => {
    const calendarioApi = jasmine.createSpyObj<CalendarioApi>('CalendarioApi', [
      'listarPeriodos',
      'listarFranjas',
      'vigente'
    ]);
    calendarioApi.vigente.and.returnValue(of(VIGENTE));

    espaciosApi = jasmine.createSpyObj<EspaciosApi>('EspaciosApi', ['listarCatalogo']);
    espaciosApi.listarCatalogo.and.returnValue(
      of({ elementos: [AREA], total: 1, pagina: 1, tamano: 50 })
    );

    const seriesApi = jasmine.createSpyObj<ReservasRecurrentesApi>('ReservasRecurrentesApi', [
      'crear',
      'misSeries'
    ]);

    TestBed.configureTestingModule({
      imports: [NuevaSeriePage, CalendarioResumen],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: CalendarioApi, useValue: calendarioApi },
        { provide: EspaciosApi, useValue: espaciosApi },
        { provide: ReservasRecurrentesApi, useValue: seriesApi }
      ]
    });
  });

  it('habilita la confirmación cuando la materia es el último dato completado', () => {
    const fixture = TestBed.createComponent(NuevaSeriePage);
    const pagina = fixture.componentInstance;

    fixture.detectChanges();

    pagina.seleccionarArea({ target: { value: AREA.id } } as unknown as Event);
    fixture.detectChanges();
    expect(pagina.puedeEnviar()).toBeFalse();

    pagina.actualizarPatron(PATRON);
    fixture.detectChanges();
    expect(pagina.confirmadas()).toBe(6);
    expect(pagina.puedeEnviar()).toBeFalse();

    pagina.formulario.setValue({ materia: 'Química orgánica' });
    fixture.detectChanges();

    const boton = fixture.nativeElement.querySelector('.acciones button') as HTMLButtonElement;

    expect(pagina.patron()).toEqual(PATRON);
    expect(pagina.puedeEnviar()).toBeTrue();
    expect(boton.disabled).toBeFalse();
  });

  it('mantiene la confirmación deshabilitada mientras falte la materia o el área', () => {
    const fixture = TestBed.createComponent(NuevaSeriePage);
    const pagina = fixture.componentInstance;

    fixture.detectChanges();

    pagina.actualizarPatron(PATRON);
    pagina.formulario.setValue({ materia: 'Química orgánica' });
    fixture.detectChanges();
    expect(pagina.puedeEnviar()).toBeFalse();

    pagina.seleccionarArea({ target: { value: AREA.id } } as unknown as Event);
    pagina.formulario.controls.materia.setValue('');
    fixture.detectChanges();
    expect(pagina.puedeEnviar()).toBeFalse();
  });
});
