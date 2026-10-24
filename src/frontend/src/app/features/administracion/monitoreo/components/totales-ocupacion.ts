import { Component, Input } from '@angular/core';

import { TotalesOcupacion } from '../models/monitoreo.models';

/** Totales institucionales del mapa: aforo, puestos activos, uso, pendientes y libres. */
@Component({
  selector: 'app-totales-ocupacion',
  template: `
    <section class="totales" aria-label="Totales de ocupación">
      <article class="tarjeta">
        <span class="valor">{{ totales.puestosActivos }}</span>
        <span class="etiqueta">Puestos activos</span>
      </article>
      <article class="tarjeta en-uso">
        <span class="valor">{{ totales.enUso }}</span>
        <span class="etiqueta">En uso</span>
      </article>
      <article class="tarjeta pendiente">
        <span class="valor">{{ totales.pendientesCheckIn }}</span>
        <span class="etiqueta">Pendientes de check-in</span>
      </article>
      <article class="tarjeta no-show">
        <span class="valor">{{ totales.sesionesSinCerrar }}</span>
        <span class="etiqueta">Sesiones sin cerrar</span>
      </article>
      <article class="tarjeta libre">
        <span class="valor">{{ totales.libres }}</span>
        <span class="etiqueta">Libres</span>
      </article>
      <article class="tarjeta">
        <span class="valor">{{ porcentaje() }}%</span>
        <span class="etiqueta">Ocupación (aforo {{ totales.aforo }})</span>
      </article>
    </section>
  `,
  styles: `
    .totales {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
      gap: 0.75rem;
      margin: 1rem 0 1.5rem;
    }

    .tarjeta {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
      padding: 0.9rem 1rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.6rem;
      background: #fff;
    }

    .valor {
      font-size: 1.6rem;
      font-weight: 700;
      color: var(--color-primario);
    }

    .etiqueta {
      font-size: 0.8rem;
      color: #6b7280;
    }

    .en-uso .valor {
      color: #1d4ed8;
    }

    .pendiente .valor {
      color: #b45309;
    }

    .no-show .valor {
      color: #b91c1c;
    }

    .libre .valor {
      color: #047857;
    }
  `
})
export class TotalesOcupacionPanel {
  @Input({ required: true }) totales!: TotalesOcupacion;

  /** Porcentaje con un decimal como máximo, sin ceros innecesarios. */
  porcentaje(): string {
    const valor = this.totales.porcentajeOcupacion;

    return Number.isInteger(valor) ? String(valor) : valor.toFixed(1);
  }
}
