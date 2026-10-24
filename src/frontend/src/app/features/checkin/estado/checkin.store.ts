import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, Observable, of, tap } from 'rxjs';

import { environment } from '../../../../environments/environment';
import {
  avisoRemanenteLiberado,
  mensajeDeErrorCheckIn
} from '../../solicitudes-estudiante/mappers/reserva.mapper';
import {
  calcularVentanaCheckIn,
  ReservaAsistencia,
  VentanaCheckIn
} from '../models/checkin.models';
import { CheckinApi } from '../services/checkin.api';

/** Mensaje de confirmación del ingreso presencial. */
export const AVISO_INGRESO_REGISTRADO = 'Ingreso registrado. El puesto queda en uso.';

/** Mensaje cuando la reserva no aparece en la agenda del titular. */
export const ERROR_RESERVA_NO_ENCONTRADA =
  'La reserva no está en su agenda. Verifique el enlace o consulte "Mis reservas".';

/**
 * Estado del flujo presencial: reserva cargada, ventana admitida calculada con la tolerancia
 * configurada, ingreso con el QR del puesto y salida con el remanente liberado.
 */
@Injectable({ providedIn: 'root' })
export class CheckinStore {
  private readonly api = inject(CheckinApi);

  readonly reserva = signal<ReservaAsistencia | null>(null);
  readonly cargando = signal(false);
  readonly registrando = signal(false);
  readonly error = signal<string | null>(null);
  readonly aviso = signal<string | null>(null);
  readonly remanente = signal<string | null>(null);

  /** Estado vigente de la reserva o `null` mientras carga. */
  readonly estado = computed(() => this.reserva()?.estado ?? null);

  /** Ventana admitida `inicio ± tolerancia` para mostrar junto a la franja. */
  readonly ventana = computed<VentanaCheckIn | null>(() => {
    const reserva = this.reserva();

    return reserva
      ? calcularVentanaCheckIn(reserva.inicio, environment.checkIn.ventanaToleranciaMinutos)
      : null;
  });

  /** Carga la reserva del titular; un fallo deja el mensaje traducido del problema. */
  cargar(reservaId: string): Observable<ReservaAsistencia | null> {
    this.cargando.set(true);
    this.error.set(null);
    this.aviso.set(null);
    this.remanente.set(null);

    return this.api.reserva(reservaId).pipe(
      tap({
        next: (reserva) => this.reserva.set(reserva),
        error: (error: unknown) => this.error.set(mensajeDeErrorCheckIn(error))
      }),
      catchError(() => of(null)),
      finalize(() => this.cargando.set(false))
    );
  }

  /** Registra el ingreso con el código escaneado o transcrito. */
  registrarIngreso(reservaId: string, codigoQr: string): Observable<boolean> {
    this.registrando.set(true);
    this.error.set(null);
    this.aviso.set(null);
    this.remanente.set(null);

    return this.api.registrarCheckIn(reservaId, codigoQr).pipe(
      map((reserva) => {
        this.reserva.set(reserva);
        this.aviso.set(AVISO_INGRESO_REGISTRADO);

        return true;
      }),
      catchError((error: unknown) => {
        this.error.set(mensajeDeErrorCheckIn(error));
        return of(false);
      }),
      finalize(() => this.registrando.set(false))
    );
  }

  /** Registra la salida y publica el remanente que vuelve a estar disponible. */
  registrarSalida(reservaId: string): Observable<boolean> {
    this.registrando.set(true);
    this.error.set(null);
    this.aviso.set(null);
    this.remanente.set(null);

    return this.api.registrarCheckOut(reservaId).pipe(
      map((reserva) => {
        this.reserva.set(reserva);
        this.remanente.set(
          avisoRemanenteLiberado(reserva.inicio, reserva.fin, reserva.finPlanificado)
        );

        return true;
      }),
      catchError((error: unknown) => {
        this.error.set(mensajeDeErrorCheckIn(error));
        return of(false);
      }),
      finalize(() => this.registrando.set(false))
    );
  }

  /** Limpia los mensajes antes de un nuevo intento. */
  limpiarMensajes(): void {
    this.error.set(null);
    this.aviso.set(null);
    this.remanente.set(null);
  }
}
