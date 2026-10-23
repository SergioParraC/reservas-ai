import { Component, effect, inject, input, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { DIAS_SEMANA, PeriodoAcademico } from '../../administracion/configuracion/models/calendario.models';
import { PatronSeleccion, TIPOS_RECURRENCIA, TipoRecurrencia } from '../models/serie.models';

/** Formulario del patrón semestral: cadencia, horario y ventana del periodo. */
@Component({
  selector: 'app-patron-form',
  imports: [ReactiveFormsModule],
  template: `
    <form [formGroup]="formulario" class="patron">
      <label>
        Tipo de recurrencia
        <select formControlName="tipoRecurrencia">
          @for (tipo of tipos; track tipo.valor) {
            <option [ngValue]="tipo.valor">{{ tipo.etiqueta }}</option>
          }
        </select>
      </label>

      @if (esFechasFijas()) {
        <label class="ancho">
          Fechas fijas
          <textarea
            formControlName="fechasFijas"
            rows="3"
            placeholder="2026-09-07, 2026-10-19, 2026-11-16"
          ></textarea>
          <span class="ayuda">Separe las fechas con comas o saltos de línea (yyyy-MM-dd).</span>
        </label>
      } @else {
        <label>
          Día de la semana
          <select formControlName="diaSemana">
            @for (dia of dias; track dia.valor) {
              <option [ngValue]="dia.valor">{{ dia.etiqueta }}</option>
            }
          </select>
        </label>
      }

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

      <label>
        Inicio del periodo
        <input type="date" formControlName="periodoInicio" />
      </label>

      <label>
        Fin del periodo
        <input type="date" formControlName="periodoFin" />
      </label>
    </form>
  `,
  styles: `
    .patron {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(13rem, 1fr));
      gap: 0.9rem;
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      font-size: 0.9rem;
      color: #374151;
    }

    label.ancho {
      grid-column: 1 / -1;
    }

    input,
    select,
    textarea {
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      font: inherit;
    }

    .ayuda {
      font-size: 0.8rem;
      color: #6b7280;
    }

    .validacion {
      color: var(--color-error);
      font-size: 0.8rem;
    }
  `
})
export class PatronForm {
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly periodo = input<PeriodoAcademico | null>(null);
  readonly cambio = output<PatronSeleccion>();

  readonly tipos = TIPOS_RECURRENCIA;
  readonly dias = DIAS_SEMANA;

  readonly formulario = this.formularioReactivo.group({
    tipoRecurrencia: [TipoRecurrencia.Semanal, [Validators.required]],
    diaSemana: [2, []],
    fechasFijas: ['', []],
    horaInicio: ['14:00', [Validators.required]],
    horaFin: ['16:00', [Validators.required]],
    periodoInicio: ['', [Validators.required]],
    periodoFin: ['', [Validators.required]]
  });

  constructor() {
    effect(() => {
      const periodo = this.periodo();

      if (periodo) {
        this.formulario.patchValue(
          { periodoInicio: periodo.fechaInicio, periodoFin: periodo.fechaFin },
          { emitEvent: false }
        );
        this.emitir();
      }
    });

    this.formulario.valueChanges.subscribe(() => this.emitir());
  }

  /** Indica si el patrón usa fechas explícitas. */
  esFechasFijas(): boolean {
    return this.formulario.getRawValue().tipoRecurrencia === TipoRecurrencia.FechasFijas;
  }

  /** Indica si la hora final es anterior o igual a la inicial. */
  horasInvalidas(): boolean {
    const valores = this.formulario.getRawValue();
    return Boolean(valores.horaInicio) && Boolean(valores.horaFin) && valores.horaFin <= valores.horaInicio;
  }

  /** Patrón actual listo para la vista previa y el envío. */
  valor(): PatronSeleccion {
    const valores = this.formulario.getRawValue();
    const tipo = Number(valores.tipoRecurrencia) as TipoRecurrencia;

    return {
      tipoRecurrencia: tipo,
      diaSemana: tipo === TipoRecurrencia.FechasFijas ? null : Number(valores.diaSemana),
      fechasFijas: valores.fechasFijas
        .split(/[\n,;]+/)
        .map((fecha) => fecha.trim())
        .filter((fecha) => fecha.length > 0),
      horaInicio: valores.horaInicio,
      horaFin: valores.horaFin,
      periodoInicio: valores.periodoInicio,
      periodoFin: valores.periodoFin
    };
  }

  /** Indica si el formulario puede enviarse. */
  esValido(): boolean {
    return this.formulario.valid && !this.horasInvalidas();
  }

  private emitir(): void {
    this.cambio.emit(this.valor());
  }
}
