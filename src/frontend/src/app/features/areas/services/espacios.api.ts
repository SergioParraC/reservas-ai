import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import {
  EspacioCatalogo,
  EspacioDetalle,
  EspacioPublico,
  EspacioResumen,
  EstadoUnidad,
  FiltrosCatalogo,
  FiltrosEspaciosAdmin,
  nombreCategoria,
  nombreEstado,
  nombreTipoAprobacion,
  PaginaResultado,
  PlantillaQrDescarga,
  PuestoDetalle,
  SolicitudActualizarEspacio,
  SolicitudCrearEspacio,
  SolicitudPuesto
} from '../models/espacio.models';

/** Cliente tipado de las APIs de catálogo, mantenimiento de áreas y QR. */
@Injectable({ providedIn: 'root' })
export class EspaciosApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api`;

  /** Catálogo público con filtros combinables. */
  listarCatalogo(filtros: FiltrosCatalogo): Observable<PaginaResultado<EspacioCatalogo>> {
    let params = new HttpParams()
      .set('pagina', String(filtros.pagina))
      .set('tamano', String(filtros.tamano));

    if (filtros.q) {
      params = params.set('q', filtros.q);
    }

    if (filtros.categoria) {
      params = params.set('categoria', nombreCategoria(filtros.categoria));
    }

    if (filtros.aforoMinimo !== null && filtros.aforoMinimo !== undefined) {
      params = params.set('aforoMinimo', String(filtros.aforoMinimo));
    }

    if (filtros.aforoMaximo !== null && filtros.aforoMaximo !== undefined) {
      params = params.set('aforoMaximo', String(filtros.aforoMaximo));
    }

    return this.http.get<PaginaResultado<EspacioCatalogo>>(`${this.base}/catalogo/espacios`, { params });
  }

  /** Detalle público de un área activa. */
  obtenerPublico(id: string): Observable<EspacioPublico> {
    return this.http.get<EspacioPublico>(`${this.base}/catalogo/espacios/${id}`);
  }

  /** Listado administrativo, incluidas las áreas inactivas. */
  listarAdmin(filtros: FiltrosEspaciosAdmin): Observable<PaginaResultado<EspacioResumen>> {
    let params = new HttpParams()
      .set('pagina', String(filtros.pagina))
      .set('tamano', String(filtros.tamano));

    if (filtros.q) {
      params = params.set('q', filtros.q);
    }

    if (filtros.categoria) {
      params = params.set('categoria', nombreCategoria(filtros.categoria));
    }

    if (filtros.estado) {
      params = params.set('estado', nombreEstado(filtros.estado));
    }

    return this.http.get<PaginaResultado<EspacioResumen>>(`${this.base}/espacios`, { params });
  }

  /** Detalle administrativo con sus puestos. */
  obtener(id: string): Observable<EspacioDetalle> {
    return this.http.get<EspacioDetalle>(`${this.base}/espacios/${id}`);
  }

  /** Alta de un área con código QR generado por la API. */
  crear(solicitud: SolicitudCrearEspacio): Observable<EspacioDetalle> {
    return this.http.post<EspacioDetalle>(`${this.base}/espacios`, this.cuerpoCrear(solicitud));
  }

  /** Edición de un área; el código QR se conserva. */
  actualizar(id: string, solicitud: SolicitudActualizarEspacio): Observable<EspacioDetalle> {
    return this.http.put<EspacioDetalle>(`${this.base}/espacios/${id}`, {
      ...this.cuerpoCrear(solicitud),
      estado: nombreEstado(solicitud.estado),
      rowVersion: solicitud.rowVersion ?? null
    });
  }

  /** Baja lógica del área. */
  desactivar(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/espacios/${id}`);
  }

  /** Alta de un puesto en el área. */
  crearPuesto(espacioId: string, solicitud: SolicitudPuesto): Observable<PuestoDetalle> {
    return this.http.post<PuestoDetalle>(`${this.base}/espacios/${espacioId}/puestos`, {
      identificador: solicitud.identificador,
      descripcionEquipo: solicitud.descripcionEquipo ?? null
    });
  }

  /** Edición de un puesto; el código QR se conserva. */
  actualizarPuesto(espacioId: string, puestoId: string, solicitud: SolicitudPuesto): Observable<PuestoDetalle> {
    return this.http.put<PuestoDetalle>(`${this.base}/espacios/${espacioId}/puestos/${puestoId}`, {
      identificador: solicitud.identificador,
      descripcionEquipo: solicitud.descripcionEquipo ?? null,
      estado: nombreEstado(solicitud.estado ?? EstadoUnidad.Activo)
    });
  }

  /** Baja lógica del puesto. */
  desactivarPuesto(espacioId: string, puestoId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/espacios/${espacioId}/puestos/${puestoId}`);
  }

  /** Carga la fotografía con `multipart/form-data`. */
  subirImagen(espacioId: string, archivo: File): Observable<{ urlFotografia: string }> {
    const datos = new FormData();
    datos.append('imagen', archivo, archivo.name);

    return this.http.post<{ urlFotografia: string }>(`${this.base}/espacios/${espacioId}/imagen`, datos);
  }

  /** Elimina la fotografía del área. */
  eliminarImagen(espacioId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/espacios/${espacioId}/imagen`);
  }

  /** Descarga el SVG individual del área. */
  qrEspacio(espacioId: string): Observable<Blob> {
    return this.http.get(`${this.base}/espacios/${espacioId}/qr.svg`, { responseType: 'blob' });
  }

  /** Descarga el SVG individual del puesto. */
  qrPuesto(puestoId: string): Observable<Blob> {
    return this.http.get(`${this.base}/puestos/${puestoId}/qr.svg`, { responseType: 'blob' });
  }

  /** Compone la plantilla A4 y devuelve el SVG con el total de hojas. */
  plantilla(espacioIds: string[], incluirPuestos: boolean, hoja: number): Observable<PlantillaQrDescarga> {
    let params = new HttpParams()
      .set('incluirPuestos', String(incluirPuestos))
      .set('hoja', String(hoja));

    for (const id of espacioIds) {
      params = params.append('espacioIds', id);
    }

    return this.http
      .get(`${this.base}/espacios/qr/plantilla`, { params, observe: 'response', responseType: 'blob' })
      .pipe(
        map((respuesta: HttpResponse<Blob>) => ({
          blob: respuesta.body ?? new Blob([], { type: 'image/svg+xml' }),
          totalHojas: Number(respuesta.headers.get('X-Total-Hojas') ?? '1')
        }))
      );
  }

  private cuerpoCrear(solicitud: SolicitudCrearEspacio): Record<string, unknown> {
    return {
      nombre: solicitud.nombre,
      ubicacion: solicitud.ubicacion,
      categoria: nombreCategoria(solicitud.categoria),
      aforo: solicitud.aforo,
      tipoAprobacion: nombreTipoAprobacion(solicitud.tipoAprobacion)
    };
  }
}
