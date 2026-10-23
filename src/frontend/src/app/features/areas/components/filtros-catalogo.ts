import { Component, DestroyRef, effect, inject, input, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged } from 'rxjs';

import { CATEGORIAS, CategoriaEspacio, FiltrosCatalogo } from '../models/espacio.models';

/** Filtros del catálogo con debounce de 300 ms y emisión al estado. */
@Component({
  selector: 'app-filtros-catalogo',
  imports: [ReactiveFormsModule],
  template: `
    <form [formGroup]="formulario" class="filtros" role="search" (ngSubmit)="emitir()">
      <label>
        Buscar
        <input formControlName="q" placeholder="Nombre o ubicación" />
      </label>

      <label>
        Categoría
        <select formControlName="categoria">
          <option value="">Todas</option>
          @for (categoria of categorias; track categoria.valor) {
            <option [value]="categoria.valor">{{ categoria.etiqueta }}</option>
          }
        </select>
      </label>

      <label>
        Aforo mínimo
        <input type="number" min="1" formControlName="aforoMinimo" />
      </label>

      <button type="button" class="secundario" (click)="limpiar()">Limpiar</button>
    </form>
  `,
  styles: `
    .filtros {
      display: flex;
      flex-wrap: wrap;
      align-items: end;
      gap: 0.75rem;
      padding: 1rem;
      background: #fff;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      font-size: 0.85rem;
      font-weight: 600;
    }

    select {
      padding: 0.55rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.5rem;
      font: inherit;
      background: #fff;
    }

    .secundario {
      background: #fff;
      color: var(--color-primario);
      border: 1px solid var(--color-primario);
    }
  `
})
export class FiltrosCatalogoComponent {
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;
  private readonly destructor = inject(DestroyRef);

  readonly categorias = CATEGORIAS;
  readonly inicial = input<FiltrosCatalogo | null>(null);
  readonly cambio = output<Partial<FiltrosCatalogo>>();

  readonly formulario = this.formularioReactivo.group({
    q: '',
    categoria: '',
    aforoMinimo: this.formularioReactivo.control<number | null>(null)
  });

  constructor() {
    effect(() => {
      const filtros = this.inicial();

      if (!filtros) {
        return;
      }

      this.formulario.patchValue(
        {
          q: filtros.q ?? '',
          categoria: filtros.categoria ? String(filtros.categoria) : '',
          aforoMinimo: filtros.aforoMinimo ?? null
        },
        { emitEvent: false }
      );
    });

    this.formulario.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged((anterior, siguiente) => JSON.stringify(anterior) === JSON.stringify(siguiente)),
        takeUntilDestroyed(this.destructor)
      )
      .subscribe(() => this.emitir());
  }

  /** Emite los filtros normalizados al catálogo. */
  emitir(): void {
    const valores = this.formulario.getRawValue();
    const categoria = Number(valores.categoria);

    this.cambio.emit({
      q: valores.q.trim() || null,
      categoria: Number.isFinite(categoria) && categoria > 0 ? (categoria as CategoriaEspacio) : null,
      aforoMinimo: valores.aforoMinimo && valores.aforoMinimo > 0 ? valores.aforoMinimo : null
    });
  }

  /** Limpia el formulario y emite el estado vacío. */
  limpiar(): void {
    this.formulario.reset({ q: '', categoria: '', aforoMinimo: null }, { emitEvent: false });
    this.cambio.emit({ q: null, categoria: null, aforoMinimo: null, aforoMaximo: null });
  }
}
