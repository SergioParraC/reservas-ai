import { Component, EventEmitter, Input, Output } from '@angular/core';

import { etiquetaMotivo, FranjaDisponible } from '../models/reserva.models';
import { formatearRango } from '../mappers/reserva.mapper';

/** Lista seleccionable de franjas del puesto; las ocupadas quedan deshabilitadas. */
@Component({
  selector: 'app-franja-lista',
  template: `
    @if (franjas.length === 0) {
      <p class="vacio" role="status">No hay franjas disponibles para la fecha y duración seleccionadas.</p>
    } @else {
      <ul class="rejilla">
        @for (franja of franjas; track franja.inicio) {
          <li>
            <button
              type="button"
              class="franja"
              [class.ocupada]="!franja.disponible"
              [class.seleccionada]="esSeleccionada(franja)"
              [disabled]="!franja.disponible"
              [attr.aria-pressed]="esSeleccionada(franja)"
              (click)="elegir(franja)"
            >
              <span class="hora">{{ rango(franja) }}</span>
              <span class="estado">{{ franja.disponible ? 'Disponible' : motivo(franja.motivo) }}</span>
            </button>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .rejilla {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(11rem, 1fr));
      gap: 0.6rem;
      list-style: none;
      margin: 1rem 0;
      padding: 0;
    }

    .franja {
      width: 100%;
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      padding: 0.7rem 0.85rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.5rem;
      background: #fff;
      /* El botón global usa texto blanco; aquí el fondo es claro y debe conservar contraste. */
      color: #1f2937;
      cursor: pointer;
      text-align: left;
      font: inherit;
      transition:
        border-color 0.15s ease,
        box-shadow 0.15s ease,
        background 0.15s ease;
    }

    .franja:hover:not(:disabled) {
      border-color: var(--color-primario);
      background: #eff6ff;
      box-shadow: 0 2px 8px rgba(29, 78, 216, 0.15);
    }

    .franja:focus-visible {
      outline: 2px solid var(--color-primario);
      outline-offset: 2px;
    }

    .franja.seleccionada {
      border-color: var(--color-primario);
      background: #eff6ff;
      box-shadow: 0 0 0 1px var(--color-primario) inset;
    }

    .franja:disabled {
      cursor: not-allowed;
      background: #f9fafb;
      color: #9ca3af;
    }

    .hora {
      font-weight: 600;
    }

    .estado {
      font-size: 0.8rem;
      color: #047857;
    }

    .franja.ocupada .estado {
      color: #b91c1c;
    }

    .vacio {
      margin: 1rem 0;
      color: #6b7280;
    }
  `
})
export class FranjaLista {
  @Input({ required: true }) franjas: FranjaDisponible[] = [];

  @Input() seleccionada: FranjaDisponible | null = null;

  @Output() readonly seleccionar = new EventEmitter<FranjaDisponible>();

  /** Rango legible de la franja. */
  rango(franja: FranjaDisponible): string {
    return formatearRango(franja.inicio, franja.fin);
  }

  /** Motivo legible cuando la franja no está disponible. */
  motivo(valor: string | null): string {
    return etiquetaMotivo(valor);
  }

  /** Indica si la franja es la seleccionada actualmente. */
  esSeleccionada(franja: FranjaDisponible): boolean {
    return this.seleccionada?.inicio === franja.inicio;
  }

  /** Emite la franja elegida al contenedor. */
  elegir(franja: FranjaDisponible): void {
    if (franja.disponible) {
      this.seleccionar.emit(franja);
    }
  }
}
