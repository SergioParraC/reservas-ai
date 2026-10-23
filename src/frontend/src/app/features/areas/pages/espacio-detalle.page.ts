import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { etiquetaCategoria, etiquetaTipoAprobacion, EspacioPublico } from '../models/espacio.models';
import { EspaciosApi } from '../services/espacios.api';

/** Detalle público de un área activa, con su fotografía y puestos disponibles. */
@Component({
  selector: 'app-espacio-detalle',
  imports: [RouterLink],
  template: `
    <a routerLink="/catalogo" class="volver">← Volver al catálogo</a>

    @if (error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    } @else if (area(); as datos) {
      <article class="detalle">
        @if (datos.fotografiaUrl) {
          <img [src]="datos.fotografiaUrl" [alt]="'Fotografía de ' + datos.nombre" />
        }

        <div class="contenido">
          <h1>{{ datos.nombre }}</h1>
          <p class="ubicacion">{{ datos.ubicacion }}</p>

          <dl>
            <dt>Categoría</dt>
            <dd>{{ categoria() }}</dd>
            <dt>Aforo</dt>
            <dd>{{ datos.aforo }} personas</dd>
            <dt>Aprobación</dt>
            <dd>{{ aprobacion() }}</dd>
            <dt>Puestos activos</dt>
            <dd>{{ datos.puestosActivos }}</dd>
          </dl>
        </div>
      </article>
    } @else {
      <p role="status">Cargando área…</p>
    }
  `,
  styles: `
    .volver {
      display: inline-block;
      margin-bottom: 1rem;
      color: var(--color-primario);
      text-decoration: none;
    }

    .detalle {
      background: #fff;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
      overflow: hidden;
    }

    img {
      width: 100%;
      max-height: 20rem;
      object-fit: cover;
    }

    .contenido {
      padding: 1.5rem;
    }

    h1 {
      margin: 0 0 0.25rem;
    }

    .ubicacion {
      margin: 0 0 1rem;
      color: #6b7280;
    }

    dl {
      display: grid;
      grid-template-columns: 10rem 1fr;
      gap: 0.4rem 1rem;
    }

    dt {
      font-weight: 600;
      color: #4b5563;
    }

    dd {
      margin: 0;
    }

    .error {
      padding: 0.8rem 1rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
    }
  `
})
export class EspacioDetallePage implements OnInit {
  private readonly api = inject(EspaciosApi);
  private readonly ruta = inject(ActivatedRoute);

  readonly area = signal<EspacioPublico | null>(null);
  readonly error = signal<string | null>(null);

  /** Carga el área indicada en la ruta. */
  ngOnInit(): void {
    const id = this.ruta.snapshot.paramMap.get('id');

    if (!id) {
      this.error.set('El área solicitada no existe.');
      return;
    }

    this.api.obtenerPublico(id).subscribe({
      next: (area) => this.area.set(area),
      error: () => this.error.set('El área solicitada no existe o no está publicada.')
    });
  }

  /** Etiqueta de la categoría del área. */
  categoria(): string {
    return etiquetaCategoria(this.area()!.categoria);
  }

  /** Etiqueta del tipo de aprobación. */
  aprobacion(): string {
    return etiquetaTipoAprobacion(this.area()!.tipoAprobacion);
  }
}
