import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, of, switchMap, tap, timer } from 'rxjs';

import { OcupacionActual } from '../models/monitoreo.models';
import { MonitoreoApi } from '../services/monitoreo.api';

/** Cadencia del sondeo del tablero: 15 segundos, sin WebSocket. */
export const INTERVALO_ACTUALIZACION_MS = 15_000;

/** Mensaje del fallo de actualización; el siguiente ciclo reintenta solo. */
export const ERROR_OCUPACION =
  'No fue posible actualizar el mapa de ocupación. Se reintentará en el próximo ciclo.';

/**
 * Estado del mapa de ocupación con sondeo periódico (`timer(0, 15000)` + `switchMap`) y Signals
 * para la carga, el error y el instante de la última actualización.
 */
@Injectable({ providedIn: 'root' })
export class MonitoreoStore {
  private readonly api = inject(MonitoreoApi);

  readonly ocupacion = signal<OcupacionActual | null>(null);
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);
  readonly actualizadoEn = signal<Date | null>(null);

  /**
   * Inicia el sondeo: el primer ciclo corre de inmediato y los siguientes cada `intervaloMs`.
   * Un fallo de red se publica en `error` y no cancela el flujo, de modo que el tablero se
   * recupera solo en el siguiente ciclo.
   */
  iniciar(intervaloMs: number = INTERVALO_ACTUALIZACION_MS): Observable<OcupacionActual | null> {
    this.cargando.set(this.ocupacion() === null);
    this.error.set(null);

    return timer(0, intervaloMs).pipe(
      switchMap(() =>
        this.api.ocupacionActual().pipe(
          tap({
            next: (ocupacion) => {
              this.ocupacion.set(ocupacion);
              this.actualizadoEn.set(new Date());
              this.error.set(null);
              this.cargando.set(false);
            },
            error: () => {
              this.error.set(ERROR_OCUPACION);
              this.cargando.set(false);
            }
          }),
          catchError(() => of(null))
        )
      )
    );
  }

  /** Limpia el error vigente antes de forzar una nueva consulta. */
  limpiarError(): void {
    this.error.set(null);
  }
}
