import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  CambioRolRespuesta,
  EstadoVinculacion,
  FiltroUsuarios,
  PaginaUsuarios,
  RegistroUsuarioRespuesta,
  RegistroUsuarioSolicitud
} from './usuarios.models';

/** Cliente tipado de los endpoints de registro, vinculación y administración de cuentas. */
@Injectable({ providedIn: 'root' })
export class UsuariosApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/usuarios`;

  /** Autorregistro público. */
  registrar(solicitud: RegistroUsuarioSolicitud): Observable<RegistroUsuarioRespuesta> {
    return this.http.post<RegistroUsuarioRespuesta>(`${this.base}/registro`, solicitud);
  }

  /** Vincula la cuenta autenticada con Entra ID. */
  vincular(tokenEntra: string): Observable<EstadoVinculacion> {
    return this.http.post<EstadoVinculacion>(`${this.base}/me/vinculacion-entra`, { tokenEntra });
  }

  /** Retira el vínculo federado de la cuenta autenticada. */
  desvincular(): Observable<void> {
    return this.http.delete<void>(`${this.base}/me/vinculacion-entra`);
  }

  /** Listado administrativo paginado y filtrable. */
  listar(filtro: FiltroUsuarios): Observable<PaginaUsuarios> {
    let params = new HttpParams()
      .set('pagina', String(filtro.pagina))
      .set('tamano', String(filtro.tamano));

    if (filtro.q) {
      params = params.set('q', filtro.q);
    }

    if (filtro.rol) {
      params = params.set('rol', filtro.rol);
    }

    if (filtro.vinculado !== null && filtro.vinculado !== undefined) {
      params = params.set('vinculado', String(filtro.vinculado));
    }

    return this.http.get<PaginaUsuarios>(this.base, { params });
  }

  /** Cambia el rol vigente de una cuenta. */
  cambiarRol(id: string, rol: string): Observable<CambioRolRespuesta> {
    return this.http.put<CambioRolRespuesta>(`${this.base}/${id}/rol`, { rol });
  }
}
