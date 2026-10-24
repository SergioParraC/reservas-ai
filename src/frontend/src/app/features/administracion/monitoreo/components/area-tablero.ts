import { Component, Input } from '@angular/core';

import {
  formatearHora,
  formatearRango
} from '../../../solicitudes-estudiante/mappers/reserva.mapper';
import { AreaOcupacion, ESTADOS_TABLERO, EstadoPuesto } from '../models/monitoreo.models';

/** Tablero de un área con sus indicadores, la leyenda de estados y las sesiones en curso. */
@Component({
  selector: 'app-area-tablero',
  template: `
    <article class="tablero">
      <header class="cabecera">
        <h2>{{ area.nombre }}</h2>
        <span class="porcentaje">{{ porcentaje() }}%</span>
      </header>

      <div
        class="barra"
        role="img"
        [attr.aria-label]="'Ocupación de ' + area.nombre + ': ' + porcentaje() + ' por ciento'"
      >
        <span class="relleno en-uso" [style.width.%]="ancho(area.enUso)"></span>
        <span class="relleno pendiente" [style.width.%]="ancho(area.pendientesCheckIn)"></span>
      </div>

      <p class="resumen">
        Aforo {{ area.aforo }} · {{ area.puestosActivos }} puesto(s) activo(s) ·
        {{ area.libres }} libre(s)
      </p>

      <ul class="leyenda">
        @for (item of estados; track item.estado) {
          <li [attr.data-estado]="item.estado">
            <span class="punto"></span>
            {{ item.etiqueta }}: <strong>{{ conteo(item.estado) }}</strong>
          </li>
        }
      </ul>

      @if (area.sesionesSinCerrar > 0) {
        <p class="alerta" role="alert">
          {{ area.sesionesSinCerrar }} sesión(es) sin cerrar: el titular no registró la salida y la
          franja ya terminó.
        </p>
      }

      @if (area.proximaLiberacion) {
        <p class="nota">Próxima liberación: {{ hora(area.proximaLiberacion) }}</p>
      }

      <h3>Sesiones en curso</h3>

      @if (area.enCurso.length === 0) {
        <p class="vacio">Sin sesiones en uso en este momento.</p>
      } @else {
        <ul class="sesiones">
          @for (sesion of area.enCurso; track sesion.reservaId) {
            <li>
              <span class="puesto">{{ sesion.puestoIdentificador }}</span>
              <span class="titular">{{ sesion.titular }}</span>
              <span class="horario">{{ rango(sesion.inicio, sesion.fin) }}</span>
              @if (sesion.fechaCheckIn) {
                <span class="ingreso">Ingreso {{ hora(sesion.fechaCheckIn) }}</span>
              }
            </li>
          }
        </ul>
      }
    </article>
  `,
  styles: `
    .tablero {
      padding: 1rem 1.1rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
      background: #fff;
    }

    .cabecera {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      gap: 0.5rem;
    }

    h2 {
      margin: 0;
      font-size: 1.05rem;
    }

    h3 {
      margin: 1rem 0 0.4rem;
      font-size: 0.95rem;
      color: #374151;
    }

    .porcentaje {
      font-weight: 700;
      color: var(--color-primario);
    }

    .barra {
      display: flex;
      height: 0.6rem;
      margin: 0.6rem 0;
      border-radius: 999px;
      background: #e5e7eb;
      overflow: hidden;
    }

    .relleno {
      display: block;
      height: 100%;
    }

    .relleno.en-uso {
      background: #2563eb;
    }

    .relleno.pendiente {
      background: #f59e0b;
    }

    .resumen,
    .nota,
    .vacio {
      margin: 0.25rem 0;
      color: #6b7280;
      font-size: 0.85rem;
    }

    .leyenda {
      display: flex;
      flex-wrap: wrap;
      gap: 0.9rem;
      margin: 0.6rem 0 0;
      padding: 0;
      list-style: none;
      font-size: 0.85rem;
      color: #374151;
    }

    .leyenda li {
      display: flex;
      align-items: center;
      gap: 0.35rem;
    }

    .punto {
      width: 0.6rem;
      height: 0.6rem;
      border-radius: 999px;
      background: #9ca3af;
    }

    li[data-estado='libre'] .punto {
      background: #10b981;
    }

    li[data-estado='en-uso'] .punto {
      background: #2563eb;
    }

    li[data-estado='pendiente'] .punto {
      background: #f59e0b;
    }

    li[data-estado='no-show'] .punto {
      background: #dc2626;
    }

    .alerta {
      margin: 0.6rem 0 0;
      padding: 0.5rem 0.7rem;
      border-left: 4px solid #dc2626;
      background: #fef2f2;
      color: #b91c1c;
      border-radius: 0.25rem;
      font-size: 0.85rem;
    }

    .sesiones {
      display: grid;
      gap: 0.4rem;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .sesiones li {
      display: flex;
      flex-wrap: wrap;
      gap: 0.6rem;
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      font-size: 0.85rem;
      color: #4b5563;
    }

    .puesto {
      font-weight: 700;
      color: var(--color-primario);
    }

    .ingreso {
      color: #047857;
    }
  `
})
export class AreaTablero {
  @Input({ required: true }) area!: AreaOcupacion;

  readonly estados = ESTADOS_TABLERO;

  /** Porcentaje con un decimal como máximo. */
  porcentaje(): string {
    const valor = this.area.porcentajeOcupacion;

    return Number.isInteger(valor) ? String(valor) : valor.toFixed(1);
  }

  /** Cantidad asociada a cada estado de la leyenda. */
  conteo(estado: EstadoPuesto): number {
    switch (estado) {
      case 'libre':
        return this.area.libres;
      case 'en-uso':
        return this.area.enUso;
      case 'pendiente':
        return this.area.pendientesCheckIn;
      case 'no-show':
        return this.area.sesionesSinCerrar;
    }
  }

  /** Ancho del segmento de la barra, acotado a los puestos activos. */
  ancho(cantidad: number): number {
    if (this.area.puestosActivos === 0) {
      return 0;
    }

    return Math.min(100, Math.round((cantidad * 100) / this.area.puestosActivos));
  }

  /** Hora local del instante indicado. */
  hora(instante: string): string {
    return formatearHora(instante);
  }

  /** Rango horario de la sesión. */
  rango(inicio: string, fin: string): string {
    return formatearRango(inicio, fin);
  }
}
