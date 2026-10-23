import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';

import { CatalogoStore } from './catalogo.store';

describe('CatalogoStore', () => {
  let store: CatalogoStore;
  let control: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({}) } }
        }
      ]
    });

    store = TestBed.inject(CatalogoStore);
    control = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    control.expectOne((peticion) => peticion.url.endsWith('/api/catalogo/espacios')).flush({
      elementos: [],
      total: 0,
      pagina: 1,
      tamano: 12
    });
  });

  afterEach(() => control.verify());

  it('carga la primera página al inicializarse', () => {
    expect(store.filtros().pagina).toBe(1);
    expect(store.resultado()?.total).toBe(0);
    expect(store.totalPaginas()).toBe(1);
  });

  it('aplica el debounce de 300 ms y refleja los filtros en la URL', fakeAsync(() => {
    store.aplicarFiltros({ q: 'redes', aforoMinimo: 20 });

    control.expectNone((peticion) => peticion.url.endsWith('/api/catalogo/espacios'));

    tick(300);

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/catalogo/espacios'));
    expect(solicitud.request.params.get('q')).toBe('redes');
    expect(solicitud.request.params.get('aforoMinimo')).toBe('20');
    expect(router.navigate).toHaveBeenCalledWith(
      [],
      jasmine.objectContaining({
        queryParams: jasmine.objectContaining({ q: 'redes', aforoMinimo: 20 }),
        replaceUrl: true
      })
    );

    solicitud.flush({ elementos: [], total: 0, pagina: 1, tamano: 12 });
  }));

  it('pagina y conserva los filtros activos', fakeAsync(() => {
    store.aplicarFiltros({ q: 'redes' });
    tick(300);
    control.expectOne((peticion) => peticion.url.endsWith('/api/catalogo/espacios')).flush({
      elementos: [],
      total: 30,
      pagina: 1,
      tamano: 12
    });

    expect(store.totalPaginas()).toBe(3);

    store.irAPagina(2);
    tick(300);

    const segunda = control.expectOne((peticion) => peticion.url.endsWith('/api/catalogo/espacios'));
    expect(segunda.request.params.get('pagina')).toBe('2');
    expect(segunda.request.params.get('q')).toBe('redes');
    segunda.flush({ elementos: [], total: 30, pagina: 2, tamano: 12 });

    expect(store.filtros().pagina).toBe(2);
  }));

  it('refresca la consulta con los filtros vigentes', () => {
    store.refrescar();

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/catalogo/espacios'));

    expect(solicitud.request.params.get('pagina')).toBe('1');
    solicitud.flush({ elementos: [], total: 2, pagina: 1, tamano: 12 });

    expect(store.resultado()?.total).toBe(2);
  });

  it('no repite la consulta cuando los filtros no cambian', fakeAsync(() => {
    store.aplicarFiltros({ q: 'redes' });
    store.aplicarFiltros({ q: 'redes' });
    tick(300);

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/catalogo/espacios'));
    solicitud.flush({ elementos: [], total: 0, pagina: 1, tamano: 12 });

    expect(store.filtros().q).toBe('redes');
    control.expectNone((peticion) => peticion.url.endsWith('/api/catalogo/espacios'));
  }));
});
