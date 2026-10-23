import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { EspacioDetallePage } from './espacio-detalle.page';
import { EspacioPublico, EstadoUnidad, TipoAprobacion } from '../models/espacio.models';
import { EspaciosApi } from '../services/espacios.api';

const AREA: EspacioPublico = {
  id: 'espacio-1',
  nombre: 'Sala de Cómputo 1',
  ubicacion: 'Bloque C',
  categoria: 4,
  aforo: 20,
  tipoAprobacion: TipoAprobacion.Automatica,
  fotografiaUrl: null,
  puestosActivos: 1,
  puestos: [
    {
      id: 'puesto-1',
      identificador: 'PC-01',
      descripcionEquipo: 'Equipo de pruebas',
      estado: EstadoUnidad.Activo
    }
  ]
};

function configurar(area: EspacioPublico) {
  const api = jasmine.createSpyObj<EspaciosApi>('EspaciosApi', ['obtenerPublico']);
  api.obtenerPublico.and.returnValue(of(area));

  TestBed.configureTestingModule({
    imports: [EspacioDetallePage],
    providers: [
      provideRouter([]),
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ id: 'espacio-1' }) } }
      },
      { provide: EspaciosApi, useValue: api }
    ]
  });
}

describe('EspacioDetallePage', () => {
  it('enlaza cada puesto cuando el área tiene aprobación automática', () => {
    configurar(AREA);
    const fixture = TestBed.createComponent(EspacioDetallePage);
    fixture.detectChanges();

    const enlaces = Array.from(
      fixture.nativeElement.querySelectorAll('.puestos a') as NodeListOf<HTMLAnchorElement>
    );

    expect(fixture.nativeElement.textContent).toContain('Reserva inmediata de puestos');
    expect(enlaces.length).toBe(1);
    expect(enlaces[0].getAttribute('href')).toContain('/solicitudes/puesto/puesto-1');
  });

  it('muestra los puestos sin enlaces cuando el área exige aprobación docente', () => {
    configurar({ ...AREA, tipoAprobacion: TipoAprobacion.Docente });
    const fixture = TestBed.createComponent(EspacioDetallePage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Puestos del área');
    expect(fixture.nativeElement.textContent).toContain(
      'solo está disponible en áreas con aprobación automática'
    );
    expect(fixture.nativeElement.querySelectorAll('.puestos a').length).toBe(0);
    expect(fixture.nativeElement.textContent).not.toContain('Reserva inmediata de puestos');
  });

  it('avisa cuando el área automática aún no tiene puestos activos', () => {
    configurar({ ...AREA, puestosActivos: 0, puestos: [] });
    const fixture = TestBed.createComponent(EspacioDetallePage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'aún no tiene puestos activos para préstamo directo'
    );
  });

  it('explica que la reserva del área completa pertenece a otra etapa', () => {
    configurar({ ...AREA, tipoAprobacion: TipoAprobacion.Docente, puestosActivos: 0, puestos: [] });
    const fixture = TestBed.createComponent(EspacioDetallePage);
    fixture.detectChanges();

    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('no tiene puestos de cómputo');
    expect(texto).toContain('aprobación docente');
    expect(texto).toContain('todavía no está disponible');
    expect(fixture.nativeElement.querySelectorAll('app-reserva-card, .puestos a').length).toBe(0);
  });
});
