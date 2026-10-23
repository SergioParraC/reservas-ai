import { DatePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';

import { PeriodoAcademico } from '../models/calendario.models';

/** Tarjeta de un periodo académico con su estado y la acción de activación. */
@Component({
  selector: 'app-periodo-card',
  imports: [DatePipe],
  template: `
    <article class="tarjeta" [class.activo]="periodo().estadoActivo">
      <header>
        <h3>{{ periodo().nombre }}</h3>
        <span class="estado">{{ periodo().estadoActivo ? 'Activo' : 'Inactivo' }}</span>
      </header>

      <p class="rango">
        {{ periodo().fechaInicio | date: 'dd/MM/yyyy' }} — {{ periodo().fechaFin | date: 'dd/MM/yyyy' }}
      </p>

      <footer>
        @if (!periodo().estadoActivo) {
          <button type="button" (click)="activar.emit(periodo().id)">Activar periodo</button>
        } @else {
          <span class="vigente">Periodo vigente</span>
        }
      </footer>
    </article>
  `,
  styles: `
    .tarjeta {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      padding: 1rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.6rem;
      background: #fff;
    }

    .tarjeta.activo {
      border-color: #10b981;
      box-shadow: inset 0 0 0 1px #10b981;
    }

    header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.5rem;
    }

    h3 {
      margin: 0;
      font-size: 1rem;
    }

    .estado {
      font-size: 0.75rem;
      padding: 0.15rem 0.5rem;
      border-radius: 999px;
      background: #f3f4f6;
      color: #4b5563;
    }

    .activo .estado {
      background: #ecfdf5;
      color: #047857;
    }

    .rango {
      margin: 0;
      color: #6b7280;
      font-size: 0.9rem;
    }

    button {
      padding: 0.4rem 0.8rem;
      border: 1px solid var(--color-primario);
      border-radius: 0.4rem;
      background: #fff;
      color: var(--color-primario);
      font: inherit;
      cursor: pointer;
    }

    .vigente {
      font-size: 0.85rem;
      color: #047857;
    }
  `
})
export class PeriodoCard {
  readonly periodo = input.required<PeriodoAcademico>();
  readonly activar = output<string>();
}
