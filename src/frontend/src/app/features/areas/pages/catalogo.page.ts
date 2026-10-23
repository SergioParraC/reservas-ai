import { Component, inject, OnInit } from '@angular/core';

import { FiltrosCatalogoComponent } from '../components/filtros-catalogo';
import { EspacioCard } from '../components/espacio-card';
import { CatalogoStore } from '../estado/catalogo.store';
import { FiltrosCatalogo as Filtros } from '../models/espacio.models';

/** Catálogo público interactivo de áreas activas. */
@Component({
  selector: 'app-catalogo',
  imports: [FiltrosCatalogoComponent, EspacioCard],
  template: `
    <header class="encabezado">
      <div>
        <h1>Catálogo de espacios</h1>
        <p class="ayuda">Consulte las áreas disponibles, su aforo y su tipo de aprobación.</p>
      </div>
    </header>

    <app-filtros-catalogo [inicial]="tienda.filtros()" (cambio)="aplicar($event)" />

    @if (tienda.error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    }

    @if (tienda.cargando()) {
      <p role="status" class="estado">Cargando espacios…</p>
    } @else if (tienda.resultado(); as pagina) {
      @if (pagina.elementos.length === 0) {
        <p class="vacio" role="status">No hay espacios que coincidan con los filtros.</p>
      } @else {
        <p class="total">{{ pagina.total }} espacio(s) encontrados</p>

        <div class="rejilla">
          @for (espacio of pagina.elementos; track espacio.id) {
            <app-espacio-card [espacio]="espacio" />
          }
        </div>

        <nav class="paginacion" aria-label="Paginación del catálogo">
          <button type="button" (click)="anterior()" [disabled]="pagina.pagina <= 1">Anterior</button>
          <span>Página {{ pagina.pagina }} de {{ tienda.totalPaginas() }}</span>
          <button type="button" (click)="siguiente()" [disabled]="pagina.pagina >= tienda.totalPaginas()">
            Siguiente
          </button>
        </nav>
      }
    }
  `,
  styles: `
    .encabezado {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      margin-bottom: 1rem;
    }

    h1 {
      margin: 0 0 0.25rem;
    }

    .ayuda {
      margin: 0;
      color: #6b7280;
    }

    .rejilla {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr));
      gap: 1rem;
      margin-top: 1rem;
    }

    .total {
      margin: 1rem 0 0;
      color: #4b5563;
      font-size: 0.9rem;
    }

    .vacio,
    .error,
    .estado {
      margin-top: 1rem;
      padding: 0.8rem 1rem;
      background: #fff;
      border: 1px solid var(--color-borde);
      border-radius: 0.5rem;
    }

    .error {
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
    }

    .paginacion {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 1rem;
      margin-top: 1.5rem;
    }
  `
})
export class CatalogoPage implements OnInit {
  readonly tienda = inject(CatalogoStore);

  /** Refresca el catálogo al entrar, para reflejar altas o cambios administrativos recientes. */
  ngOnInit(): void {
    this.tienda.refrescar();
  }

  /** Aplica los filtros emitidos por el formulario. */
  aplicar(filtros: Partial<Filtros>): void {
    this.tienda.aplicarFiltros(filtros);
  }

  /** Retrocede una página. */
  anterior(): void {
    this.tienda.irAPagina((this.tienda.resultado()?.pagina ?? 1) - 1);
  }

  /** Avanza una página. */
  siguiente(): void {
    this.tienda.irAPagina((this.tienda.resultado()?.pagina ?? 1) + 1);
  }
}
