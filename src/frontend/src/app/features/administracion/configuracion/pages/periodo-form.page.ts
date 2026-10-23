import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { CalendarioStore } from '../estado/calendario.store';

/** Alta y edición de un periodo académico con fechas y estado inactivo inicial. */
@Component({
  selector: 'app-periodo-form',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <a routerLink="/administracion/configuracion" class="volver">← Volver a configuración</a>

    <h1>{{ editando() ? 'Editar periodo' : 'Nuevo periodo' }}</h1>

    @if (tienda.error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    }

    <form [formGroup]="formulario" (ngSubmit)="guardar()" class="formulario" novalidate>
      <label>
        Nombre
        <input formControlName="nombre" maxlength="40" placeholder="2026-2" />
        @if (nombreInvalido()) {
          <span class="validacion">El nombre es obligatorio (máximo 40 caracteres).</span>
        }
      </label>

      <label>
        Fecha de inicio
        <input type="date" formControlName="fechaInicio" />
        @if (fechasInvalidas()) {
          <span class="validacion">El rango de fechas es obligatorio y el fin no puede ser anterior al inicio.</span>
        }
      </label>

      <label>
        Fecha de fin
        <input type="date" formControlName="fechaFin" />
      </label>

      <p class="nota">
        El periodo se registra inactivo. Actívelo desde la configuración para habilitar reservas dentro
        de su ventana.
      </p>

      <div class="acciones">
        <button type="submit" [disabled]="tienda.guardando()">
          {{ tienda.guardando() ? 'Guardando…' : 'Guardar periodo' }}
        </button>
        <a routerLink="/administracion/configuracion">Cancelar</a>
      </div>
    </form>
  `,
  styles: `
    .volver {
      display: inline-block;
      margin-bottom: 1rem;
      color: var(--color-primario);
      text-decoration: none;
    }

    .formulario {
      display: flex;
      flex-direction: column;
      gap: 0.9rem;
      max-width: 26rem;
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      font-size: 0.9rem;
      color: #374151;
    }

    input {
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      font: inherit;
    }

    .validacion {
      color: var(--color-error);
      font-size: 0.8rem;
    }

    .nota {
      margin: 0;
      font-size: 0.85rem;
      color: #6b7280;
    }

    .acciones {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-top: 0.5rem;
    }

    .acciones button {
      padding: 0.5rem 1rem;
      border: none;
      border-radius: 0.4rem;
      background: var(--color-primario);
      color: #fff;
      font: inherit;
      cursor: pointer;
    }

    .acciones button:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .error {
      padding: 0.8rem 1rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
    }
  `
})
export class PeriodoFormPage implements OnInit {
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly tienda = inject(CalendarioStore);
  readonly periodoId = signal<string | null>(this.ruta.snapshot.paramMap.get('id'));
  readonly editando = computed(() => this.periodoId() !== null);

  private readonly rowVersion = signal<string | null>(null);

  readonly formulario = this.formularioReactivo.group({
    nombre: ['', [Validators.required, Validators.maxLength(40)]],
    fechaInicio: ['', [Validators.required]],
    fechaFin: ['', [Validators.required]]
  });

  /** Carga el periodo a editar cuando la ruta lo indica. */
  ngOnInit(): void {
    const id = this.periodoId();

    if (!id) {
      return;
    }

    const preparar = () => {
      const periodo = this.tienda.periodos().find((item) => item.id === id);

      if (!periodo) {
        return;
      }

      this.rowVersion.set(periodo.rowVersion);
      this.formulario.patchValue({
        nombre: periodo.nombre,
        fechaInicio: periodo.fechaInicio,
        fechaFin: periodo.fechaFin
      });
    };

    if (this.tienda.periodos().length === 0) {
      this.tienda.cargar().subscribe(preparar);
    } else {
      preparar();
    }
  }

  /** Crea o actualiza el periodo y vuelve a la configuración. */
  guardar(): void {
    if (this.formulario.invalid || this.fechasInvalidas()) {
      this.formulario.markAllAsTouched();
      return;
    }

    const valores = this.formulario.getRawValue();
    const solicitud = {
      nombre: valores.nombre,
      fechaInicio: valores.fechaInicio,
      fechaFin: valores.fechaFin
    };

    const id = this.periodoId();
    const peticion = id
      ? this.tienda.actualizarPeriodo(id, { ...solicitud, rowVersion: this.rowVersion() })
      : this.tienda.crearPeriodo(solicitud);

    peticion.subscribe((periodo) => {
      if (periodo) {
        void this.router.navigate(['/administracion/configuracion']);
      }
    });
  }

  /** Indica si el nombre muestra su validación local. */
  nombreInvalido(): boolean {
    const control = this.formulario.controls.nombre;
    return control.touched && control.invalid;
  }

  /** Indica si el rango de fechas es inválido. */
  fechasInvalidas(): boolean {
    const { fechaInicio, fechaFin } = this.formulario.getRawValue();

    if (!fechaInicio || !fechaFin) {
      return false;
    }

    return fechaFin < fechaInicio;
  }
}
