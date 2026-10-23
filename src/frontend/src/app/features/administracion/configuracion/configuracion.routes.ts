import { Routes } from '@angular/router';

import { ROLES } from '../../../core/auth/auth.models';
import { authGuard } from '../../../core/guards/auth.guard';
import { roleGuard } from '../../../core/guards/role.guard';

const GUARDAS_ADMIN = [authGuard, roleGuard];
const DATOS_ADMIN = { roles: [ROLES.administrativo] };

/** Rutas administrativas de periodos académicos y franjas no operativas. */
export const CONFIGURACION_ROUTES: Routes = [
  {
    path: '',
    title: 'Calendario institucional',
    canActivate: GUARDAS_ADMIN,
    data: DATOS_ADMIN,
    loadComponent: () =>
      import('./pages/configuracion.page').then((modulo) => modulo.ConfiguracionPage)
  },
  {
    path: 'periodos/nuevo',
    title: 'Nuevo periodo',
    canActivate: GUARDAS_ADMIN,
    data: DATOS_ADMIN,
    loadComponent: () =>
      import('./pages/periodo-form.page').then((modulo) => modulo.PeriodoFormPage)
  },
  {
    path: 'periodos/:id/editar',
    title: 'Editar periodo',
    canActivate: GUARDAS_ADMIN,
    data: DATOS_ADMIN,
    loadComponent: () =>
      import('./pages/periodo-form.page').then((modulo) => modulo.PeriodoFormPage)
  },
  {
    path: 'franjas/nueva',
    title: 'Nueva franja no operativa',
    canActivate: GUARDAS_ADMIN,
    data: DATOS_ADMIN,
    loadComponent: () =>
      import('./pages/franja-form.page').then((modulo) => modulo.FranjaFormPage)
  },
  {
    path: 'franjas/:id/editar',
    title: 'Editar franja no operativa',
    canActivate: GUARDAS_ADMIN,
    data: DATOS_ADMIN,
    loadComponent: () =>
      import('./pages/franja-form.page').then((modulo) => modulo.FranjaFormPage)
  }
];
