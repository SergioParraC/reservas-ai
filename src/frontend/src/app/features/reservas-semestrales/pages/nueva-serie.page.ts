import { Component, computed, inject, OnInit, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { CalendarioResumen } from '../../administracion/configuracion/components/calendario-resumen';
import { CalendarioStore } from '../../administracion/configuracion/estado/calendario.store';
import { EspacioCatalogo, TipoModalidadPrestamo } from '../../areas/models/espacio.models';
import { EspaciosApi } from '../../areas/services/espacios.api';
import { OcurrenciasPreview } from '../components/ocurrencias-preview';
import { PatronForm } from '../components/patron-form';
import { SeriesStore } from '../estado/series.store';
import { PatronSeleccion } from '../models/serie.models';
import { previsualizarOcurrencias } from '../models/serie.preview';

/** Radicación de una serie semestral de área completa con vista previa del calendario. */
@Component({
  selector: 'app-nueva-serie',
  imports: [ReactiveFormsModule, RouterLink, PatronForm, OcurrenciasPreview, CalendarioResumen],
  template: `
    <a routerLink="/reservas-semestrales/mis-series" class="volver">← Mis series</a>

    <header class="encabezado">
      <h1>Nueva serie semestral</h1>
      <p class="ayuda">
        La serie se confirma como un todo: si una ocurrencia colisiona con la agenda del área, no se
        registra ninguna. Los días cerrados del calendario se omiten automáticamente.
      </p>
    </header>

    <app-calendario-resumen class="resumen" />

    @if (calendario.periodoActivo(); as periodo) {
      @if (tienda.error(); as mensaje) {
        <p class="error" role="alert">{{ mensaje }}</p>
      }

      @if (mensaje(); as texto) {
        <p class="advertencia" role="alert">{{ texto }}</p>
      }

      <form [formGroup]="formulario" class="datos" novalidate>
        <label>
          Área completa
          <select [value]="areaId()" (change)="seleccionarArea($event)">
            <option value="">Seleccione un área…</option>
            @for (area of areas(); track area.id) {
              <option [value]="area.id">{{ area.nombre }} · {{ area.ubicacion }}</option>
            }
          </select>
        </label>

        <label>
          Materia
          <input formControlName="materia" maxlength="120" placeholder="Cálculo II" />
          @if (materiaInvalida()) {
            <span class="validacion">La materia es obligatoria (máximo 120 caracteres).</span>
          }
        </label>
      </form>

      <app-patron-form [periodo]="periodo" (cambio)="actualizarPatron($event)" class="patron" />

      <app-ocurrencias-preview [ocurrencias]="ocurrencias()" class="preview" />

      <div class="acciones">
        <button type="button" (click)="enviar()" [disabled]="!puedeEnviar()">
          {{ tienda.guardando() ? 'Confirmando…' : 'Confirmar serie' }}
        </button>
        <span class="conteo">{{ confirmadas() }} ocurrencia(s) operativa(s)</span>
      </div>
    } @else {
      <p class="error" role="alert">
        No hay un periodo académico activo. Un administrativo debe activar uno antes de radicar series.
      </p>
    }
  `,
  styles: `
    .volver {
      display: inline-block;
      margin-bottom: 1rem;
      color: var(--color-primario);
      text-decoration: none;
    }

    .encabezado h1 {
      margin: 0 0 0.25rem;
    }

    .ayuda {
      margin: 0;
      color: #6b7280;
      max-width: 52rem;
    }

    .resumen,
    .patron,
    .preview {
      display: block;
      margin-top: 1rem;
    }

    .datos {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr));
      gap: 0.9rem;
      margin-top: 1rem;
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      font-size: 0.9rem;
      color: #374151;
    }

    input,
    select {
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      font: inherit;
    }

    .validacion {
      color: var(--color-error);
      font-size: 0.8rem;
    }

    .acciones {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-top: 1.25rem;
    }

    .acciones button {
      padding: 0.55rem 1.1rem;
      border: none;
      border-radius: 0.4rem;
      background: var(--color-primario);
      color: #fff;
      font: inherit;
      cursor: pointer;
    }

    .acciones button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .conteo {
      font-size: 0.85rem;
      color: #6b7280;
    }

    .error {
      padding: 0.8rem 1rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
    }

    .advertencia {
      padding: 0.8rem 1rem;
      border-left: 4px solid #f59e0b;
      background: #fffbeb;
      color: #92400e;
      border-radius: 0.25rem;
    }
  `
})
export class NuevaSeriePage implements OnInit {
  private readonly espaciosApi = inject(EspaciosApi);
  private readonly router = inject(Router);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly tienda = inject(SeriesStore);
  readonly calendario = inject(CalendarioStore);
  readonly patronForm = viewChild(PatronForm);

  readonly areas = signal<EspacioCatalogo[]>([]);
  readonly areaId = signal('');
  readonly patron = signal<PatronSeleccion | null>(null);
  readonly mensaje = signal<string | null>(null);

  readonly formulario = this.formularioReactivo.group({
    materia: ['', [Validators.required, Validators.maxLength(120)]]
  });

  readonly ocurrencias = computed(() => {
    const patron = this.patron();

    return patron ? previsualizarOcurrencias(patron, this.calendario.vigente()) : [];
  });

  readonly confirmadas = computed(
    () => this.ocurrencias().filter((ocurrencia) => !ocurrencia.omitida && !ocurrencia.conflicto).length
  );

  readonly puedeEnviar = computed(
    () => Boolean(this.areaId()) && this.formulario.valid && this.confirmadas() > 0
  );

  /** Carga el calendario vigente y las áreas con modalidad de área completa. */
  ngOnInit(): void {
    this.calendario.cargarVigente().subscribe();

    this.espaciosApi.listarCatalogo({ pagina: 1, tamano: 50 }).subscribe({
      next: (pagina) =>
        this.areas.set(
          pagina.elementos.filter(
            (area) => area.tipoModalidadPrestamo === TipoModalidadPrestamo.AreaCompleta
          )
        ),
      error: () => this.mensaje.set('No fue posible cargar las áreas disponibles.')
    });
  }

  /** Actualiza el patrón y la vista previa. */
  actualizarPatron(patron: PatronSeleccion): void {
    this.patron.set(patron);
    this.mensaje.set(null);
  }

  /** Registra el área seleccionada. */
  seleccionarArea(evento: Event): void {
    this.areaId.set((evento.target as HTMLSelectElement).value);
  }

  /** Radica la serie y navega al listado propio. */
  enviar(): void {
    const patron = this.patron();
    const componente = this.patronForm();

    if (!patron || !componente || !componente.esValido() || !this.areaId() || this.formulario.invalid) {
      this.formulario.markAllAsTouched();
      this.mensaje.set('Complete el área, la materia y el patrón antes de confirmar.');
      return;
    }

    if (this.confirmadas() === 0) {
      this.mensaje.set('La serie no genera ocurrencias operativas: ajuste el patrón o el periodo.');
      return;
    }

    this.mensaje.set(null);

    this.tienda
      .crear({
        ...patron,
        espacioId: this.areaId(),
        materia: this.formulario.getRawValue().materia
      })
      .subscribe((serie) => {
        if (serie) {
          void this.router.navigate(['/reservas-semestrales/mis-series']);
        }
      });
  }

  /** Indica si la materia muestra su validación local. */
  materiaInvalida(): boolean {
    const control = this.formulario.controls.materia;
    return control.touched && control.invalid;
  }
}
