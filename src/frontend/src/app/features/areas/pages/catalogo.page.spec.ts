import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { CatalogoStore } from '../estado/catalogo.store';
import {
  CategoriaEspacio,
  EspacioCatalogo,
  FiltrosCatalogo,
  TipoAprobacion,
  TipoElementoPrestamo,
  TipoModalidadPrestamo
} from '../models/espacio.models';
import { CatalogoPage } from './catalogo.page';

const AREA: EspacioCatalogo = {
  id: 'area-1',
  nombre: 'Laboratorio de Redes',
  ubicacion: 'Bloque B',
  categoria: CategoriaEspacio.Laboratorio,
  aforo: 25,
  tipoAprobacion: TipoAprobacion.Docente,
  fotografiaUrl: null,
  tipoModalidadPrestamo: TipoModalidadPrestamo.AreaCompleta,
  tipoElementoPrestamo: TipoElementoPrestamo.LaboratorioEspecializado
};

function crearTienda(elementos: EspacioCatalogo[], total = elementos.length) {
  return {
    filtros: signal<FiltrosCatalogo>({ pagina: 1, tamano: 12 }),
    resultado: signal({ elementos, total, pagina: 1, tamano: 12 }),
    cargando: signal(false),
    error: signal<string | null>(null),
    totalPaginas: signal(1),
    aplicarFiltros: jasmine.createSpy('aplicarFiltros'),
    irAPagina: jasmine.createSpy('irAPagina'),
    refrescar: jasmine.createSpy('refrescar')
  };
}

describe('CatalogoPage', () => {
  function configurar(elementos: EspacioCatalogo[], total = elementos.length) {
    const tienda = crearTienda(elementos, total);

    TestBed.configureTestingModule({
      imports: [CatalogoPage],
      providers: [provideRouter([]), { provide: CatalogoStore, useValue: tienda }]
    });

    return tienda;
  }

  it('renderiza las tarjetas del catálogo', () => {
    configurar([AREA]);
    const fixture = TestBed.createComponent(CatalogoPage);

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Laboratorio de Redes');
    expect(fixture.nativeElement.textContent).toContain('Bloque B');
    expect(fixture.nativeElement.textContent).toContain('Aforo 25');
  });

  it('muestra el mensaje cuando no hay coincidencias', () => {
    configurar([]);
    const fixture = TestBed.createComponent(CatalogoPage);

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No hay espacios que coincidan con los filtros.');
  });

  it('delega los filtros y la paginación en el store', () => {
    const tienda = configurar([AREA], 30);
    const fixture = TestBed.createComponent(CatalogoPage);
    const pagina = fixture.componentInstance;

    pagina.aplicar({ q: 'redes' });
    pagina.siguiente();

    expect(tienda.aplicarFiltros).toHaveBeenCalledWith({ q: 'redes' });
    expect(tienda.irAPagina).toHaveBeenCalled();
  });

  it('refresca el catálogo al entrar a la vista', () => {
    const tienda = configurar([AREA]);
    const fixture = TestBed.createComponent(CatalogoPage);

    fixture.detectChanges();

    expect(tienda.refrescar).toHaveBeenCalled();
  });
});
