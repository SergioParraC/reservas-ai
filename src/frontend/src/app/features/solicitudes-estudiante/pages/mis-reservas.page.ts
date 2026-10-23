import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';

import { ReservaCard } from '../components/reserva-card';
import { ReservasStore } from '../estado/reservas.store';
import { ESTADOS_RESERVA, EstadoReserva, ReservaResumen } from '../models/reserva.models';

/** Agenda del titular con filtro por estado, paginación y cancelación de reservas. */
@Component({
  selector: 'app-mis-reservas',
  imports: [ReactiveFormsModule, ReservaCard],
  template: `
    <header class="encabezado">
      <h1>Mis reservas</h1>
      <p class="ayuda">Consulte y cancele sus reservas de puestos de cómputo.</p>
    </header>

    <form class="filtros" [formGroup]="filtros">
      <label>
        Estado
        <select formControlName="estado" (change)="aplicar()">
          <option value="">Todos</option>
          @for (estado of estados; track estado.valor) {
            <option [value]="estado.valor">{{ estado.etiqueta }}</option>
          }
        </select>
      </label>
    </form>

    @if (tienda.error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    }

    @if (tienda.aviso(); as mensaje) {
      <p class="aviso" role="status">{{ mensaje }}</p>
    }

    @if (tienda.cargandoReservas()) {
      <p role="status" class="estado">Cargando reservas…</p>
    } @else if (tienda.reservas(); as pagina) {
      @if (pagina.elementos.length === 0) {
        <p class="vacio" role="status">Aún no tiene reservas registradas.</p>
      } @else {
        <p class="total">{{ pagina.total }} reserva(s)</p>

        <div class="rejilla">
          @for (reserva of pagina.elementos; track reserva.id) {
            <app-reserva-card
              [reserva]="reserva"
              [cancelando]="tienda.cancelando() === reserva.id"
              (cancelar)="cancelar($event)"
            />
          }
        </div>

        <nav class="paginacion" aria-label="Paginación de reservas">
          <button type="button" (click)="ir(pagina.pagina - 1)" [disabled]="pagina.pagina <= 1">Anterior</button>
          <span>Página {{ pagina.pagina }} de {{ tienda.totalPaginas() }}</span>
          <button
            type="button"
            (click)="ir(pagina.pagina + 1)"
            [disabled]="pagina.pagina >= tienda.totalPaginas()"
          >
            Siguiente
          </button>
        </nav>
      }
    }
  `,
  styles: `
    .encabezado h1 {
      margin: 0 0 0.25rem;
    }

    .ayuda {
      margin: 0;
      color: #6b7280;
    }

    .filtros {
      margin: 1.25rem 0 1rem;
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      max-width: 14rem;
      font-size: 0.9rem;
      color: #374151;
    }

    select {
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      font: inherit;
    }

    .rejilla {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(17rem, 1fr));
      gap: 1rem;
      margin-top: 0.75rem;
    }

    .paginacion {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 1rem;
      margin-top: 1.5rem;
    }

    .paginacion button {
      padding: 0.4rem 0.9rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      background: #fff;
      font: inherit;
      cursor: pointer;
    }

    .paginacion button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
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

    .estado,
    .vacio,
    .total {
      color: #6b7280;
    }
  `
})
export class MisReservasPage implements OnInit {
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly tienda = inject(ReservasStore);
  readonly estados = ESTADOS_RESERVA;

  readonly filtros = this.formularioReactivo.group({
    estado: ''
  });

  /** Carga la primera página al entrar a la vista. */
  ngOnInit(): void {
    this.cargar(1);
  }

  /** Reaplica el filtro de estado desde la primera página. */
  aplicar(): void {
    this.cargar(1);
  }

  /** Navega a la página indicada si está dentro del rango. */
  ir(pagina: number): void {
    if (pagina < 1 || pagina > this.tienda.totalPaginas()) {
      return;
    }

    this.cargar(pagina);
  }

  /** Pide confirmación y cancela la reserva propia. */
  cancelar(reserva: ReservaResumen): void {
    const confirmado = window.confirm(
      `¿Cancelar la reserva del puesto ${reserva.puestoIdentificador}?`
    );

    if (!confirmado) {
      return;
    }

    this.tienda.cancelar(reserva.id).subscribe((cancelada) => {
      if (cancelada) {
        this.cargar(this.paginaActual());
      }
    });
  }

  private paginaActual(): number {
    return this.tienda.reservas()?.pagina ?? 1;
  }

  private cargar(pagina: number): void {
    const valor = this.filtros.getRawValue().estado;
    const estado = valor ? (Number(valor) as EstadoReserva) : null;

    this.tienda.cargarMisReservas({ estado, pagina, tamano: 10 }).subscribe();
  }
}
