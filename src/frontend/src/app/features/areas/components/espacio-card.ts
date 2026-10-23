import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import {
  CategoriaEspacio,
  etiquetaCategoria,
  etiquetaTipoAprobacion,
  EstadoUnidad,
  TipoAprobacion
} from '../models/espacio.models';

/** Subconjunto común de un área para la tarjeta del catálogo y del listado administrativo. */
export interface TarjetaEspacio {
  id: string;
  nombre: string;
  ubicacion: string;
  categoria: CategoriaEspacio;
  aforo: number;
  tipoAprobacion: TipoAprobacion;
  fotografiaUrl?: string | null;
  estado?: EstadoUnidad;
}

/** Tarjeta reutilizable de área con fotografía, categoría y aforo. */
@Component({
  selector: 'app-espacio-card',
  imports: [RouterLink],
  template: `
    <article class="tarjeta">
      @if (espacio().fotografiaUrl) {
        <img [src]="espacio().fotografiaUrl" [alt]="'Fotografía de ' + espacio().nombre" />
      } @else {
        <div class="sin-foto" aria-hidden="true">Sin fotografía</div>
      }

      <div class="cuerpo">
        <h2>
          <a [routerLink]="enlace()">{{ espacio().nombre }}</a>
        </h2>
        <p class="ubicacion">{{ espacio().ubicacion }}</p>
        <p class="meta">
          <span>{{ categoria() }}</span>
          <span>Aforo {{ espacio().aforo }}</span>
        </p>
        <p class="meta">Aprobación: {{ aprobacion() }}</p>

        @if (espacio().estado === estadoInactivo) {
          <p class="inactiva">Dada de baja</p>
        }
      </div>
    </article>
  `,
  styles: `
    .tarjeta {
      display: flex;
      flex-direction: column;
      background: #fff;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
      overflow: hidden;
      height: 100%;
    }

    img,
    .sin-foto {
      width: 100%;
      height: 9rem;
      object-fit: cover;
      background: #e2e8f0;
      color: #64748b;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.85rem;
    }

    .cuerpo {
      padding: 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
    }

    h2 {
      margin: 0;
      font-size: 1.05rem;
    }

    h2 a {
      color: var(--color-primario);
      text-decoration: none;
    }

    .ubicacion {
      margin: 0;
      color: #6b7280;
      font-size: 0.9rem;
    }

    .meta {
      display: flex;
      gap: 0.75rem;
      margin: 0;
      color: #374151;
      font-size: 0.85rem;
    }

    .inactiva {
      margin: 0.4rem 0 0;
      color: #b45309;
      font-size: 0.85rem;
      font-weight: 600;
    }
  `
})
export class EspacioCard {
  readonly espacio = input.required<TarjetaEspacio>();
  readonly modo = input<'catalogo' | 'admin'>('catalogo');
  readonly estadoInactivo = EstadoUnidad.Inactivo;

  readonly categoria = computed(() => etiquetaCategoria(this.espacio().categoria));
  readonly aprobacion = computed(() => etiquetaTipoAprobacion(this.espacio().tipoAprobacion));
  readonly enlace = computed(() =>
    this.modo() === 'admin'
      ? ['/administracion/espacios', this.espacio().id, 'editar']
      : ['/catalogo', this.espacio().id]
  );
}
