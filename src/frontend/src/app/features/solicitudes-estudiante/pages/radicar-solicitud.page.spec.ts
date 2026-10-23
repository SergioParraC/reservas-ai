import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { SolicitudesApi } from '../../../core/solicitudes/solicitudes.api';
import {
  EspacioPublico,
  TipoAprobacion,
  TipoElementoPrestamo,
  TipoModalidadPrestamo
} from '../../areas/models/espacio.models';
import { EspaciosApi } from '../../areas/services/espacios.api';
import { CalendarioApi } from '../../administracion/configuracion/services/calendario.api';
import { RadicarSolicitudPage } from './radicar-solicitud.page';

const AREA: EspacioPublico = {
  id: 'espacio-1',
  nombre: 'Laboratorio de Redes',
  ubicacion: 'Bloque B',
  categoria: 2,
  aforo: 30,
  tipoAprobacion: TipoAprobacion.Docente,
  fotografiaUrl: null,
  tipoModalidadPrestamo: TipoModalidadPrestamo.AreaCompleta,
  tipoElementoPrestamo: TipoElementoPrestamo.LaboratorioEspecializado,
  puestosActivos: 0,
  puestos: []
};

function problema(status: number, slug: string): HttpErrorResponse {
  return new HttpErrorResponse({
    status,
    error: {
      type: `https://api.reservas-espacios.edu/problems/${slug}`,
      title: 'Error',
      status,
      detail: 'Detalle del problema.'
    }
  });
}

function pdf(): File {
  return new File(['%PDF-1.4 contenido %%EOF'], 'informe.pdf', { type: 'application/pdf' });
}

describe('RadicarSolicitudPage', () => {
  let espacios: jasmine.SpyObj<EspaciosApi>;
  let solicitudes: jasmine.SpyObj<SolicitudesApi>;

  beforeEach(() => {
    espacios = jasmine.createSpyObj<EspaciosApi>('EspaciosApi', ['obtenerPublico']);
    solicitudes = jasmine.createSpyObj<SolicitudesApi>('SolicitudesApi', ['radicar']);
    const calendario = jasmine.createSpyObj<CalendarioApi>('CalendarioApi', [
      'vigente',
      'listarPeriodos',
      'listarFranjas'
    ]);

    calendario.vigente.and.returnValue(
      of({ tienePeriodoActivo: false, periodoActivo: null, franjas: [] })
    );
    calendario.listarPeriodos.and.returnValue(of([]));
    calendario.listarFranjas.and.returnValue(of([]));
    espacios.obtenerPublico.and.returnValue(of(AREA));

    TestBed.configureTestingModule({
      imports: [RadicarSolicitudPage],
      providers: [
        provideRouter([]),
        { provide: EspaciosApi, useValue: espacios },
        { provide: SolicitudesApi, useValue: solicitudes },
        { provide: CalendarioApi, useValue: calendario },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ espacioId: 'espacio-1' }) } }
        }
      ]
    });
  });

  function pagina(): RadicarSolicitudPage {
    const fixture = TestBed.createComponent(RadicarSolicitudPage);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  function completar(componente: RadicarSolicitudPage): void {
    componente.formulario.setValue({
      materia: 'Bases de datos',
      docenteResponsable: 'Ing. Laura Gómez',
      justificacion: 'Práctica de laboratorio.',
      fecha: '2026-11-05',
      horaInicio: '14:00',
      horaFin: '16:00'
    });
    componente.seleccionarArchivo({ target: { files: [pdf()] } } as unknown as Event);
  }

  it('rechaza localmente un archivo que no es PDF', () => {
    const componente = pagina();
    const archivo = new File(['texto'], 'informe.txt', { type: 'text/plain' });

    componente.seleccionarArchivo({ target: { files: [archivo] } } as unknown as Event);

    expect(componente.errorArchivo()).toContain('PDF');
    expect(solicitudes.radicar).not.toHaveBeenCalled();
  });

  it('traduce el 413 del pre-informe sobredimensionado', () => {
    solicitudes.radicar.and.returnValue(throwError(() => problema(413, 'archivo-demasiado-grande')));
    const componente = pagina();
    completar(componente);

    componente.radicar();

    expect(componente.error()).toContain('413');
  });

  it('traduce el 415 del archivo no válido', () => {
    solicitudes.radicar.and.returnValue(throwError(() => problema(415, 'archivo-no-valido')));
    const componente = pagina();
    completar(componente);

    componente.radicar();

    expect(componente.error()).toContain('415');
  });

  it('traduce el 409 de modalidad incompatible', () => {
    solicitudes.radicar.and.returnValue(throwError(() => problema(409, 'modalidad-incompatible')));
    const componente = pagina();
    completar(componente);

    componente.radicar();

    expect(componente.error()).toContain('modalidad');
  });

  it('radica con FormData y muestra el folio', () => {
    solicitudes.radicar.and.returnValue(
      of({
        id: 'solicitud-1',
        espacioId: 'espacio-1',
        estado: 1,
        inicio: '2026-11-05T14:00:00-05:00',
        fin: '2026-11-05T16:00:00-05:00',
        radicadaEn: '2026-11-02T08:00:00-05:00',
        archivoNombreOriginal: 'informe.pdf',
        archivoTamanoBytes: 20
      })
    );

    const componente = pagina();
    completar(componente);
    componente.radicar();

    expect(solicitudes.radicar).toHaveBeenCalledWith(
      jasmine.objectContaining({ espacioId: 'espacio-1', materia: 'Bases de datos' })
    );
    expect(componente.radicada()?.id).toBe('solicitud-1');
    expect(componente.enviando()).toBeFalse();
  });
});
