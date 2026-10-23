import { Routes } from '@angular/router';

/** Rutas autenticadas de series semestrales (el rol Docente se exige al registrar el módulo). */
export const SERIES_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'mis-series' },
  {
    path: 'nueva',
    title: 'Nueva serie semestral',
    loadComponent: () => import('./pages/nueva-serie.page').then((modulo) => modulo.NuevaSeriePage)
  },
  {
    path: 'mis-series',
    title: 'Mis series',
    loadComponent: () => import('./pages/mis-series.page').then((modulo) => modulo.MisSeriesPage)
  }
];
