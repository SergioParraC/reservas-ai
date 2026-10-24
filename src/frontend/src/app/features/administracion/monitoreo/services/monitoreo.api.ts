import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../../environments/environment';
import { OcupacionActual } from '../models/monitoreo.models';

/** Cliente del mapa de ocupación institucional (solo lectura, rol Administrativo). */
@Injectable({ providedIn: 'root' })
export class MonitoreoApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api`;

  /** Foto instantánea de la ocupación por área y en totales. */
  ocupacionActual(): Observable<OcupacionActual> {
    return this.http.get<OcupacionActual>(`${this.base}/monitoreo/ocupacion-actual`);
  }
}
