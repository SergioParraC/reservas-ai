import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, tap } from 'rxjs';

import { mensajeDeErrorReserva } from '../mappers/reserva.mapper';
import {
  DisponibilidadEspacio,
  DisponibilidadPuesto,
  FiltrosDisponibilidad,
  FiltrosMisReservas,
  PaginaReservas,
  ReservaDetalle,
  SolicitudReserva
} from '../models/reserva.models';
import { ReservasApi } from '../services/reservas.api';

/** Mensaje de confirmación del alta de una reserva. */
export const AVISO_RESERVA_CONFIRMADA = 'Reserva confirmada. Consulte su agenda en "Mis reservas".';

/** Mensaje de confirmación de la cancelación. */
export const AVISO_RESERVA_CANCELADA = 'Reserva cancelada. La franja vuelve a estar disponible.';

/**
 * Estado del préstamo directo: disponibilidad del puesto, agenda propia, cancelación
 * y mensajes derivados del contrato RFC 7807.
 */
@Injectable({ providedIn: 'root' })
export class ReservasStore {
  private readonly api = inject(ReservasApi);

  readonly disponibilidad = signal<DisponibilidadPuesto | null>(null);
  readonly disponibilidadEspacio = signal<DisponibilidadEspacio | null>(null);
  readonly reservas = signal<PaginaReservas | null>(null);
  readonly cargando = signal(false);
  readonly cargandoReservas = signal(false);
  readonly cancelando = signal<string | null>(null);
  readonly cargandoEspacio = signal(false);
  readonly errorEspacio = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);

  readonly totalPaginas = computed(() => {
    const pagina = this.reservas();

    return pagina ? Math.max(1, Math.ceil(pagina.total / pagina.tamano)) : 1;
  });

  readonly reservasActivas = computed(
    () => this.reservas()?.elementos.filter((reserva) => reserva.cancelable).length ?? 0
  );

  /** Carga las franjas del puesto; ante error deja el mensaje y limpia la disponibilidad. */
  cargarDisponibilidad(
    puestoId: string,
    filtros: FiltrosDisponibilidad
  ): Observable<DisponibilidadPuesto | null> {
    this.cargando.set(true);
    this.error.set(null);
    this.disponibilidad.set(null);

    return this.api.disponibilidad(puestoId, filtros).pipe(
      tap({
        next: (disponibilidad) => this.disponibilidad.set(disponibilidad),
        error: (error: unknown) => this.error.set(mensajeDeErrorReserva(error))
      }),
      catchError(() => of(null)),
      finalize(() => this.cargando.set(false))
    );
  }

  /** Carga las franjas del área completa; ante error deja el mensaje y limpia la disponibilidad. */
  cargarDisponibilidadEspacio(
    espacioId: string,
    filtros: FiltrosDisponibilidad
  ): Observable<DisponibilidadEspacio | null> {
    this.cargandoEspacio.set(true);
    this.errorEspacio.set(null);
    this.disponibilidadEspacio.set(null);

    return this.api.disponibilidadEspacio(espacioId, filtros).pipe(
      tap({
        next: (disponibilidad) => this.disponibilidadEspacio.set(disponibilidad),
        error: (error: unknown) => this.errorEspacio.set(mensajeDeErrorReserva(error))
      }),
      catchError(() => of(null)),
      finalize(() => this.cargandoEspacio.set(false))
    );
  }

  /** Confirma la reserva de una franja y publica el aviso de éxito. */
  reservar(solicitud: SolicitudReserva): Observable<ReservaDetalle | null> {
    this.error.set(null);
    this.aviso.set(null);

    return this.api.crear(solicitud).pipe(
      tap({
        next: () => this.aviso.set(AVISO_RESERVA_CONFIRMADA),
        error: (error: unknown) => this.error.set(mensajeDeErrorReserva(error))
      }),
      catchError(() => of(null))
    );
  }

  /** Carga la agenda del titular. */
  cargarMisReservas(filtros: FiltrosMisReservas): Observable<PaginaReservas | null> {
    this.cargandoReservas.set(true);
    this.error.set(null);

    return this.api.misReservas(filtros).pipe(
      tap({
        next: (pagina) => this.reservas.set(pagina),
        error: (error: unknown) => this.error.set(mensajeDeErrorReserva(error))
      }),
      catchError(() => of(null)),
      finalize(() => this.cargandoReservas.set(false))
    );
  }

  /** Cancela una reserva propia; devuelve `true` cuando la API responde 204. */
  cancelar(reservaId: string): Observable<boolean> {
    this.cancelando.set(reservaId);
    this.error.set(null);
    this.aviso.set(null);

    return this.api.cancelar(reservaId).pipe(
      map(() => true),
      tap({
        next: () => this.aviso.set(AVISO_RESERVA_CANCELADA),
        error: (error: unknown) => this.error.set(mensajeDeErrorReserva(error))
      }),
      catchError(() => of(false)),
      finalize(() => this.cancelando.set(null))
    );
  }

  /** Limpia el mensaje de error y el aviso vigente. */
  limpiarMensajes(): void {
    this.error.set(null);
    this.aviso.set(null);
  }
}
