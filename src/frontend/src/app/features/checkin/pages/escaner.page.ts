import { Component, OnInit, computed, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import {
  etiquetaEstadoReserva,
  EstadoReserva,
  puedeRegistrarIngreso,
  puedeRegistrarSalida
} from '../../solicitudes-estudiante/models/reserva.models';
import {
  formatearFecha,
  formatearHora,
  formatearRango,
  formatearVentanaCheckIn
} from '../../solicitudes-estudiante/mappers/reserva.mapper';
import { CamaraEscaner } from '../components/camara-escaner';
import { CheckinStore } from '../estado/checkin.store';

/**
 * Pantalla presencial `/checkin/:reservaId`: muestra la franja, la ventana admitida calculada con
 * la tolerancia configurada, registra el ingreso con el QR del puesto y ofrece la salida siempre
 * habilitada mientras la reserva esté en uso, informando el remanente liberado.
 */
@Component({
  selector: 'app-escaner',
  imports: [RouterLink, CamaraEscaner],
  template: `
    <a routerLink="/mis-reservas" class="volver">← Volver a mis reservas</a>

    @if (tienda.error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    }

    @if (tienda.aviso(); as mensaje) {
      <p class="aviso" role="status">{{ mensaje }}</p>
    }

    @if (tienda.remanente(); as mensaje) {
      <p class="aviso remanente" role="status">{{ mensaje }}</p>
    }

    @if (tienda.cargando()) {
      <p class="estado" role="status">Cargando la reserva…</p>
    } @else if (tienda.reserva(); as reserva) {
      <header class="encabezado">
        <h1>Check-in presencial</h1>
        <p class="ayuda">{{ reserva.espacioNombre }} · Puesto {{ reserva.puestoIdentificador }}</p>
      </header>

      <section class="franja">
        <p><strong>Franja reservada:</strong> {{ fecha() }} · {{ rango() }}</p>
        <p><strong>Fin planificado:</strong> {{ horaFinPlanificada() }}</p>

        @if (tienda.ventana(); as ventana) {
          <p>
            <strong>Ventana admitida (±{{ ventana.toleranciaMinutos }} min):</strong>
            {{ ventanaTexto() }}
          </p>
        }

        <p>
          <strong>Estado:</strong>
          <span class="insignia" [attr.data-estado]="reserva.estado">{{ estadoTexto() }}</span>
        </p>

        @if (reserva.fechaCheckIn) {
          <p><strong>Ingreso registrado:</strong> {{ horaCheckIn() }}</p>
        }

        @if (reserva.fechaCheckOut) {
          <p><strong>Salida registrada:</strong> {{ horaCheckOut() }}</p>
        }
      </section>

      @if (ingresoHabilitado()) {
        <app-camara-escaner (codigo)="registrarIngreso($event)" />

        @if (tienda.registrando()) {
          <p class="estado" role="status">Registrando el ingreso…</p>
        }
      }

      @if (salidaHabilitada()) {
        <section class="salida">
          <h2>Salida del puesto</h2>
          <p class="detalle">
            Puede registrar la salida en cualquier momento mientras la reserva esté en uso: el
            remanente de la franja vuelve a estar disponible de inmediato.
          </p>
          <button type="button" [disabled]="tienda.registrando()" (click)="registrarSalida()">
            {{ tienda.registrando() ? 'Registrando…' : 'Registrar salida' }}
          </button>
        </section>
      }

      @if (reserva.estado === finalizada) {
        <p class="nota">
          La franja queda liberada.
          <a [routerLink]="['/solicitudes/puesto', reserva.puestoId]">Ver disponibilidad del puesto</a>
        </p>
      }

      @if (reserva.estado === noShow) {
        <p class="nota">
          La reserva se liberó automáticamente por inasistencia al vencer la ventana de check-in.
        </p>
      }
    } @else if (!tienda.cargando()) {
      <p class="estado" role="status">
        No se encontró la reserva indicada en su agenda. Verifique el enlace recibido.
      </p>
    }
  `,
  styles: `
    .volver {
      display: inline-block;
      margin-bottom: 1rem;
      color: var(--color-primario);
      text-decoration: none;
      font-size: 0.9rem;
    }

    .encabezado h1 {
      margin: 0 0 0.25rem;
    }

    .ayuda {
      margin: 0;
      color: #6b7280;
    }

    .franja {
      margin: 1.25rem 0;
      padding: 1rem 1.1rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
      background: #fff;
    }

    .franja p {
      margin: 0.25rem 0;
      color: #374151;
      font-size: 0.95rem;
    }

    .insignia {
      padding: 0.15rem 0.5rem;
      border-radius: 999px;
      background: #ecfdf5;
      color: #047857;
      font-size: 0.8rem;
    }

    .insignia[data-estado='4'] {
      background: #eff6ff;
      color: #1d4ed8;
    }

    .insignia[data-estado='5'],
    .insignia[data-estado='2'],
    .insignia[data-estado='3'] {
      background: #f3f4f6;
      color: #6b7280;
    }

    .salida {
      margin-top: 1.25rem;
      padding: 1rem 1.1rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
      background: #fff;
    }

    .salida h2 {
      margin: 0 0 0.5rem;
      font-size: 1.05rem;
    }

    .detalle,
    .nota {
      margin: 0 0 0.75rem;
      color: #4b5563;
      font-size: 0.9rem;
    }

    .salida button {
      padding: 0.45rem 1rem;
      border: 1px solid var(--color-primario);
      border-radius: 0.4rem;
      background: var(--color-primario);
      color: #fff;
      font: inherit;
      cursor: pointer;
    }

    .salida button:disabled {
      opacity: 0.6;
      cursor: progress;
    }

    .error {
      padding: 0.8rem 1rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
    }

    .aviso {
      padding: 0.8rem 1rem;
      border-left: 4px solid #10b981;
      background: #ecfdf5;
      color: #047857;
      border-radius: 0.25rem;
    }

    .aviso.remanente {
      border-left-color: #2563eb;
      background: #eff6ff;
      color: #1d4ed8;
    }

    .estado {
      color: #6b7280;
    }
  `
})
export class EscanerPage implements OnInit {
  private readonly ruta = inject(ActivatedRoute);

  readonly tienda = inject(CheckinStore);

  readonly finalizada = EstadoReserva.Finalizada;
  readonly noShow = EstadoReserva.NoShow;

  readonly fecha = computed(() => {
    const reserva = this.tienda.reserva();

    return reserva ? formatearFecha(reserva.inicio) : '';
  });

  readonly rango = computed(() => {
    const reserva = this.tienda.reserva();

    return reserva ? formatearRango(reserva.inicio, reserva.fin) : '';
  });

  readonly horaFinPlanificada = computed(() => {
    const reserva = this.tienda.reserva();

    return reserva ? formatearHora(reserva.finPlanificado) : '';
  });

  readonly horaCheckIn = computed(() => {
    const fecha = this.tienda.reserva()?.fechaCheckIn;

    return fecha ? formatearHora(fecha) : '';
  });

  readonly horaCheckOut = computed(() => {
    const fecha = this.tienda.reserva()?.fechaCheckOut;

    return fecha ? formatearHora(fecha) : '';
  });

  readonly estadoTexto = computed(() => {
    const estado = this.tienda.estado();

    return estado === null ? '' : etiquetaEstadoReserva(estado as EstadoReserva);
  });

  readonly ingresoHabilitado = computed(() => {
    const estado = this.tienda.estado();

    return estado !== null && puedeRegistrarIngreso(estado as EstadoReserva);
  });

  readonly salidaHabilitada = computed(() => {
    const estado = this.tienda.estado();

    return estado !== null && puedeRegistrarSalida(estado as EstadoReserva);
  });

  /** Carga la reserva indicada por la ruta. */
  ngOnInit(): void {
    const reservaId = this.ruta.snapshot.paramMap.get('reservaId') ?? '';

    if (reservaId) {
      this.tienda.cargar(reservaId).subscribe();
    }
  }

  /** Ventana admitida en formato `09:45 - 10:15`. */
  ventanaTexto(): string {
    const ventana = this.tienda.ventana();

    return ventana ? formatearVentanaCheckIn(ventana.desde, ventana.hasta) : '';
  }

  /** Envía el código escaneado o transcrito al check-in. */
  registrarIngreso(codigo: string): void {
    const reserva = this.tienda.reserva();

    if (!reserva || codigo.trim().length === 0) {
      return;
    }

    this.tienda.registrarIngreso(reserva.id, codigo.trim()).subscribe();
  }

  /** Registra la salida del titular en cualquier instante de la sesión. */
  registrarSalida(): void {
    const reserva = this.tienda.reserva();

    if (!reserva) {
      return;
    }

    this.tienda.registrarSalida(reserva.id).subscribe();
  }
}
