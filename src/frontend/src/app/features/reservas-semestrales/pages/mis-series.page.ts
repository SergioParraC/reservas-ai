import { DatePipe } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';

import { etiquetaDia, horaCorta } from '../../administracion/configuracion/models/calendario.models';
import { SeriesStore } from '../estado/series.store';
import { etiquetaTipoRecurrencia } from '../models/serie.models';

/** Listado de las series del docente con estados de carga, error y vacío. */
@Component({
  selector: 'app-mis-series',
  imports: [DatePipe, RouterLink],
  template: `
    <header class="encabezado">
      <div>
        <h1>Mis series semestrales</h1>
        <p class="ayuda">Series de área completa radicadas, de la más reciente a la más antigua.</p>
      </div>
      <a routerLink="/reservas-semestrales/nueva" class="accion">Nueva serie</a>
    </header>

    @if (tienda.error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    }

    @if (tienda.aviso(); as mensaje) {
      <p class="aviso" role="status">{{ mensaje }}</p>
    }

    @if (tienda.cargando()) {
      <p class="estado" role="status">Cargando series…</p>
    } @else if (tienda.series(); as pagina) {
      @if (pagina.elementos.length === 0) {
        <p class="vacio" role="status">
          Aún no tiene series registradas. Radique una desde <strong>Nueva serie</strong>.
        </p>
      } @else {
        <p class="total">{{ pagina.total }} serie(s)</p>

        <ul class="lista">
          @for (serie of pagina.elementos; track serie.id) {
            <li>
              <div class="principal">
                <strong>{{ serie.materia }}</strong>
                <span class="area">{{ serie.espacioNombre }}</span>
              </div>

              <div class="detalle">
                <span>{{ etiquetaTipo(serie.tipoRecurrencia) }}</span>
                @if (serie.diaSemana !== null) {
                  <span>{{ etiquetaDiaSemana(serie.diaSemana) }}</span>
                }
                <span>{{ hora(serie.horaInicio) }}–{{ hora(serie.horaFin) }}</span>
                <span>
                  {{ serie.periodoInicio | date: 'dd/MM/yyyy' }} —
                  {{ serie.periodoFin | date: 'dd/MM/yyyy' }}
                </span>
                <span class="ocurrencias">{{ serie.totalOcurrencias }} ocurrencia(s)</span>
              </div>

              <span class="registro">Radicada {{ serie.creadaEn | date: 'dd/MM/yyyy HH:mm' }}</span>
            </li>
          }
        </ul>

        <nav class="paginacion" aria-label="Paginación de series">
          <button type="button" (click)="ir(pagina.pagina - 1)" [disabled]="pagina.pagina <= 1">
            Anterior
          </button>
          <span>Página {{ pagina.pagina }} de {{ tienda.totalPaginas() }}</span>
          <button
            type="button"
            (click)="ir(pagina.pagina + 1)"
            [disabled]="pagina.pagina >= tienda.totalPaginas()"
          >
            Siguiente
          </button>
        </nav>
      }
    }
  `,
  styles: `
    .encabezado {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 1rem;
    }

    .encabezado h1 {
      margin: 0 0 0.25rem;
    }

    .ayuda {
      margin: 0;
      color: #6b7280;
    }

    .accion {
      padding: 0.45rem 0.9rem;
      border-radius: 0.4rem;
      background: var(--color-primario);
      color: #fff;
      text-decoration: none;
      font-size: 0.9rem;
    }

    .lista {
      margin: 1rem 0 0;
      padding: 0;
      list-style: none;
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
    }

    li {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      padding: 0.85rem 1rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.5rem;
      background: #fff;
    }

    .principal {
      display: flex;
      align-items: baseline;
      gap: 0.75rem;
    }

    .area {
      color: #6b7280;
      font-size: 0.9rem;
    }

    .detalle {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem 1rem;
      font-size: 0.85rem;
      color: #4b5563;
    }

    .ocurrencias {
      font-weight: 600;
      color: var(--color-primario);
    }

    .registro {
      font-size: 0.78rem;
      color: #9ca3af;
    }

    .paginacion {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 1rem;
      margin-top: 1.5rem;
    }

    .paginacion button {
      padding: 0.4rem 0.9rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      background: #fff;
      font: inherit;
      cursor: pointer;
    }

    .paginacion button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .error {
      padding: 0.8rem 1rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
    }

    .aviso {
      padding: 0.8rem 1rem;
      border-left: 4px solid #10b981;
      background: #ecfdf5;
      color: #047857;
      border-radius: 0.25rem;
    }

    .estado,
    .vacio,
    .total {
      color: #6b7280;
    }
  `
})
export class MisSeriesPage implements OnInit {
  readonly tienda = inject(SeriesStore);

  /** Carga la primera página de series propias. */
  ngOnInit(): void {
    this.tienda.cargarSeries(1, 10).subscribe();
  }

  /** Navega a la página indicada si está dentro del rango. */
  ir(pagina: number): void {
    if (pagina < 1 || pagina > this.tienda.totalPaginas()) {
      return;
    }

    this.tienda.cargarSeries(pagina, 10).subscribe();
  }

  /** Etiqueta legible del tipo de recurrencia. */
  etiquetaTipo(valor: number): string {
    return etiquetaTipoRecurrencia(valor);
  }

  /** Etiqueta legible del día de la semana. */
  etiquetaDiaSemana(valor: number | null): string {
    return etiquetaDia(valor);
  }

  /** Hora corta de la ocurrencia. */
  hora(valor: string): string {
    return horaCorta(valor);
  }
}
