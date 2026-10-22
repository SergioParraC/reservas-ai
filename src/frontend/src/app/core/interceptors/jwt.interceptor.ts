import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { environment } from '../../../environments/environment';
import { SesionStore } from '../auth/sesion.store';

/**
 * Adjunta `Authorization: Bearer` únicamente a las solicitudes dirigidas a la API
 * cuando existe una sesión con token vigente.
 */
export const jwtInterceptor: HttpInterceptorFn = (solicitud, siguiente) => {
  const sesion = inject(SesionStore);
  const token = sesion.token();

  if (!token || !esSolicitudApi(solicitud.url)) {
    return siguiente(solicitud);
  }

  return siguiente(
    solicitud.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    })
  );
};

/** Indica si la URL pertenece a la API institucional. */
export function esSolicitudApi(url: string): boolean {
  if (url.startsWith('/api')) {
    return true;
  }

  const base = environment.apiBaseUrl;

  return base.length > 0 && url.startsWith(`${base}/api`);
}
