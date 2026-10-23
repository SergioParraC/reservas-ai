import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { FranjaLista } from '../components/franja-lista';
import { ReservasStore } from '../estado/reservas.store';
import { aFechaIso, sumarDias } from '../mappers/reserva.mapper';
import { DURACIONES, FranjaDisponible, ReservaDetalle } from '../models/reserva.models';

/** Selección de fecha, duración y franja, con confirmación inmediata de la reserva. */
@Component({
  selector: 'app-puesto-disponibilidad',
  imports: [ReactiveFormsModule, RouterLink, FranjaLista],
  template: `
    <a routerLink="/catalogo" class="volver">← Volver al catálogo</a>

    <header class="encabezado">
      <h1>Reservar un puesto de cómputo</h1>
      @if (tienda.disponibilidad(); as datos) {
        <p class="ayuda">{{ datos.espacioNombre }} · Puesto {{ datos.identificador }}</p>
      }
    </header>

    <form class="filtros" [formGroup]="formulario">
      <label>
        Fecha
        <input type="date" formControlName="fecha" [min]="hoy" />
      </label>

      <label>
        Duración
        <select formControlName="duracionMinutos">
          @for (duracion of duraciones; track duracion.valor) {
            <option [value]="duracion.valor">{{ duracion.etiqueta }}</option>
          }
        </select>
      </label>
    </form>

    @if (tienda.error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    }

    @if (confirmada(); as reserva) {
      <section class="confirmacion" role="status">
        <h2>Reserva confirmada</h2>
        <p>Puesto {{ reserva.puestoIdentificador }} · {{ reserva.espacioNombre }}</p>
        <p>{{ fechaConfirmada() }} · {{ rangoConfirmado() }}</p>
        <a routerLink="/mis-reservas" class="enlace">Ver mis reservas</a>
      </section>
    }

    @if (tienda.cargando()) {
      <p role="status" class="estado">Consultando disponibilidad…</p>
    } @else if (tienda.disponibilidad(); as datos) {
      <app-franja-lista
        [franjas]="datos.franjas"
        [seleccionada]="franja()"
        (seleccionar)="seleccionar($event)"
      />

      <div class="acciones">
        <button type="button" class="confirmar" [disabled]="!franja() || enviando()" (click)="confirmar()">
          {{ enviando() ? 'Confirmando…' : 'Confirmar reserva' }}
        </button>

        @if (franja(); as elegida) {
          <span class="seleccion">Franja elegida: {{ rangoFranja(elegida) }}</span>
        } @else {
          <span class="seleccion">Seleccione una franja disponible.</span>
        }
      </div>
    } @else if (!tienda.error()) {
      <p role="status" class="estado">Sin información de disponibilidad.</p>
    }
  `,
  styles: `
    .volver {
      display: inline-block;
      margin-bottom: 1rem;
      color: var(--color-primario);
      text-decoration: none;
    }

    .encabezado h1 {
      margin: 0 0 0.25rem;
    }

    .ayuda {
      margin: 0;
      color: #6b7280;
    }

    .filtros {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      margin: 1.25rem 0 0.5rem;
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      font-size: 0.9rem;
      color: #374151;
    }

    input,
    select {
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      font: inherit;
    }

    .acciones {
      display: flex;
      align-items: center;
      gap: 1rem;
      flex-wrap: wrap;
      margin-top: 0.5rem;
    }

    .confirmar {
      padding: 0.55rem 1.1rem;
      border: none;
      border-radius: 0.45rem;
      background: var(--color-primario);
      color: #fff;
      font: inherit;
      cursor: pointer;
    }

    .confirmar:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }

    .seleccion {
      color: #4b5563;
      font-size: 0.9rem;
    }

    .estado {
      color: #6b7280;
    }

    .error {
      margin-top: 1rem;
      padding: 0.8rem 1rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
    }

    .confirmacion {
      margin-top: 1rem;
      padding: 1rem 1.2rem;
      border: 1px solid #a7f3d0;
      background: #ecfdf5;
      border-radius: 0.5rem;
    }

    .confirmacion h2 {
      margin: 0 0 0.4rem;
      font-size: 1.05rem;
      color: #047857;
    }

    .confirmacion p {
      margin: 0.15rem 0;
    }

    .enlace {
      display: inline-block;
      margin-top: 0.6rem;
      color: var(--color-primario);
    }
  `
})
export class PuestoDisponibilidadPage implements OnInit {
  private readonly ruta = inject(ActivatedRoute);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly tienda = inject(ReservasStore);
  readonly duraciones = DURACIONES;
  readonly hoy = aFechaIso();
  readonly puestoId = signal<string | null>(this.ruta.snapshot.paramMap.get('puestoId'));
  readonly franja = signal<FranjaDisponible | null>(null);
  readonly confirmada = signal<ReservaDetalle | null>(null);
  readonly enviando = signal(false);

  readonly formulario = this.formularioReactivo.group({
    fecha: [aFechaIso(sumarDias(new Date(), 1)), [Validators.required]],
    duracionMinutos: [60, [Validators.required]]
  });

  /** Consulta la disponibilidad inicial y la recalcula al cambiar fecha o duración. */
  ngOnInit(): void {
    if (!this.puestoId()) {
      this.tienda.error.set('El puesto solicitado no existe.');
      return;
    }

    this.consultar();
    this.formulario.valueChanges.subscribe(() => {
      this.franja.set(null);
      this.consultar();
    });
  }

  /** Selecciona una franja disponible. */
  seleccionar(franja: FranjaDisponible): void {
    this.franja.set(franja);
    this.confirmada.set(null);
  }

  /** Confirma la reserva de la franja elegida. */
  confirmar(): void {
    const puesto = this.puestoId();
    const elegida = this.franja();

    if (!puesto || !elegida) {
      return;
    }

    this.enviando.set(true);
    this.tienda.reservar({ puestoId: puesto, inicio: elegida.inicio, fin: elegida.fin }).subscribe({
      next: (reserva) => {
        this.enviando.set(false);

        if (reserva) {
          this.confirmada.set(reserva);
          this.franja.set(null);
          this.consultar();
        }
      },
      error: () => this.enviando.set(false)
    });
  }

  /** Rango legible de la franja elegida. */
  rangoFranja(franja: FranjaDisponible): string {
    return `${this.hora(franja.inicio)} - ${this.hora(franja.fin)}`;
  }

  /** Fecha de la reserva confirmada. */
  fechaConfirmada(): string {
    const reserva = this.confirmada();

    return reserva ? this.formatear(reserva.inicio) : '';
  }

  /** Rango de la reserva confirmada. */
  rangoConfirmado(): string {
    const reserva = this.confirmada();

    return reserva ? `${this.hora(reserva.inicio)} - ${this.hora(reserva.fin)}` : '';
  }

  private consultar(): void {
    const puesto = this.puestoId();

    if (!puesto) {
      return;
    }

    const valores = this.formulario.getRawValue();
    this.tienda
      .cargarDisponibilidad(puesto, {
        fecha: valores.fecha,
        duracionMinutos: valores.duracionMinutos
      })
      .subscribe();
  }

  private hora(instante: string): string {
    return new Intl.DateTimeFormat('es-CO', {
      timeZone: 'America/Bogota',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false
    }).format(new Date(instante));
  }

  private formatear(instante: string): string {
    return new Intl.DateTimeFormat('es-CO', {
      timeZone: 'America/Bogota',
      weekday: 'long',
      day: '2-digit',
      month: 'long'
    }).format(new Date(instante));
  }
}
