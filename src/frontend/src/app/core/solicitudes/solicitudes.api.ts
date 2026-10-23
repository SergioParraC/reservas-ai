import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  ComandoEstadoSolicitud,
  FiltrosBandeja,
  FiltrosSolicitudes,
  nombreEstadoSolicitud,
  PaginaSolicitudes,
  SolicitudBandejaItem,
  SolicitudDetalle,
  SolicitudRadicacion,
  SolicitudRadicada,
  SolicitudResumen
} from './solicitudes.models';

/** Cliente de radicación, historial propio, bandeja y decisiones de solicitudes. */
@Injectable({ providedIn: 'root' })
export class SolicitudesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api`;

  /** Radica la solicitud con el pre-informe como `multipart/form-data`. */
  radicar(solicitud: SolicitudRadicacion): Observable<SolicitudRadicada> {
    const datos = new FormData();
    datos.append('espacioId', solicitud.espacioId);
    datos.append('inicio', solicitud.inicio);
    datos.append('fin', solicitud.fin);
    datos.append('materia', solicitud.materia);
    datos.append('docenteResponsable', solicitud.docenteResponsable);
    datos.append('justificacion', solicitud.justificacion);
    datos.append('archivo', solicitud.archivo, solicitud.archivo.name);

    return this.http.post<SolicitudRadicada>(`${this.base}/solicitudes`, datos);
  }

  /** Historial del solicitante autenticado. */
  misSolicitudes(filtros: FiltrosSolicitudes): Observable<PaginaSolicitudes<SolicitudResumen>> {
    let params = new HttpParams()
      .set('pagina', String(filtros.pagina))
      .set('tamano', String(filtros.tamano));

    if (filtros.estado) {
      params = params.set('estado', nombreEstadoSolicitud(filtros.estado));
    }

    return this.http.get<PaginaSolicitudes<SolicitudResumen>>(`${this.base}/solicitudes/me`, { params });
  }

  /** Descarga autenticada del pre-informe. */
  preinforme(solicitudId: string): Observable<Blob> {
    return this.http.get(`${this.base}/solicitudes/${solicitudId}/preinforme`, { responseType: 'blob' });
  }

  /** Bandeja filtrada por la competencia del revisor. */
  bandeja(filtros: FiltrosBandeja): Observable<PaginaSolicitudes<SolicitudBandejaItem>> {
    let params = new HttpParams()
      .set('pagina', String(filtros.pagina))
      .set('tamano', String(filtros.tamano));

    if (filtros.estado) {
      params = params.set('estado', nombreEstadoSolicitud(filtros.estado));
    }

    if (filtros.espacioId) {
      params = params.set('espacioId', filtros.espacioId);
    }

    if (filtros.desde) {
      params = params.set('desde', filtros.desde);
    }

    if (filtros.hasta) {
      params = params.set('hasta', filtros.hasta);
    }

    return this.http.get<PaginaSolicitudes<SolicitudBandejaItem>>(`${this.base}/bandeja-aprobaciones`, { params });
  }

  /** Cambia el estado de una solicitud de la competencia del revisor. */
  cambiarEstado(solicitudId: string, comando: ComandoEstadoSolicitud): Observable<SolicitudDetalle> {
    return this.http.put<SolicitudDetalle>(`${this.base}/solicitudes/${solicitudId}/estado`, {
      estado: nombreEstadoSolicitud(comando.estado),
      motivo: comando.motivo ?? null
    });
  }
}
