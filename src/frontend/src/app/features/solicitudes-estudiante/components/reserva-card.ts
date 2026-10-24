import { Component, EventEmitter, Input, Output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { formatearFecha, formatearHora, formatearRango } from '../mappers/reserva.mapper';
import {
  EstadoReserva,
  etiquetaEstadoReserva,
  puedeRegistrarSalida,
  ReservaResumen
} from '../models/reserva.models';

/**
 * Tarjeta de una reserva del titular con el estado de asistencia, las marcas de check-in/out y las
 * acciones de cancelación, ingreso con QR y salida anticipada.
 */
@Component({
  selector: 'app-reserva-card',
  imports: [RouterLink],
  template: `
    <article class="tarjeta">
      <header class="cabecera">
        <h3>{{ reserva.espacioNombre }}</h3>
        <span class="insignia" [attr.data-estado]="reserva.estado">{{ estado() }}</span>
      </header>

      <p class="puesto">Puesto {{ reserva.puestoIdentificador }}</p>
      <p class="horario">{{ fecha() }}</p>
      <p class="horario">{{ rango() }}</p>

      @if (remanenteVisible()) {
        <p class="horario">Fin planificado: {{ horaFinPlanificada() }}</p>
      }

      @if (reserva.fechaCheckIn) {
        <p class="marca">Ingreso: {{ horaCheckIn() }}</p>
      }

      @if (reserva.fechaCheckOut) {
        <p class="marca">Salida: {{ horaCheckOut() }}</p>
      }

      @if (reserva.estado === confirmada) {
        @if (reserva.cancelable) {
          <button type="button" [disabled]="cancelando" (click)="solicitarCancelacion()">
            {{ cancelando ? 'Cancelando…' : 'Cancelar reserva' }}
          </button>
        } @else {
          <p class="nota" role="status">
            Ya no puede cancelarse: falta menos del plazo mínimo de antelación.
          </p>
        }

        <a class="accion" [routerLink]="['/checkin', reserva.id]">Escanear para ingresar</a>
      }

      @if (reserva.estado === enUso) {
        <a class="accion salida" [routerLink]="['/checkin', reserva.id]">Registrar salida</a>
      }

      @if (reserva.estado === noShow) {
        <p class="nota" role="status">
          Liberada por inasistencia: la franja volvió a estar disponible.
        </p>
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

    .insignia[data-estado='4'] {
      background: #eff6ff;
      color: #1d4ed8;
    }

    .insignia[data-estado='5'] {
      background: #f3f4f6;
      color: #374151;
    }

    .insignia[data-estado='2'],
    .insignia[data-estado='3'] {
      background: #f3f4f6;
      color: #6b7280;
    }

    .puesto,
    .horario,
    .marca {
      margin: 0;
      color: #4b5563;
      font-size: 0.9rem;
    }

    .marca {
      color: #047857;
      font-size: 0.85rem;
    }

    .accion {
      align-self: flex-start;
      margin-top: 0.5rem;
      padding: 0.4rem 0.9rem;
      border: 1px solid var(--color-primario);
      border-radius: 0.4rem;
      color: var(--color-primario);
      text-decoration: none;
      font-size: 0.9rem;
    }

    .accion.salida {
      background: var(--color-primario);
      color: #fff;
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
  /** Estado que habilita la cancelación y el acceso al escáner. */
  readonly confirmada = EstadoReserva.Confirmada;

  /** Estado de la sesión abierta. */
  readonly enUso = EstadoReserva.EnUso;

  /** Estado liberado por inasistencia. */
  readonly noShow = EstadoReserva.NoShow;

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

  /** Rango horario efectivo de la reserva. */
  rango(): string {
    return formatearRango(this.reserva.inicio, this.reserva.fin);
  }

  /** Hora del fin planificado, visible cuando la franja se acortó. */
  horaFinPlanificada(): string {
    return formatearHora(this.reserva.finPlanificado);
  }

  /** Indica si la franja efectiva difiere de la planificada. */
  remanenteVisible(): boolean {
    return this.reserva.fin !== this.reserva.finPlanificado;
  }

  /** Hora del ingreso registrado. */
  horaCheckIn(): string {
    return this.reserva.fechaCheckIn ? formatearHora(this.reserva.fechaCheckIn) : '';
  }

  /** Hora de la salida registrada. */
  horaCheckOut(): string {
    return this.reserva.fechaCheckOut ? formatearHora(this.reserva.fechaCheckOut) : '';
  }

  /** Indica si la salida está habilitada (solo en `EnUso`). */
  salidaHabilitada(): boolean {
    return puedeRegistrarSalida(this.reserva.estado);
  }

  /** Emite la reserva que el titular decidió cancelar. */
  solicitarCancelacion(): void {
    this.cancelar.emit(this.reserva);
  }
}
