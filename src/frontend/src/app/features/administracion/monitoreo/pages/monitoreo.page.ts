import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { formatearHora } from '../../../solicitudes-estudiante/mappers/reserva.mapper';
import { AreaTablero } from '../components/area-tablero';
import { TotalesOcupacionPanel } from '../components/totales-ocupacion';
import { MonitoreoStore } from '../estado/monitoreo.store';

/**
 * Tablero administrativo de ocupación por área. Sondea `GET /api/monitoreo/ocupacion-actual`
 * cada 15 segundos, muestra los totales institucionales y el desglose por área con sus estados
 * (libre, en uso, pendiente de check-in y sesión sin cerrar).
 */
@Component({
  selector: 'app-monitoreo',
  imports: [TotalesOcupacionPanel, AreaTablero],
  template: `
    <header class="encabezado">
      <h1>Monitoreo de ocupación</h1>
      <p class="ayuda">
        Ocupación real del campus, actualizada cada 15 segundos. La tolerancia de check-in vigente es
        de {{ tolerancia() }} minutos.
      </p>
    </header>

    @if (tienda.error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    }

    @if (tienda.cargando()) {
      <p class="estado" role="status">Consultando la ocupación…</p>
    }

    @if (tienda.ocupacion(); as ocupacion) {
      <app-totales-ocupacion [totales]="ocupacion.totales" />

      <p class="actualizado" role="status">
        Datos generados a las {{ hora(ocupacion.generadoEn) }}
        @if (actualizado()) {
          · última consulta {{ actualizado() }}
        }
      </p>

      @if (ocupacion.areas.length === 0) {
        <p class="vacio">No hay áreas registradas en el catálogo.</p>
      } @else {
        <div class="tableros">
          @for (area of ocupacion.areas; track area.espacioId) {
            <app-area-tablero [area]="area" />
          }
        </div>
      }
    } @else if (!tienda.cargando() && !tienda.error()) {
      <p class="vacio" role="status">Aún no hay datos de ocupación disponibles.</p>
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

    .actualizado,
    .vacio,
    .estado {
      color: #6b7280;
      font-size: 0.85rem;
    }

    .error {
      padding: 0.8rem 1rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
    }

    .tableros {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(20rem, 1fr));
      gap: 1rem;
      margin-top: 0.75rem;
    }
  `
})
export class MonitoreoPage implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  readonly tienda = inject(MonitoreoStore);

  /** Arranca el sondeo y lo detiene al salir de la página. */
  ngOnInit(): void {
    this.tienda
      .iniciar()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();
  }

  /** Tolerancia de check-in aplicada por la API en el último dato recibido. */
  tolerancia(): number {
    return this.tienda.ocupacion()?.toleranciaMinutos ?? 15;
  }

  /** Hora local del instante de generación del dato. */
  hora(instante: string): string {
    return formatearHora(instante);
  }

  /** Hora de la última consulta realizada por la SPA. */
  actualizado(): string {
    const fecha = this.tienda.actualizadoEn();

    return fecha
      ? fecha.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
      : '';
  }
}
