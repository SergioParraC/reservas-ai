import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';

import { leerProblema } from '../../../core/interceptors/problem-details.interceptor';
import {
  EspacioResumen,
  FiltrosEspaciosAdmin,
  PaginaResultado
} from '../models/espacio.models';
import { EspaciosApi } from '../services/espacios.api';

const FILTROS_INICIALES: FiltrosEspaciosAdmin = { pagina: 1, tamano: 10 };

/** Estado del mantenimiento administrativo de áreas. */
@Injectable({ providedIn: 'root' })
export class EspaciosAdminStore {
  private readonly api = inject(EspaciosApi);

  readonly filtros = signal<FiltrosEspaciosAdmin>(FILTROS_INICIALES);
  readonly resultado = signal<PaginaResultado<EspacioResumen> | null>(null);
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);
  readonly totalPaginas = computed(() => {
    const pagina = this.resultado();

    return pagina ? Math.max(1, Math.ceil(pagina.total / pagina.tamano)) : 1;
  });

  /** Carga el listado con los filtros indicados. */
  cargar(filtros: Partial<FiltrosEspaciosAdmin> = {}): void {
    const siguiente: FiltrosEspaciosAdmin = {
      ...this.filtros(),
      ...filtros,
      pagina: filtros.pagina ?? this.filtros().pagina
    };

    this.filtros.set(siguiente);
    this.cargando.set(true);
    this.error.set(null);

    this.api.listarAdmin(siguiente).subscribe({
      next: (pagina) => {
        this.cargando.set(false);
        this.resultado.set(pagina);
      },
      error: (error: unknown) => {
        this.cargando.set(false);
        this.error.set(this.mensaje(error));
      }
    });
  }

  /** Aplica filtros y vuelve a la primera página. */
  filtrar(filtros: Partial<FiltrosEspaciosAdmin>): void {
    this.cargar({ ...filtros, pagina: 1 });
  }

  /** Recarga la página vigente. */
  recargar(): void {
    this.cargar();
  }

  /** Navega a la página indicada. */
  irAPagina(pagina: number): void {
    this.cargar({ pagina });
  }

  /** Da de baja el área y recarga el listado. */
  desactivar(id: string): void {
    this.api.desactivar(id).subscribe({
      next: () => this.recargar(),
      error: (error: unknown) => this.error.set(this.mensaje(error))
    });
  }

  private mensaje(error: unknown): string {
    return error instanceof HttpErrorResponse
      ? leerProblema(error)?.detail ?? 'No fue posible cargar las áreas.'
      : 'No fue posible cargar las áreas.';
  }
}
