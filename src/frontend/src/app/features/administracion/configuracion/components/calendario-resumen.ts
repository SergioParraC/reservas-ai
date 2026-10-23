import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit } from '@angular/core';

import {
  aplicaAFecha,
  esDiaCompleto,
  fechaIso,
  FranjaNoOperativa,
  sumarDias
} from '../models/calendario.models';
import { CalendarioStore } from '../estado/calendario.store';

/** Cierres de los próximos siete días con su motivo. */
interface CierreSemana {
  fecha: string;
  motivo: string;
  diaCompleto: boolean;
}

/**
 * Resumen del calendario vigente: periodo activo y cierres de la próxima semana. Se reutiliza
 * en la administración y en las pantallas de reserva para anticipar las restricciones.
 */
@Component({
  selector: 'app-calendario-resumen',
  imports: [DatePipe],
  template: `
    <section class="resumen">
      <h2>Calendario institucional</h2>

      @if (periodo(); as activo) {
        <p class="periodo">
          Periodo activo: <strong>{{ activo.nombre }}</strong>
          ({{ activo.fechaInicio | date: 'dd/MM/yyyy' }} — {{ activo.fechaFin | date: 'dd/MM/yyyy' }})
        </p>
      } @else {
        <p class="alerta" role="alert">
          No hay un periodo académico activo: las reservas, series y solicitudes serán rechazadas.
        </p>
      }

      @if (cierres().length > 0) {
        <p class="titulo">Próximos cierres</p>
        <ul>
          @for (cierre of cierres(); track cierre.fecha) {
            <li>
              <span class="fecha">{{ cierre.fecha | date: 'EEE dd/MM' }}</span>
              <span class="motivo">{{ cierre.motivo }}</span>
              @if (!cierre.diaCompleto) {
                <span class="parcial">Parcial</span>
              }
            </li>
          }
        </ul>
      } @else {
        <p class="sin-cierres">No hay cierres programados en los próximos siete días.</p>
      }
    </section>
  `,
  styles: `
    .resumen {
      padding: 1rem 1.25rem;
      border: 1px solid var(--color-borde);
      border-left: 4px solid var(--color-primario);
      border-radius: 0.5rem;
      background: #f9fafb;
    }

    h2 {
      margin: 0 0 0.5rem;
      font-size: 1rem;
    }

    .periodo {
      margin: 0;
      font-size: 0.9rem;
      color: #374151;
    }

    .alerta {
      margin: 0;
      color: var(--color-error);
      font-size: 0.9rem;
    }

    .titulo {
      margin: 0.75rem 0 0.25rem;
      font-size: 0.85rem;
      font-weight: 600;
      color: #4b5563;
    }

    ul {
      margin: 0;
      padding: 0;
      list-style: none;
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      font-size: 0.85rem;
    }

    li {
      display: flex;
      gap: 0.75rem;
      align-items: center;
    }

    .fecha {
      font-weight: 600;
    }

    .motivo {
      color: #6b7280;
    }

    .parcial {
      font-size: 0.7rem;
      padding: 0.1rem 0.4rem;
      border-radius: 999px;
      background: #fef3c7;
      color: #92400e;
    }

    .sin-cierres {
      margin: 0.5rem 0 0;
      font-size: 0.85rem;
      color: #6b7280;
    }
  `
})
export class CalendarioResumen implements OnInit {
  private readonly store = inject(CalendarioStore);

  readonly periodo = this.store.periodoActivo;

  readonly cierres = computed<CierreSemana[]>(() => {
    const calendario = this.store.vigente();
    const cierres: CierreSemana[] = [];

    for (let indice = 0; indice < 7; indice += 1) {
      const fecha = sumarDias(new Date(), indice);
      const delDia = (calendario?.franjas ?? []).filter((franja: FranjaNoOperativa) =>
        aplicaAFecha(franja, fecha)
      );

      if (delDia.length === 0) {
        continue;
      }

      cierres.push({
        fecha: fechaIso(fecha),
        motivo: delDia[0].motivo,
        diaCompleto: delDia.some(esDiaCompleto)
      });
    }

    return cierres;
  });

  /** Carga el calendario si la vista no lo ha consultado todavía. */
  ngOnInit(): void {
    if (!this.store.vigente()) {
      this.store.cargarVigente().subscribe();
    }
  }
}
