import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import {
  DisponibilidadPuesto,
  EstadoReserva,
  FiltrosDisponibilidad,
  FiltrosMisReservas,
  PaginaReservas,
  ReservaDetalle,
  SolicitudReserva
} from '../models/reserva.models';

/** Cliente tipado de disponibilidad y reservas propias. */
@Injectable({ providedIn: 'root' })
export class ReservasApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api`;

  /** Franjas del puesto con su disponibilidad para una fecha y duración. */
  disponibilidad(puestoId: string, filtros: FiltrosDisponibilidad): Observable<DisponibilidadPuesto> {
    let params = new HttpParams().set('fecha', filtros.fecha);

    if (filtros.duracionMinutos) {
      params = params.set('duracionMinutos', String(filtros.duracionMinutos));
    }

    return this.http.get<DisponibilidadPuesto>(`${this.base}/puestos/${puestoId}/disponibilidad`, { params });
  }

  /** Alta autoconfirmada e inmediata de una reserva. */
  crear(solicitud: SolicitudReserva): Observable<ReservaDetalle> {
    return this.http.post<ReservaDetalle>(`${this.base}/reservas`, {
      puestoId: solicitud.puestoId,
      inicio: solicitud.inicio,
      fin: solicitud.fin
    });
  }

  /** Agenda propia paginada, ordenada por inicio ascendente. */
  misReservas(filtros: FiltrosMisReservas): Observable<PaginaReservas> {
    let params = new HttpParams()
      .set('pagina', String(filtros.pagina))
      .set('tamano', String(filtros.tamano));

    if (filtros.estado !== null && filtros.estado !== undefined) {
      params = params.set('estado', EstadoReserva[filtros.estado] ?? 'Confirmada');
    }

    if (filtros.desde) {
      params = params.set('desde', filtros.desde);
    }

    if (filtros.hasta) {
      params = params.set('hasta', filtros.hasta);
    }

    return this.http.get<PaginaReservas>(`${this.base}/reservas/me`, { params });
  }

  /** Cancelación de una reserva propia dentro del margen permitido. */
  cancelar(reservaId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/reservas/${reservaId}`);
  }
}
