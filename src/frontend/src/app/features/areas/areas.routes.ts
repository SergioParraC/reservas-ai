import { Routes } from '@angular/router';

import { ROLES } from '../../core/auth/auth.models';
import { authGuard } from '../../core/guards/auth.guard';
import { roleGuard } from '../../core/guards/role.guard';

/** Rutas públicas del catálogo, sin guards. */
export const CATALOGO_ROUTES: Routes = [
  {
    path: '',
    title: 'Catálogo de espacios',
    loadComponent: () => import('./pages/catalogo.page').then((modulo) => modulo.CatalogoPage)
  },
  {
    path: ':id',
    title: 'Detalle del espacio',
    loadComponent: () =>
      import('./pages/espacio-detalle.page').then((modulo) => modulo.EspacioDetallePage)
  }
];

const GUARDAS_ADMIN = [authGuard, roleGuard];
const DATOS_ADMIN = { roles: [ROLES.administrativo] };

/** Rutas administrativas de áreas y puestos. */
export const ESPACIOS_ADMIN_ROUTES: Routes = [
  {
    path: '',
    title: 'Mantenimiento de espacios',
    canActivate: GUARDAS_ADMIN,
    data: DATOS_ADMIN,
    loadComponent: () =>
      import('./pages/admin/espacios-admin.page').then((modulo) => modulo.EspaciosAdminPage)
  },
  {
    path: 'nuevo',
    title: 'Nueva área',
    canActivate: GUARDAS_ADMIN,
    data: DATOS_ADMIN,
    loadComponent: () =>
      import('./pages/admin/espacio-form.page').then((modulo) => modulo.EspacioFormPage)
  },
  {
    path: ':id/editar',
    title: 'Editar área',
    canActivate: GUARDAS_ADMIN,
    data: DATOS_ADMIN,
    loadComponent: () =>
      import('./pages/admin/espacio-form.page').then((modulo) => modulo.EspacioFormPage)
  },
  {
    path: ':id/puestos',
    title: 'Puestos del área',
    canActivate: GUARDAS_ADMIN,
    data: DATOS_ADMIN,
    loadComponent: () =>
      import('./pages/admin/puestos.page').then((modulo) => modulo.PuestosPage)
  }
];
