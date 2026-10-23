import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import {
  DIAS_SEMANA,
  esDiaCompleto,
  horaCorta,
  nombreDia,
  SolicitudFranja
} from '../models/calendario.models';
import { CalendarioStore } from '../estado/calendario.store';

/** Alta y edición de una franja no operativa global. */
@Component({
  selector: 'app-franja-form',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <a routerLink="/administracion/configuracion" class="volver">← Volver a configuración</a>

    <h1>{{ editando() ? 'Editar franja no operativa' : 'Nueva franja no operativa' }}</h1>

    @if (tienda.error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    }

    @if (criterioInvalido(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    }

    <form [formGroup]="formulario" (ngSubmit)="guardar()" class="formulario" novalidate>
      <fieldset>
        <legend>Criterio</legend>

        <label class="opcion">
          <input type="radio" value="dia" formControlName="criterio" />
          Día de la semana recurrente
        </label>

        <label class="opcion">
          <input type="radio" value="fecha" formControlName="criterio" />
          Fecha específica
        </label>
      </fieldset>

      @if (esRecurrente()) {
        <label>
          Día de la semana
          <select formControlName="diaSemana">
            @for (dia of dias; track dia.valor) {
              <option [ngValue]="dia.valor">{{ dia.etiqueta }}</option>
            }
          </select>
        </label>
      } @else {
        <label>
          Fecha
          <input type="date" formControlName="fechaEspecifica" />
        </label>
      }

      <label class="opcion">
        <input type="checkbox" formControlName="diaCompleto" />
        Día completo (00:00 a 23:59)
      </label>

      @if (!diaCompleto()) {
        <label>
          Hora de inicio
          <input type="time" formControlName="horaInicio" />
        </label>

        <label>
          Hora de fin
          <input type="time" formControlName="horaFin" />
          @if (horasInvalidas()) {
            <span class="validacion">La hora de fin debe ser posterior a la de inicio.</span>
          }
        </label>
      }

      <label class="opcion">
        <input type="checkbox" formControlName="esFeriado" />
        Es feriado institucional
      </label>

      <label>
        Motivo
        <input formControlName="motivo" maxlength="120" placeholder="Cierre dominical, mantenimiento…" />
        @if (motivoInvalido()) {
          <span class="validacion">El motivo es obligatorio (máximo 120 caracteres).</span>
        }
      </label>

      <div class="acciones">
        <button type="submit" [disabled]="tienda.guardando()">
          {{ tienda.guardando() ? 'Guardando…' : 'Guardar franja' }}
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
      max-width: 28rem;
    }

    fieldset {
      margin: 0;
      padding: 0.6rem 0.9rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
    }

    legend {
      padding: 0 0.3rem;
      font-size: 0.85rem;
      color: #4b5563;
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      font-size: 0.9rem;
      color: #374151;
    }

    label.opcion {
      flex-direction: row;
      align-items: center;
      gap: 0.5rem;
    }

    input,
    select {
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      font: inherit;
    }

    input[type='checkbox'],
    input[type='radio'] {
      width: auto;
    }

    .validacion {
      color: var(--color-error);
      font-size: 0.8rem;
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
export class FranjaFormPage implements OnInit {
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly tienda = inject(CalendarioStore);
  readonly dias = DIAS_SEMANA;
  readonly franjaId = signal<string | null>(this.ruta.snapshot.paramMap.get('id'));
  readonly editando = computed(() => this.franjaId() !== null);
  readonly criterioInvalido = signal<string | null>(null);

  private readonly rowVersion = signal<string | null>(null);

  readonly formulario = this.formularioReactivo.group({
    criterio: ['dia', [Validators.required]],
    diaSemana: [0, []],
    fechaEspecifica: ['', []],
    diaCompleto: [true, []],
    horaInicio: ['00:00', []],
    horaFin: ['23:59', []],
    esFeriado: [false, []],
    motivo: ['', [Validators.required, Validators.maxLength(120)]]
  });

  /** Indica si el criterio es recurrente. */
  esRecurrente(): boolean {
    return this.formulario.getRawValue().criterio === 'dia';
  }

  /** Indica si la franja cubre el día completo. */
  diaCompleto(): boolean {
    return this.formulario.getRawValue().diaCompleto;
  }

  /** Carga la franja a editar cuando la ruta lo indica. */
  ngOnInit(): void {
    const id = this.franjaId();

    if (!id) {
      return;
    }

    const preparar = () => {
      const franja = this.tienda.franjas().find((item) => item.id === id);

      if (!franja) {
        return;
      }

      this.rowVersion.set(franja.rowVersion);
      this.formulario.patchValue({
        criterio: franja.fechaEspecifica ? 'fecha' : 'dia',
        diaSemana: franja.diaSemana ?? 0,
        fechaEspecifica: franja.fechaEspecifica ?? '',
        diaCompleto: esDiaCompleto(franja),
        horaInicio: horaCorta(franja.horaInicio),
        horaFin: horaCorta(franja.horaFin),
        esFeriado: franja.esFeriado,
        motivo: franja.motivo
      });
    };

    if (this.tienda.franjas().length === 0) {
      this.tienda.cargar().subscribe(preparar);
    } else {
      preparar();
    }
  }

  /** Crea o actualiza la franja y vuelve a la configuración. */
  guardar(): void {
    this.criterioInvalido.set(null);

    if (this.formulario.invalid || !this.criterioValido() || this.horasInvalidas()) {
      this.formulario.markAllAsTouched();
      return;
    }

    const valores = this.formulario.getRawValue();
    const solicitud: SolicitudFranja = {
      diaSemana: valores.criterio === 'dia' ? nombreDia(valores.diaSemana) : null,
      fechaEspecifica: valores.criterio === 'fecha' ? valores.fechaEspecifica : null,
      horaInicio: valores.diaCompleto ? '00:00' : valores.horaInicio,
      horaFin: valores.diaCompleto ? '23:59' : valores.horaFin,
      esFeriado: valores.esFeriado,
      motivo: valores.motivo,
      rowVersion: this.rowVersion()
    };

    const id = this.franjaId();
    const peticion = id
      ? this.tienda.actualizarFranja(id, solicitud)
      : this.tienda.crearFranja(solicitud);

    peticion.subscribe((franja) => {
      if (franja) {
        void this.router.navigate(['/administracion/configuracion']);
      }
    });
  }

  /** Indica si el motivo muestra su validación local. */
  motivoInvalido(): boolean {
    const control = this.formulario.controls.motivo;
    return control.touched && control.invalid;
  }

  /** Indica si la hora final es anterior a la inicial. */
  horasInvalidas(): boolean {
    const valores = this.formulario.getRawValue();

    if (valores.diaCompleto) {
      return false;
    }

    return valores.horaFin <= valores.horaInicio;
  }

  private criterioValido(): boolean {
    const valores = this.formulario.getRawValue();

    if (valores.criterio === 'fecha' && !valores.fechaEspecifica) {
      this.criterioInvalido.set('Debe indicar la fecha específica de la franja.');
      return false;
    }

    return true;
  }
}
