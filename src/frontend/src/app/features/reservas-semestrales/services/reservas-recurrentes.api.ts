import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { nombreDia } from '../../administracion/configuracion/models/calendario.models';
import {
  nombreTipoRecurrencia,
  PaginaSeries,
  SerieDetalle,
  SolicitudSerie
} from '../models/serie.models';

/** Cliente de series semestrales de área completa. */
@Injectable({ providedIn: 'root' })
export class ReservasRecurrentesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api`;

  /** Radica la serie completa; la API la rechaza entera si una ocurrencia colisiona. */
  crear(solicitud: SolicitudSerie): Observable<SerieDetalle> {
    return this.http.post<SerieDetalle>(`${this.base}/reservas/recurrentes`, {
      espacioId: solicitud.espacioId,
      tipoRecurrencia: nombreTipoRecurrencia(solicitud.tipoRecurrencia),
      diaSemana: solicitud.diaSemana === null ? null : nombreDia(solicitud.diaSemana),
      fechasFijas: solicitud.fechasFijas,
      horaInicio: solicitud.horaInicio,
      horaFin: solicitud.horaFin,
      periodoInicio: solicitud.periodoInicio,
      periodoFin: solicitud.periodoFin,
      materia: solicitud.materia
    });
  }

  /** Series del docente autenticado, de la más reciente a la más antigua. */
  misSeries(pagina: number, tamano: number): Observable<PaginaSeries> {
    const params = new HttpParams()
      .set('pagina', String(pagina))
      .set('tamano', String(tamano));

    return this.http.get<PaginaSeries>(`${this.base}/reservas/recurrentes/me`, { params });
  }
}
