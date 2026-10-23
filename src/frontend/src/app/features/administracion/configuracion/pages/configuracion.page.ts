import { Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CalendarioResumen } from '../components/calendario-resumen';
import { FranjaTabla } from '../components/franja-tabla';
import { PeriodoCard } from '../components/periodo-card';
import { CalendarioStore } from '../estado/calendario.store';

/** Administración del periodo académico vigente y de las franjas no operativas globales. */
@Component({
  selector: 'app-configuracion',
  imports: [RouterLink, PeriodoCard, FranjaTabla, CalendarioResumen],
  template: `
    <header class="encabezado">
      <h1>Configuración del calendario</h1>
      <p class="ayuda">
        Solo puede existir un periodo académico activo. Las franjas no operativas aplican a todo el
        campus y bloquean las reservas, series y solicitudes.
      </p>
    </header>

    @if (tienda.error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    }

    @if (tienda.aviso(); as mensaje) {
      <p class="aviso" role="status">{{ mensaje }}</p>
    }

    <app-calendario-resumen class="resumen" />

    <section class="bloque">
      <div class="titulo">
        <h2>Periodos académicos</h2>
        <a routerLink="/administracion/configuracion/periodos/nuevo" class="accion">Nuevo periodo</a>
      </div>

      @if (tienda.cargando()) {
        <p role="status">Cargando periodos…</p>
      } @else if (tienda.periodos().length === 0) {
        <p class="vacio">Aún no hay periodos registrados.</p>
      } @else {
        <div class="tarjetas">
          @for (periodo of tienda.periodos(); track periodo.id) {
            <app-periodo-card [periodo]="periodo" (activar)="activar($event)" />
          }
        </div>
      }
    </section>

    <section class="bloque">
      <div class="titulo">
        <h2>Franjas no operativas</h2>
        <a routerLink="/administracion/configuracion/franjas/nueva" class="accion">Nueva franja</a>
      </div>

      @if (tienda.franjas().length === 0) {
        <p class="vacio">No hay franjas no operativas registradas.</p>
      } @else {
        <app-franja-tabla [franjas]="tienda.franjas()" (eliminar)="eliminar($event)" />
      }
    </section>
  `,
  styles: `
    .encabezado h1 {
      margin: 0 0 0.25rem;
    }

    .ayuda {
      margin: 0;
      color: #6b7280;
      max-width: 52rem;
    }

    .resumen {
      display: block;
      margin: 1.25rem 0;
    }

    .bloque {
      margin-top: 1.75rem;
    }

    .titulo {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      margin-bottom: 0.75rem;
    }

    .titulo h2 {
      margin: 0;
      font-size: 1.1rem;
    }

    .accion {
      color: var(--color-primario);
      text-decoration: none;
      font-size: 0.9rem;
    }

    .tarjetas {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr));
      gap: 1rem;
    }

    .vacio {
      color: #6b7280;
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
  `
})
export class ConfiguracionPage implements OnInit {
  readonly tienda = inject(CalendarioStore);

  /** Carga periodos, franjas y calendario vigente. */
  ngOnInit(): void {
    this.tienda.cargar().subscribe();
  }

  /** Activa el periodo seleccionado y desactiva al vigente. */
  activar(id: string): void {
    this.tienda.activarPeriodo(id).subscribe();
  }

  /** Elimina una franja no operativa tras confirmación. */
  eliminar(id: string): void {
    const confirmado = window.confirm('¿Eliminar esta franja no operativa?');

    if (confirmado) {
      this.tienda.eliminarFranja(id).subscribe();
    }
  }
}
