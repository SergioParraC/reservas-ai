import { Routes } from '@angular/router';

/** Rutas autenticadas de la bandeja de aprobaciones (Docente y Administrativo). */
export const BANDEJA_ROUTES: Routes = [
  {
    path: '',
    title: 'Bandeja de aprobaciones',
    loadComponent: () => import('./pages/bandeja.page').then((modulo) => modulo.BandejaPage)
  },
  {
    path: ':id',
    title: 'Detalle de la solicitud',
    loadComponent: () =>
      import('./pages/solicitud-detalle.page').then((modulo) => modulo.SolicitudDetallePage)
  }
];
