import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { PaginaReservas } from '../../solicitudes-estudiante/models/reserva.models';
import { ReservaAsistencia, SolicitudCheckIn } from '../models/checkin.models';

/** Cliente tipado del check-in y del check-out presencial. */
@Injectable({ providedIn: 'root' })
export class CheckinApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api`;

  /**
   * Reserva del titular por identificador. La API expone la agenda paginada
   * (`GET /api/reservas/me`), así que se localiza la reserva dentro de la primera página.
   */
  reserva(reservaId: string): Observable<ReservaAsistencia | null> {
    return this.http
      .get<PaginaReservas>(`${this.base}/reservas/me`, {
        params: { pagina: '1', tamano: '100' }
      })
      .pipe(map((pagina) => pagina.elementos.find((reserva) => reserva.id === reservaId) ?? null));
  }

  /** Confirma la presencia con el QR impreso en la placa del puesto reservado. */
  registrarCheckIn(reservaId: string, codigoQr: string): Observable<ReservaAsistencia> {
    const cuerpo: SolicitudCheckIn = { codigoQr };

    return this.http.post<ReservaAsistencia>(`${this.base}/reservas/${reservaId}/check-in`, cuerpo);
  }

  /** Registra la salida desde `EnUso`; no lleva cuerpo ni ventana horaria. */
  registrarCheckOut(reservaId: string): Observable<ReservaAsistencia> {
    return this.http.post<ReservaAsistencia>(`${this.base}/reservas/${reservaId}/check-out`, null);
  }
}


