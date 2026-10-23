import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { esDiaCompleto, FranjaNoOperativa, horaCorta, etiquetaDia } from '../models/calendario.models';

/** Tabla de franjas no operativas con sus acciones de edición y baja. */
@Component({
  selector: 'app-franja-tabla',
  imports: [RouterLink],
  template: `
    <table>
      <thead>
        <tr>
          <th>Criterio</th>
          <th>Horario</th>
          <th>Feriado</th>
          <th>Motivo</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        @for (franja of franjas(); track franja.id) {
          <tr>
            <td>
              {{ franja.fechaEspecifica ?? etiquetaDia(franja.diaSemana) }}
            </td>
            <td>
              @if (diaCompleto(franja)) {
                Día completo
              } @else {
                {{ hora(franja.horaInicio) }} — {{ hora(franja.horaFin) }}
              }
            </td>
            <td>{{ franja.esFeriado ? 'Sí' : 'No' }}</td>
            <td>{{ franja.motivo }}</td>
            <td class="acciones">
              <a [routerLink]="['/administracion/configuracion/franjas', franja.id, 'editar']">Editar</a>
              <button type="button" (click)="eliminar.emit(franja.id)">Eliminar</button>
            </td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: `
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.9rem;
    }

    th,
    td {
      padding: 0.6rem 0.75rem;
      border-bottom: 1px solid var(--color-borde);
      text-align: left;
    }

    th {
      color: #4b5563;
      font-weight: 600;
    }

    .acciones {
      display: flex;
      gap: 0.75rem;
      justify-content: flex-end;
    }

    .acciones a {
      color: var(--color-primario);
    }

    .acciones button {
      border: none;
      background: none;
      color: var(--color-error);
      font: inherit;
      cursor: pointer;
      padding: 0;
    }
  `
})
export class FranjaTabla {
  readonly franjas = input.required<FranjaNoOperativa[]>();
  readonly eliminar = output<string>();

  /** Indica si la franja cubre el día completo. */
  diaCompleto(franja: FranjaNoOperativa): boolean {
    return esDiaCompleto(franja);
  }

  /** Etiqueta legible del criterio recurrente. */
  etiquetaDia(valor: number | null): string {
    return etiquetaDia(valor);
  }

  /** Hora corta del rango de la franja. */
  hora(valor: string): string {
    return horaCorta(valor);
  }
}
