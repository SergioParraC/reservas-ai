import { Routes } from '@angular/router';

/** Rutas autenticadas del préstamo directo de puestos y de la radicación con pre-informe. */
export const SOLICITUDES_ROUTES: Routes = [
  {
    path: '',
    title: 'Mis reservas',
    loadComponent: () =>
      import('./pages/mis-reservas.page').then((modulo) => modulo.MisReservasPage)
  },
  {
    path: 'puesto/:puestoId',
    title: 'Disponibilidad del puesto',
    loadComponent: () =>
      import('./pages/puesto-disponibilidad.page').then((modulo) => modulo.PuestoDisponibilidadPage)
  },
  {
    path: 'radicar/:espacioId',
    title: 'Radicar solicitud',
    loadComponent: () =>
      import('./pages/radicar-solicitud.page').then((modulo) => modulo.RadicarSolicitudPage)
  }
];

