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
    path: 'registro',
    title: 'Crear cuenta',
    loadComponent: () => import('./features/auth/registro.page').then((modulo) => modulo.RegistroPage)
  },
  {
    path: 'auth/entra',
    title: 'Acceso institucional',
    loadComponent: () =>
      import('./features/auth/entra-callback.page').then((modulo) => modulo.EntraCallbackPage)
  },
  {
    path: 'catalogo',
    loadChildren: () => import('./features/areas/areas.routes').then((modulo) => modulo.CATALOGO_ROUTES)
  },
  {
    path: 'inicio',
    title: 'Inicio',
    canActivate: [authGuard],
    loadComponent: () => import('./features/inicio/inicio.page').then((modulo) => modulo.InicioPage)
  },
  {
    path: 'perfil',
    title: 'Mi cuenta',
    canActivate: [authGuard],
    loadComponent: () => import('./features/perfil/perfil.page').then((modulo) => modulo.PerfilPage)
  },
  {
    path: 'mis-reservas',
    title: 'Mis reservas',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/solicitudes-estudiante/pages/mis-reservas.page').then(
        (modulo) => modulo.MisReservasPage
      )
  },
  {
    path: 'solicitudes',
    title: 'Solicitudes',
    canActivate: [authGuard],
    loadChildren: () =>
      import('./features/solicitudes-estudiante/solicitudes.routes').then(
        (modulo) => modulo.SOLICITUDES_ROUTES
      )
  },
  {
    path: 'administracion',
    title: 'Administración',
    canActivate: [authGuard, roleGuard],
    data: { roles: [ROLES.administrativo] },
    loadComponent: () =>
      import('./features/administracion/administracion.page').then((modulo) => modulo.AdministracionPage)
  },
  {
    path: 'administracion/espacios',
    loadChildren: () =>
      import('./features/areas/areas.routes').then((modulo) => modulo.ESPACIOS_ADMIN_ROUTES)
  },
  {
    path: 'administracion/usuarios',
    title: 'Usuarios',
    canActivate: [authGuard, roleGuard],
    data: { roles: [ROLES.administrativo] },
    loadComponent: () =>
      import('./features/administracion/usuarios/usuarios-admin.page').then(
        (modulo) => modulo.UsuariosAdminPage
      )
  },
  { path: '**', redirectTo: 'inicio' }
];
