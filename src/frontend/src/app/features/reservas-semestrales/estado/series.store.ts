import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, tap } from 'rxjs';

import { mensajeDeProblema } from '../../../core/errores/mensajes-problema';
import { PaginaSeries, SerieDetalle, SolicitudSerie } from '../models/serie.models';
import { ReservasRecurrentesApi } from '../services/reservas-recurrentes.api';

/** Aviso de confirmación de una serie. */
export const AVISO_SERIE_CREADA = 'Serie confirmada. Revise sus ocurrencias en "Mis series".';

/**
 * Estado de las series semestrales: listado propio, alta y mensajes derivados del contrato
 * RFC 7807 (`colision-serie`, `franja-no-operativa`, `fuera-de-periodo-academico`, …).
 */
@Injectable({ providedIn: 'root' })
export class SeriesStore {
  private readonly api = inject(ReservasRecurrentesApi);

  readonly series = signal<PaginaSeries | null>(null);
  readonly ultimaSerie = signal<SerieDetalle | null>(null);
  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);

  readonly totalPaginas = computed(() => {
    const pagina = this.series();

    return pagina ? Math.max(1, Math.ceil(pagina.total / pagina.tamano)) : 1;
  });

  /** Carga las series propias de la página indicada. */
  cargarSeries(pagina = 1, tamano = 10): Observable<PaginaSeries | null> {
    this.cargando.set(true);
    this.error.set(null);

    return this.api.misSeries(pagina, tamano).pipe(
      tap({
        next: (paginaSeries) => this.series.set(paginaSeries),
        error: (error: unknown) => this.error.set(mensajeDeProblema(error))
      }),
      catchError(() => of(null)),
      finalize(() => this.cargando.set(false))
    );
  }

  /** Radica la serie completa y publica el resultado. */
  crear(solicitud: SolicitudSerie): Observable<SerieDetalle | null> {
    this.guardando.set(true);
    this.error.set(null);
    this.aviso.set(null);

    return this.api.crear(solicitud).pipe(
      tap({
        next: (serie) => {
          this.ultimaSerie.set(serie);
          this.aviso.set(AVISO_SERIE_CREADA);
        },
        error: (error: unknown) => this.error.set(mensajeDeProblema(error))
      }),
      catchError(() => of(null)),
      finalize(() => this.guardando.set(false))
    );
  }

  /** Limpia los mensajes vigentes. */
  limpiarMensajes(): void {
    this.error.set(null);
    this.aviso.set(null);
  }
}
