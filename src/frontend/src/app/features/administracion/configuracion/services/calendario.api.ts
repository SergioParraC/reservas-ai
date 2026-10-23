import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../../environments/environment';
import {
  CalendarioVigente,
  FranjaNoOperativa,
  PeriodoAcademico,
  SolicitudActualizarPeriodo,
  SolicitudFranja,
  SolicitudPeriodo
} from '../models/calendario.models';

/** Cliente del calendario institucional: periodos, franjas y consulta vigente. */
@Injectable({ providedIn: 'root' })
export class CalendarioApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api`;

  /** Periodos registrados, activos e inactivos. */
  listarPeriodos(): Observable<PeriodoAcademico[]> {
    return this.http.get<PeriodoAcademico[]>(`${this.base}/periodos-academicos`);
  }

  /** Alta de un periodo inactivo. */
  crearPeriodo(solicitud: SolicitudPeriodo): Observable<PeriodoAcademico> {
    return this.http.post<PeriodoAcademico>(`${this.base}/periodos-academicos`, solicitud);
  }

  /** Edición del nombre y el rango del periodo. */
  actualizarPeriodo(id: string, solicitud: SolicitudActualizarPeriodo): Observable<PeriodoAcademico> {
    return this.http.put<PeriodoAcademico>(`${this.base}/periodos-academicos/${id}`, {
      nombre: solicitud.nombre,
      fechaInicio: solicitud.fechaInicio,
      fechaFin: solicitud.fechaFin,
      rowVersion: solicitud.rowVersion ?? null
    });
  }

  /** Activación transaccional que desactiva al periodo vigente. */
  activarPeriodo(id: string): Observable<PeriodoAcademico> {
    return this.http.put<PeriodoAcademico>(`${this.base}/periodos-academicos/${id}/activar`, null);
  }

  /** Franjas no operativas vigentes. */
  listarFranjas(): Observable<FranjaNoOperativa[]> {
    return this.http.get<FranjaNoOperativa[]>(`${this.base}/franjas-no-operativas`);
  }

  /** Alta de una franja global. */
  crearFranja(solicitud: SolicitudFranja): Observable<FranjaNoOperativa> {
    return this.http.post<FranjaNoOperativa>(`${this.base}/franjas-no-operativas`, {
      diaSemana: solicitud.diaSemana ?? null,
      fechaEspecifica: solicitud.fechaEspecifica ?? null,
      horaInicio: solicitud.horaInicio,
      horaFin: solicitud.horaFin,
      esFeriado: solicitud.esFeriado,
      motivo: solicitud.motivo
    });
  }

  /** Edición de una franja global. */
  actualizarFranja(id: string, solicitud: SolicitudFranja): Observable<FranjaNoOperativa> {
    return this.http.put<FranjaNoOperativa>(`${this.base}/franjas-no-operativas/${id}`, {
      diaSemana: solicitud.diaSemana ?? null,
      fechaEspecifica: solicitud.fechaEspecifica ?? null,
      horaInicio: solicitud.horaInicio,
      horaFin: solicitud.horaFin,
      esFeriado: solicitud.esFeriado,
      motivo: solicitud.motivo,
      rowVersion: solicitud.rowVersion ?? null
    });
  }

  /** Baja lógica de la franja. */
  eliminarFranja(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/franjas-no-operativas/${id}`);
  }

  /** Calendario vigente para cualquier usuario autenticado. */
  vigente(): Observable<CalendarioVigente> {
    return this.http.get<CalendarioVigente>(`${this.base}/calendario/vigente`);
  }
}
