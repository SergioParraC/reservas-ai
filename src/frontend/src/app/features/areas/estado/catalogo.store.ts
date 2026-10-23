import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, finalize, of, Subject, switchMap } from 'rxjs';

import { leerProblema } from '../../../core/interceptors/problem-details.interceptor';
import {
  CategoriaEspacio,
  EspacioCatalogo,
  FiltrosCatalogo,
  PaginaResultado
} from '../models/espacio.models';
import { EspaciosApi } from '../services/espacios.api';

const FILTROS_INICIALES: FiltrosCatalogo = { pagina: 1, tamano: 12 };

/** Estado del catálogo público: filtros con `debounce`, resultados y paginación. */
@Injectable({ providedIn: 'root' })
export class CatalogoStore {
  private readonly api = inject(EspaciosApi);
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);
  private readonly destructor = inject(DestroyRef);
  private readonly cambios = new Subject<FiltrosCatalogo>();

  readonly filtros = signal<FiltrosCatalogo>(FILTROS_INICIALES);
  readonly resultado = signal<PaginaResultado<EspacioCatalogo> | null>(null);
  readonly cargando = signal(false);
  readonly error = signal<string | null>(null);
  readonly totalPaginas = computed(() => {
    const pagina = this.resultado();

    return pagina ? Math.max(1, Math.ceil(pagina.total / pagina.tamano)) : 1;
  });
  readonly filtrosActivos = computed(() => {
    const actuales = this.filtros();

    return [
      actuales.q,
      actuales.categoria ? String(actuales.categoria) : null,
      actuales.aforoMinimo ? String(actuales.aforoMinimo) : null
    ].filter((valor): valor is string => Boolean(valor));
  });

  constructor() {
    this.cambios
      .pipe(
        debounceTime(300),
        distinctUntilChanged((anterior, siguiente) => JSON.stringify(anterior) === JSON.stringify(siguiente)),
        switchMap((filtros) => this.consultar(filtros)),
        takeUntilDestroyed(this.destructor)
      )
      .subscribe((pagina) => {
        if (pagina) {
          this.resultado.set(pagina);
        }
      });

    const inicial = this.leerParametros(this.ruta.snapshot.queryParamMap);
    this.filtros.set(inicial);
    this.consultar(inicial).subscribe((pagina) => {
      if (pagina) {
        this.resultado.set(pagina);
      }
    });
  }

  /** Aplica filtros parciales, reinicia la página y refleja el estado en la URL. */
  aplicarFiltros(parcial: Partial<FiltrosCatalogo>): void {
    const siguiente: FiltrosCatalogo = {
      ...this.filtros(),
      ...parcial,
      pagina: parcial.pagina ?? 1
    };

    this.filtros.set(siguiente);
    this.escribirParametros(siguiente);
    this.cambios.next(siguiente);
  }

  /** Navega a una página concreta. */
  irAPagina(pagina: number): void {
    this.aplicarFiltros({ pagina });
  }

  /** Limpia los filtros y vuelve a la primera página. */
  limpiarFiltros(): void {
    this.aplicarFiltros({ q: null, categoria: null, aforoMinimo: null, aforoMaximo: null, pagina: 1 });
  }

  private consultar(filtros: FiltrosCatalogo) {
    this.cargando.set(true);
    this.error.set(null);

    return this.api.listarCatalogo(filtros).pipe(
      finalize(() => this.cargando.set(false)),
      catchError((error: unknown) => {
        this.error.set(this.mensaje(error));
        return of(null);
      })
    );
  }

  private escribirParametros(filtros: FiltrosCatalogo): void {
    const queryParams: Record<string, string | number | null> = {
      q: filtros.q ?? null,
      categoria: filtros.categoria ?? null,
      aforoMinimo: filtros.aforoMinimo ?? null,
      aforoMaximo: filtros.aforoMaximo ?? null
    };

    void this.router.navigate([], { queryParams, replaceUrl: true });
  }

  private leerParametros(parametros: ParamMap): FiltrosCatalogo {
    const categoria = Number(parametros.get('categoria'));
    const aforoMinimo = Number(parametros.get('aforoMinimo'));
    const aforoMaximo = Number(parametros.get('aforoMaximo'));
    const pagina = Number(parametros.get('pagina'));

    return {
      q: parametros.get('q'),
      categoria: Number.isFinite(categoria) && categoria > 0 ? (categoria as CategoriaEspacio) : null,
      aforoMinimo: Number.isFinite(aforoMinimo) && aforoMinimo > 0 ? aforoMinimo : null,
      aforoMaximo: Number.isFinite(aforoMaximo) && aforoMaximo > 0 ? aforoMaximo : null,
      pagina: Number.isFinite(pagina) && pagina > 0 ? pagina : 1,
      tamano: 12
    };
  }

  private mensaje(error: unknown): string {
    return error instanceof HttpErrorResponse
      ? leerProblema(error)?.detail ?? 'No fue posible cargar el catálogo.'
      : 'No fue posible cargar el catálogo.';
  }
}
