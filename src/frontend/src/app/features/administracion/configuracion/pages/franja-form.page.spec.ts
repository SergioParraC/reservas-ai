import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { FranjaNoOperativa } from '../models/calendario.models';
import { CalendarioApi } from '../services/calendario.api';
import { FranjaFormPage } from './franja-form.page';

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
  rowVersion: 'AAAA'
};

function problema(slug: string): HttpErrorResponse {
  return new HttpErrorResponse({
    status: 409,
    error: {
      type: `https://api.reservas-espacios.edu/problems/${slug}`,
      title: 'Conflicto',
      status: 409,
      detail: 'Conflicto de la franja.'
    }
  });
}

describe('FranjaFormPage', () => {
  let api: jasmine.SpyObj<CalendarioApi>;

  beforeEach(() => {
    api = jasmine.createSpyObj<CalendarioApi>('CalendarioApi', [
      'listarPeriodos',
      'listarFranjas',
      'vigente',
      'crearFranja',
      'actualizarFranja',
      'eliminarFranja',
      'crearPeriodo',
      'actualizarPeriodo',
      'activarPeriodo'
    ]);
    api.listarPeriodos.and.returnValue(of([]));
    api.listarFranjas.and.returnValue(of([]));
    api.vigente.and.returnValue(of({ tienePeriodoActivo: false, periodoActivo: null, franjas: [] }));

    TestBed.configureTestingModule({
      imports: [FranjaFormPage],
      providers: [
        provideRouter([]),
        { provide: CalendarioApi, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({}) } }
        }
      ]
    });
  });

  function pagina(): FranjaFormPage {
    const fixture = TestBed.createComponent(FranjaFormPage);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('registra el domingo completo de 00:00 a 23:59', () => {
    api.crearFranja.and.returnValue(of(FRANJA));

    const componente = pagina();
    componente.formulario.patchValue({
      criterio: 'dia',
      diaSemana: 0,
      diaCompleto: true,
      motivo: 'Cierre dominical'
    });
    componente.guardar();

    expect(api.crearFranja).toHaveBeenCalledWith(
      jasmine.objectContaining({
        diaSemana: 'Sunday',
        fechaEspecifica: null,
        horaInicio: '00:00',
        horaFin: '23:59',
        motivo: 'Cierre dominical'
      })
    );
  });

  it('registra el sábado parcial de 13:00 a 21:00', () => {
    api.crearFranja.and.returnValue(of({ ...FRANJA, diaSemana: 6 }));

    const componente = pagina();
    componente.formulario.patchValue({
      criterio: 'dia',
      diaSemana: 6,
      diaCompleto: false,
      horaInicio: '13:00',
      horaFin: '21:00',
      motivo: 'Mantenimiento'
    });
    componente.guardar();

    expect(api.crearFranja).toHaveBeenCalledWith(
      jasmine.objectContaining({
        diaSemana: 'Saturday',
        horaInicio: '13:00',
        horaFin: '21:00'
      })
    );
  });

  it('muestra el rechazo por duplicado', () => {
    api.crearFranja.and.returnValue(throwError(() => problema('franja-duplicada')));

    const componente = pagina();
    componente.formulario.patchValue({
      criterio: 'dia',
      diaSemana: 0,
      diaCompleto: true,
      motivo: 'Cierre dominical'
    });
    componente.guardar();

    expect(componente.tienda.error()).toContain('mismo criterio y horario');
  });

  it('exige la fecha cuando el criterio es una fecha específica', () => {
    const componente = pagina();
    componente.formulario.patchValue({
      criterio: 'fecha',
      fechaEspecifica: '',
      diaCompleto: true,
      motivo: 'Feriado'
    });
    componente.guardar();

    expect(api.crearFranja).not.toHaveBeenCalled();
    expect(componente.criterioInvalido()).toContain('fecha específica');
  });
});
