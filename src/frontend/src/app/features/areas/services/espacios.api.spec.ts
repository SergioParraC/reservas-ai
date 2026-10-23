import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { CategoriaEspacio, EstadoUnidad, TipoAprobacion, TipoElementoPrestamo, TipoModalidadPrestamo } from '../models/espacio.models';
import { EspaciosApi } from './espacios.api';

describe('EspaciosApi', () => {
  let api: EspaciosApi;
  let control: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    api = TestBed.inject(EspaciosApi);
    control = TestBed.inject(HttpTestingController);
  });

  afterEach(() => control.verify());

  it('consulta el catálogo con los filtros seleccionados', () => {
    api
      .listarCatalogo({ q: 'redes', categoria: CategoriaEspacio.Laboratorio, aforoMinimo: 20, pagina: 2, tamano: 12 })
      .subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/catalogo/espacios'));

    expect(solicitud.request.method).toBe('GET');
    expect(solicitud.request.params.get('q')).toBe('redes');
    expect(solicitud.request.params.get('categoria')).toBe('Laboratorio');
    expect(solicitud.request.params.get('aforoMinimo')).toBe('20');
    expect(solicitud.request.params.get('pagina')).toBe('2');
    expect(solicitud.request.params.get('tamano')).toBe('12');
    solicitud.flush({ elementos: [], total: 0, pagina: 2, tamano: 12 });
  });

  it('envía el alta con los nombres de enum que espera la API', () => {
    api
      .crear({
        nombre: 'Laboratorio de Redes',
        ubicacion: 'Bloque B',
        categoria: CategoriaEspacio.Laboratorio,
        aforo: 25,
        tipoAprobacion: TipoAprobacion.Docente,
        tipoModalidadPrestamo: TipoModalidadPrestamo.AreaCompleta,
        tipoElementoPrestamo: TipoElementoPrestamo.LaboratorioEspecializado
      })
      .subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/espacios'));

    expect(solicitud.request.method).toBe('POST');
    expect(solicitud.request.body).toEqual(jasmine.objectContaining({
      nombre: 'Laboratorio de Redes',
      categoria: 'Laboratorio',
      tipoAprobacion: 'Docente',
      tipoModalidadPrestamo: 'AreaCompleta',
      tipoElementoPrestamo: 'LaboratorioEspecializado',
      aforo: 25
    }));
    solicitud.flush({});
  });

  it('envía la edición con estado y rowVersion sin incluir el QR', () => {
    api
      .actualizar('area-1', {
        nombre: 'Laboratorio de Redes II',
        ubicacion: 'Bloque B',
        categoria: CategoriaEspacio.Laboratorio,
        aforo: 30,
        tipoAprobacion: TipoAprobacion.Docente,
        tipoModalidadPrestamo: TipoModalidadPrestamo.AreaCompleta,
        tipoElementoPrestamo: TipoElementoPrestamo.LaboratorioEspecializado,
        estado: EstadoUnidad.Activo,
        rowVersion: 'AAAA'
      })
      .subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/espacios/area-1'));

    expect(solicitud.request.method).toBe('PUT');
    expect(solicitud.request.body).toEqual(jasmine.objectContaining({ estado: 'Activo', rowVersion: 'AAAA' }));
    expect(solicitud.request.body.codigoQR).toBeUndefined();
    solicitud.flush({});
  });

  it('carga la fotografía como FormData con el campo imagen', () => {
    const archivo = new File(['png'], 'foto.png', { type: 'image/png' });
    api.subirImagen('area-1', archivo).subscribe();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/espacios/area-1/imagen'));
    const cuerpo = solicitud.request.body as FormData;

    expect(solicitud.request.method).toBe('POST');
    expect(cuerpo instanceof FormData).toBeTrue();
    expect(cuerpo.get('imagen') instanceof File).toBeTrue();
    expect((cuerpo.get('imagen') as File).name).toBe('foto.png');
    solicitud.flush({ urlFotografia: '/media/espacios/area-1/foto.png' });
  });

  it('descarga la plantilla como blob y lee el total de hojas', () => {
    let totalHojas = 0;

    api.plantilla(['area-1', 'area-2'], true, 2).subscribe((descarga) => (totalHojas = descarga.totalHojas));

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/espacios/qr/plantilla'));

    expect(solicitud.request.method).toBe('GET');
    expect(solicitud.request.params.getAll('espacioIds')).toEqual(['area-1', 'area-2']);
    expect(solicitud.request.params.get('incluirPuestos')).toBe('true');
    expect(solicitud.request.params.get('hoja')).toBe('2');
    expect(solicitud.request.responseType).toBe('blob');

    solicitud.flush(new Blob(['<svg/>'], { type: 'image/svg+xml' }), {
      headers: { 'X-Total-Hojas': '3' }
    });

    expect(totalHojas).toBe(3);
  });
});
