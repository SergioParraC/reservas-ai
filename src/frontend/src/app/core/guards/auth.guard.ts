import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { SesionStore } from '../auth/sesion.store';

/**
 * Permite el acceso con una sesión vigente de cualquiera de los dos orígenes
 * (`local` o `entra`); si no hay sesión, redirige al login conservando el destino.
 */
export const authGuard: CanActivateFn = (_ruta, estado) => {
  const sesion = inject(SesionStore);
  const router = inject(Router);

  if (sesion.estaAutenticado()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: estado.url } });
};
