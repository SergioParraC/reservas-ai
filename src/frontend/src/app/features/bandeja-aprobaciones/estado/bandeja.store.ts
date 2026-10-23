import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, tap } from 'rxjs';

import { mensajeDeProblema } from '../../../core/errores/mensajes-problema';
import {
  ComandoEstadoSolicitud,
  FiltrosBandeja,
  PaginaSolicitudes,
  SolicitudBandejaItem,
  SolicitudDetalle
} from '../../../core/solicitudes/solicitudes.models';
import { SolicitudesApi } from '../../../core/solicitudes/solicitudes.api';

/** Aviso de decisión registrada. */
export const AVISO_DECISION = 'Decisión registrada. El solicitante recibirá una notificación por correo.';

/**
 * Estado de la bandeja de aprobaciones: página filtrada por competencia, decisión del revisor
 * y mensajes derivados del contrato RFC 7807.
 */
@Injectable({ providedIn: 'root' })
export class BandejaStore {
  private readonly api = inject(SolicitudesApi);

  readonly pagina = signal<PaginaSolicitudes<SolicitudBandejaItem> | null>(null);
  readonly cargando = signal(false);
  readonly procesando = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);

  readonly totalPaginas = computed(() => {
    const pagina = this.pagina();

    return pagina ? Math.max(1, Math.ceil(pagina.total / pagina.tamano)) : 1;
  });

  /** Carga la bandeja con los filtros del revisor. */
  cargar(filtros: FiltrosBandeja): Observable<PaginaSolicitudes<SolicitudBandejaItem> | null> {
    this.cargando.set(true);
    this.error.set(null);

    return this.api.bandeja(filtros).pipe(
      tap({
        next: (pagina) => this.pagina.set(pagina),
        error: (error: unknown) => this.error.set(mensajeDeProblema(error))
      }),
      catchError(() => of(null)),
      finalize(() => this.cargando.set(false))
    );
  }

  /** Toma, aprueba o rechaza la solicitud y recarga la bandeja. */
  cambiarEstado(
    solicitudId: string,
    comando: ComandoEstadoSolicitud,
    filtros: FiltrosBandeja
  ): Observable<SolicitudDetalle | null> {
    this.procesando.set(solicitudId);
    this.error.set(null);
    this.aviso.set(null);

    return this.api.cambiarEstado(solicitudId, comando).pipe(
      tap({
        next: () => {
          this.aviso.set(AVISO_DECISION);
          this.cargar(filtros).subscribe();
        },
        error: (error: unknown) => this.error.set(mensajeDeProblema(error))
      }),
      catchError(() => of(null)),
      finalize(() => this.procesando.set(null))
    );
  }

  /** Busca una solicitud dentro de la página cargada. */
  buscar(solicitudId: string): SolicitudBandejaItem | null {
    return this.pagina()?.elementos.find((item) => item.id === solicitudId) ?? null;
  }

  /** Descarga el pre-informe y devuelve su URL temporal. */
  preinforme(solicitudId: string): Observable<string | null> {
    this.error.set(null);

    return this.api.preinforme(solicitudId).pipe(
      map((blob) => URL.createObjectURL(blob)),
      catchError((error: unknown) => {
        this.error.set(mensajeDeProblema(error));
        return of(null);
      })
    );
  }
}
