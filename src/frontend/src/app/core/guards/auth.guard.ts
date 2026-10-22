import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { SesionStore } from '../auth/sesion.store';

/** Permite el acceso solo con una sesión vigente; si no, redirige al login. */
export const authGuard: CanActivateFn = (_ruta, estado) => {
  const sesion = inject(SesionStore);
  const router = inject(Router);

  if (sesion.estaAutenticado()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: estado.url } });
};
