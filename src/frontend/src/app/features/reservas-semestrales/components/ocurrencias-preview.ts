import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';

import { OcurrenciaPreview } from '../models/serie.models';

/** Vista previa de las ocurrencias con la omisión y los conflictos del calendario. */
@Component({
  selector: 'app-ocurrencias-preview',
  imports: [DatePipe],
  template: `
    <section class="preview">
      <header>
        <h2>Vista previa</h2>
        <p class="resumen">
          {{ confirmadas() }} ocurrencia(s) · {{ omitidas() }} omitida(s) ·
          {{ conflictos() }} en conflicto
        </p>
      </header>

      @if (ocurrencias().length === 0) {
        <p class="vacio">No hay ocurrencias: revise el patrón y el periodo.</p>
      } @else {
        <ul>
          @for (ocurrencia of ocurrencias(); track ocurrencia.fecha) {
            <li [class.omitida]="ocurrencia.omitida" [class.conflicto]="ocurrencia.conflicto">
              <span class="fecha">{{ ocurrencia.fecha | date: 'EEE dd/MM/yyyy' }}</span>
              <span class="hora">{{ ocurrencia.inicio | date: 'HH:mm' }}–{{ ocurrencia.fin | date: 'HH:mm' }}</span>
              @if (ocurrencia.omitida) {
                <span class="marca">Omitida · {{ ocurrencia.motivo }}</span>
              } @else if (ocurrencia.conflicto) {
                <span class="marca error">Conflicto · {{ ocurrencia.motivo }}</span>
              } @else {
                <span class="marca ok">Se reservará</span>
              }
            </li>
          }
        </ul>
      }
    </section>
  `,
  styles: `
    .preview {
      border: 1px solid var(--color-borde);
      border-radius: 0.5rem;
      padding: 1rem;
      background: #fff;
    }

    header {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 1rem;
      margin-bottom: 0.5rem;
    }

    h2 {
      margin: 0;
      font-size: 1rem;
    }

    .resumen {
      margin: 0;
      font-size: 0.85rem;
      color: #6b7280;
    }

    ul {
      margin: 0;
      padding: 0;
      list-style: none;
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      max-height: 20rem;
      overflow-y: auto;
    }

    li {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.35rem 0.5rem;
      border-radius: 0.35rem;
      font-size: 0.85rem;
      background: #f9fafb;
    }

    li.omitida {
      opacity: 0.7;
      background: #f3f4f6;
    }

    li.conflicto {
      background: #fef2f2;
    }

    .fecha {
      min-width: 8rem;
      font-weight: 600;
    }

    .hora {
      min-width: 6rem;
      color: #4b5563;
    }

    .marca {
      font-size: 0.8rem;
      color: #047857;
    }

    .marca.error {
      color: var(--color-error);
    }

    .vacio {
      margin: 0;
      color: #6b7280;
      font-size: 0.9rem;
    }
  `
})
export class OcurrenciasPreview {
  readonly ocurrencias = input.required<OcurrenciaPreview[]>();

  readonly confirmadas = computed(
    () => this.ocurrencias().filter((ocurrencia) => !ocurrencia.omitida && !ocurrencia.conflicto).length
  );

  readonly omitidas = computed(
    () => this.ocurrencias().filter((ocurrencia) => ocurrencia.omitida).length
  );

  readonly conflictos = computed(
    () => this.ocurrencias().filter((ocurrencia) => ocurrencia.conflicto).length
  );
}
