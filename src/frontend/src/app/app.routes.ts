import { Routes } from '@angular/router';

import { ROLES } from './core/auth/auth.models';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'inicio' },
  {
    path: 'login',
    title: 'Acceso',
    loadComponent: () => import('./features/auth/login.page').then((modulo) => modulo.LoginPage)
  },
  {
    path: 'inicio',
    title: 'Inicio',
    canActivate: [authGuard],
    loadComponent: () => import('./features/inicio/inicio.page').then((modulo) => modulo.InicioPage)
  },
  {
    path: 'administracion',
    title: 'Administración',
    canActivate: [authGuard, roleGuard],
    data: { roles: [ROLES.administrativo] },
    loadComponent: () =>
      import('./features/administracion/administracion.page').then((modulo) => modulo.AdministracionPage)
  },
  { path: '**', redirectTo: 'inicio' }
];
