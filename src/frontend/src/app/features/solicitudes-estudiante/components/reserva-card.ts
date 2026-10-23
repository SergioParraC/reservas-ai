import { Component, EventEmitter, Input, Output } from '@angular/core';

import { formatearFecha, formatearRango } from '../mappers/reserva.mapper';
import { EstadoReserva, etiquetaEstadoReserva, ReservaResumen } from '../models/reserva.models';

/** Tarjeta de una reserva del titular con la acción de cancelación cuando procede. */
@Component({
  selector: 'app-reserva-card',
  template: `
    <article class="tarjeta">
      <header class="cabecera">
        <h3>{{ reserva.espacioNombre }}</h3>
        <span class="insignia" [class.cancelada]="reserva.estado !== vigente">{{ estado() }}</span>
      </header>

      <p class="puesto">Puesto {{ reserva.puestoIdentificador }}</p>
      <p class="horario">{{ fecha() }}</p>
      <p class="horario">{{ rango() }}</p>

      @if (reserva.estado === vigente) {
        @if (reserva.cancelable) {
          <button type="button" [disabled]="cancelando" (click)="solicitarCancelacion()">
            {{ cancelando ? 'Cancelando…' : 'Cancelar reserva' }}
          </button>
        } @else {
          <p class="nota" role="status">
            Ya no puede cancelarse: falta menos del plazo mínimo de antelación.
          </p>
        }
      }
    </article>
  `,
  styles: `
    .tarjeta {
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
      background: #fff;
      padding: 1rem 1.1rem;
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
    }

    .cabecera {
      display: flex;
      justify-content: space-between;
      align-items: start;
      gap: 0.5rem;
    }

    h3 {
      margin: 0;
      font-size: 1rem;
    }

    .insignia {
      padding: 0.15rem 0.5rem;
      border-radius: 999px;
      background: #ecfdf5;
      color: #047857;
      font-size: 0.75rem;
      white-space: nowrap;
    }

    .insignia.cancelada {
      background: #f3f4f6;
      color: #6b7280;
    }

    .puesto,
    .horario {
      margin: 0;
      color: #4b5563;
      font-size: 0.9rem;
    }

    button {
      align-self: flex-start;
      margin-top: 0.5rem;
      padding: 0.4rem 0.9rem;
      border: 1px solid var(--color-error);
      border-radius: 0.4rem;
      background: #fff;
      color: var(--color-error);
      font: inherit;
      cursor: pointer;
    }

    button:disabled {
      opacity: 0.6;
      cursor: progress;
    }

    .nota {
      margin: 0.5rem 0 0;
      color: #92400e;
      font-size: 0.85rem;
    }
  `
})
export class ReservaCard {
  /** Estado vigente usado para habilitar la cancelación. */
  readonly vigente = EstadoReserva.Confirmada;

  @Input({ required: true }) reserva!: ReservaResumen;

  @Input() cancelando = false;

  @Output() readonly cancelar = new EventEmitter<ReservaResumen>();

  /** Etiqueta legible del estado de la reserva. */
  estado(): string {
    return etiquetaEstadoReserva(this.reserva.estado);
  }

  /** Fecha local de la reserva. */
  fecha(): string {
    return formatearFecha(this.reserva.inicio);
  }

  /** Rango horario de la reserva. */
  rango(): string {
    return formatearRango(this.reserva.inicio, this.reserva.fin);
  }

  /** Emite la reserva que el titular decidió cancelar. */
  solicitarCancelacion(): void {
    this.cancelar.emit(this.reserva);
  }
}
