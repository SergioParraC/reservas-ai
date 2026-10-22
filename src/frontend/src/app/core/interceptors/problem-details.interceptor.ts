import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { SesionStore } from '../auth/sesion.store';
import { NotificadorErrores } from '../errores/notificador-errores';

/** Problema RFC 7807 tal como lo devuelve la API. */
export interface ProblemaRfc7807 {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

const MENSAJE_GENERICO = 'Ocurrió un error inesperado. Intente nuevamente.';

/**
 * Traduce el contrato RFC 7807: redirige al acceso ante una sesión expirada,
 * muestra el `detail` de los rechazos de permisos y usa un mensaje controlado
 * cuando la respuesta de error no cumple el formato.
 */
export const problemDetailsInterceptor: HttpInterceptorFn = (solicitud, siguiente) => {
  const sesion = inject(SesionStore);
  const notificador = inject(NotificadorErrores);
  const router = inject(Router);

  return siguiente(solicitud).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        notificador.error(MENSAJE_GENERICO);
        return throwError(() => error);
      }

      const problema = leerProblema(error);
      const tipo = problema?.type ?? '';

      if (tipo.endsWith('/sesion-expirada') || tipo.endsWith('/sesion-revocada')) {
        sesion.limpiar();
        notificador.marcarSesionExpirada();
        void router.navigate(['/login'], { queryParams: { motivo: 'sesion-expirada' } });
      } else if (error.status === 403) {
        notificador.error(problema?.detail ?? 'No tiene permisos para realizar esta operación.');
      } else if (problema?.detail) {
        notificador.error(problema.detail);
      } else if (error.status === 401) {
        notificador.error('Las credenciales proporcionadas no son válidas.');
      } else {
        notificador.error(MENSAJE_GENERICO);
      }

      return throwError(() => error);
    })
  );
};

/** Extrae el problema RFC 7807 del cuerpo o devuelve `null` si no cumple el formato. */
export function leerProblema(error: HttpErrorResponse): ProblemaRfc7807 | null {
  const cuerpo: unknown = error.error;

  if (!cuerpo || typeof cuerpo !== 'object') {
    return null;
  }

  const problema = cuerpo as ProblemaRfc7807;

  return typeof problema.type === 'string' || typeof problema.title === 'string' ? problema : null;
}
