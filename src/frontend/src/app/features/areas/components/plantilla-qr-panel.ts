import { Component, computed, inject, input, OnDestroy, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

import { EspaciosApi } from '../services/espacios.api';

/** Previsualización y descarga de la plantilla A4 de etiquetas QR. */
@Component({
  selector: 'app-plantilla-qr-panel',
  template: `
    <section class="panel">
      <h2>Plantilla de impresión QR</h2>

      <div class="controles">
        <label class="casilla">
          <input
            type="checkbox"
            [checked]="incluirPuestos()"
            (change)="incluirPuestos.set($any($event.target).checked)"
          />
          Incluir puestos
        </label>

        <label>
          Hoja
          <input
            type="number"
            min="1"
            [value]="hoja()"
            (input)="hoja.set(+$any($event.target).value || 1)"
          />
        </label>

        <button type="button" (click)="generar()" [disabled]="generando() || espacioIds().length === 0">
          {{ generando() ? 'Generando…' : 'Generar plantilla' }}
        </button>
      </div>

      @if (totalHojas() > 0) {
        <p class="estado" role="status">Hoja {{ hoja() }} de {{ totalHojas() }}</p>
      }

      @if (urlSegura(); as url) {
        <object [data]="url" type="image/svg+xml" class="vista" aria-label="Previsualización de la plantilla QR"></object>
        <a [href]="url" [download]="nombreArchivo()">Descargar SVG</a>
      }

      @if (error(); as mensaje) {
        <p class="error" role="alert">{{ mensaje }}</p>
      }
    </section>
  `,
  styles: `
    .panel {
      padding: 1rem;
      background: #fff;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
    }

    h2 {
      margin-top: 0;
    }

    .controles {
      display: flex;
      flex-wrap: wrap;
      align-items: end;
      gap: 0.75rem;
    }

    .casilla {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.9rem;
    }

    .controles label:not(.casilla) {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      font-size: 0.85rem;
      font-weight: 600;
    }

    .vista {
      width: 100%;
      max-width: 32rem;
      height: 22rem;
      margin-top: 1rem;
      border: 1px dashed var(--color-borde);
    }

    .estado {
      margin: 0.75rem 0 0;
      color: #4b5563;
      font-size: 0.9rem;
    }

    .error {
      margin-top: 0.75rem;
      padding: 0.6rem 0.8rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
    }
  `
})
export class PlantillaQrPanel implements OnDestroy {
  private readonly api = inject(EspaciosApi);
  private readonly sanitizador = inject(DomSanitizer);

  readonly espacioIds = input.required<string[]>();
  readonly incluirPuestos = signal(true);
  readonly hoja = signal(1);
  readonly totalHojas = signal(0);
  readonly generando = signal(false);
  readonly error = signal<string | null>(null);
  readonly urlSegura = signal<SafeResourceUrl | null>(null);
  readonly nombreArchivo = computed(() => `plantilla-qr-hoja-${this.hoja()}.svg`);

  private urlObjeto: string | null = null;

  /** Solicita el SVG, crea el objeto URL y lo muestra en el visor. */
  generar(): void {
    if (this.espacioIds().length === 0) {
      return;
    }

    this.generando.set(true);
    this.error.set(null);

    this.api.plantilla(this.espacioIds(), this.incluirPuestos(), this.hoja()).subscribe({
      next: (descarga) => {
        this.generando.set(false);
        this.revocar();
        this.urlObjeto = URL.createObjectURL(descarga.blob);
        this.urlSegura.set(this.sanitizador.bypassSecurityTrustResourceUrl(this.urlObjeto));
        this.totalHojas.set(descarga.totalHojas);
      },
      error: () => {
        this.generando.set(false);
        this.error.set('No fue posible generar la plantilla QR.');
      }
    });
  }

  /** Revoca el objeto URL al destruir el componente. */
  ngOnDestroy(): void {
    this.revocar();
  }

  private revocar(): void {
    if (this.urlObjeto) {
      URL.revokeObjectURL(this.urlObjeto);
      this.urlObjeto = null;
      this.urlSegura.set(null);
    }
  }
}
