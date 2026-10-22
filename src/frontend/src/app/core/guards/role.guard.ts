import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { SesionStore } from '../auth/sesion.store';

/**
 * Exige que la sesión tenga al menos uno de los roles declarados en `data.roles`.
 * Si no los tiene, redirige a la página de inicio sin renderizar el contenido.
 */
export const roleGuard: CanActivateFn = (ruta) => {
  const sesion = inject(SesionStore);
  const router = inject(Router);

  const requeridos = (ruta.data['roles'] as string[] | undefined) ?? [];

  if (requeridos.length === 0 || requeridos.some((rol) => sesion.tieneRol(rol))) {
    return true;
  }

  return router.createUrlTree(['/inicio'], { queryParams: { motivo: 'sin-permisos' } });
};
