import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, tap } from 'rxjs';

import { mensajeDeProblema } from '../../../../core/errores/mensajes-problema';
import {
  CalendarioVigente,
  FranjaNoOperativa,
  PeriodoAcademico,
  proximaSemanaNoOperativa,
  ProximoBloqueo,
  SolicitudActualizarPeriodo,
  SolicitudFranja,
  SolicitudPeriodo
} from '../models/calendario.models';
import { CalendarioApi } from '../services/calendario.api';

/** Aviso de activación de un periodo. */
export const AVISO_PERIODO_ACTIVADO = 'Periodo activado. Las reservas ya se validan contra su ventana.';

/** Aviso de creación o edición de una franja. */
export const AVISO_FRANJA_GUARDADA = 'Franja no operativa guardada. Se aplica desde la próxima reserva.';

/**
 * Estado del calendario institucional: periodos, franjas, calendario vigente y mensajes
 * derivados del contrato RFC 7807. Cada mutación invalida la caché local recargando las listas.
 */
@Injectable({ providedIn: 'root' })
export class CalendarioStore {
  private readonly api = inject(CalendarioApi);

  readonly periodos = signal<PeriodoAcademico[]>([]);
  readonly franjas = signal<FranjaNoOperativa[]>([]);
  readonly vigente = signal<CalendarioVigente | null>(null);
  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);

  /** Periodo vigente o `null` si no hay ninguno activo. */
  readonly periodoActivo = computed<PeriodoAcademico | null>(() => this.vigente()?.periodoActivo ?? null);

  /** Próximo día cerrado dentro de la semana, para anticipar restricciones. */
  readonly proximaSemana = computed<ProximoBloqueo | null>(() =>
    proximaSemanaNoOperativa(this.vigente())
  );

  /** Franjas que cierran el día completo. */
  readonly diasCerrados = computed(() => this.franjas().filter((franja) => franja.activa));

  /** Carga las listas administrativas y el calendario vigente en paralelo. */
  cargar(): Observable<boolean> {
    this.cargando.set(true);
    this.error.set(null);

    return this.api.listarPeriodos().pipe(
      tap({
        next: (periodos) => this.periodos.set(periodos),
        error: (error: unknown) => this.error.set(mensajeDeProblema(error))
      }),
      map(() => true),
      catchError(() => of(false)),
      finalize(() => {
        this.cargarFranjas().subscribe();
        this.cargarVigente().subscribe();
        this.cargando.set(false);
      })
    );
  }

  /** Carga solo las franjas no operativas. */
  cargarFranjas(): Observable<FranjaNoOperativa[]> {
    return this.api.listarFranjas().pipe(
      tap({
        next: (franjas) => this.franjas.set(franjas),
        error: (error: unknown) => this.error.set(mensajeDeProblema(error))
      }),
      catchError(() => of([] as FranjaNoOperativa[]))
    );
  }

  /** Carga el calendario vigente que consumen las pantallas de reserva. */
  cargarVigente(): Observable<CalendarioVigente | null> {
    return this.api.vigente().pipe(
      tap({
        next: (vigente) => this.vigente.set(vigente),
        error: (error: unknown) => this.error.set(mensajeDeProblema(error))
      }),
      catchError(() => of(null))
    );
  }

  /** Registra un periodo inactivo y recarga el listado. */
  crearPeriodo(solicitud: SolicitudPeriodo): Observable<PeriodoAcademico | null> {
    return this.mutar(this.api.crearPeriodo(solicitud), 'Periodo registrado. Actívelo para habilitar reservas.');
  }

  /** Edita un periodo validando solapes y ocupación futura. */
  actualizarPeriodo(
    id: string,
    solicitud: SolicitudActualizarPeriodo
  ): Observable<PeriodoAcademico | null> {
    return this.mutar(this.api.actualizarPeriodo(id, solicitud), 'Periodo actualizado.');
  }

  /** Activa el periodo y desactiva al vigente. */
  activarPeriodo(id: string): Observable<PeriodoAcademico | null> {
    return this.mutar(this.api.activarPeriodo(id), AVISO_PERIODO_ACTIVADO);
  }

  /** Registra una franja no operativa rechazando duplicados en la API. */
  crearFranja(solicitud: SolicitudFranja): Observable<FranjaNoOperativa | null> {
    return this.mutar(this.api.crearFranja(solicitud), AVISO_FRANJA_GUARDADA);
  }

  /** Edita una franja conservando su vigencia. */
  actualizarFranja(id: string, solicitud: SolicitudFranja): Observable<FranjaNoOperativa | null> {
    return this.mutar(this.api.actualizarFranja(id, solicitud), AVISO_FRANJA_GUARDADA);
  }

  /** Da de baja lógica una franja. */
  eliminarFranja(id: string): Observable<boolean> {
    this.guardando.set(true);
    this.error.set(null);

    return this.api.eliminarFranja(id).pipe(
      tap(() => {
        this.aviso.set('Franja eliminada. Deja de aplicar a las reservas siguientes.');
        this.cargarFranjas().subscribe();
        this.cargarVigente().subscribe();
      }),
      map(() => true),
      catchError((error: unknown) => {
        this.error.set(mensajeDeProblema(error));
        return of(false);
      }),
      finalize(() => this.guardando.set(false))
    );
  }

  /** Limpia los mensajes vigentes. */
  limpiarMensajes(): void {
    this.error.set(null);
    this.aviso.set(null);
  }

  private mutar<T>(
    peticion: Observable<T>,
    aviso: string
  ): Observable<T | null> {
    this.guardando.set(true);
    this.error.set(null);
    this.aviso.set(null);

    return peticion.pipe(
      tap({
        next: () => {
          this.aviso.set(aviso);
          this.cargar().subscribe();
        },
        error: (error: unknown) => this.error.set(mensajeDeProblema(error))
      }),
      catchError(() => of(null)),
      finalize(() => this.guardando.set(false))
    );
  }
}
