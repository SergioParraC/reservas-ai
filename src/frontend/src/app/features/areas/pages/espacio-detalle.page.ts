import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import {
  etiquetaCategoria,
  etiquetaElemento,
  etiquetaModalidad,
  etiquetaTipoAprobacion,
  EspacioPublico,
  TipoAprobacion,
  TipoModalidadPrestamo
} from '../models/espacio.models';
import { EspaciosApi } from '../services/espacios.api';

/** Detalle público de un área activa, con su fotografía y puestos disponibles. */
@Component({
  selector: 'app-espacio-detalle',
  imports: [RouterLink],
  template: `
    <a routerLink="/catalogo" class="volver">← Volver al catálogo</a>

    @if (error(); as mensaje) {
      <p class="error" role="alert">{{ mensaje }}</p>
    } @else if (area(); as datos) {
      <article class="detalle">
        @if (datos.fotografiaUrl) {
          <img [src]="datos.fotografiaUrl" [alt]="'Fotografía de ' + datos.nombre" />
        }

        <div class="contenido">
          <h1>{{ datos.nombre }}</h1>
          <p class="ubicacion">{{ datos.ubicacion }}</p>

          <dl>
            <dt>Categoría</dt>
            <dd>{{ categoria() }}</dd>
            <dt>Aforo</dt>
            <dd>{{ datos.aforo }} personas</dd>
            <dt>Aprobación</dt>
            <dd>{{ aprobacion() }}</dd>
            <dt>Modalidad</dt>
            <dd>{{ modalidad() }}</dd>
            <dt>Elemento</dt>
            <dd>{{ elemento() }}</dd>
            <dt>Puestos activos</dt>
            <dd>{{ datos.puestosActivos }}</dd>
          </dl>

          @if (admiteSolicitud()) {
            <p class="solicitud">
              <a [routerLink]="['/solicitudes/radicar', datos.id]">Radicar solicitud con pre-informe →</a>
            </p>
          }

          @if (datos.puestos.length > 0) {
            <section class="puestos">
              <h2>{{ admitePrestamoDirecto() ? 'Reserva inmediata de puestos' : 'Puestos del área' }}</h2>

              @if (!admitePrestamoDirecto()) {
                <p class="nota">
                  El préstamo directo solo está disponible en áreas con aprobación automática.
                  Esta área requiere aprobación {{ aprobacion().toLowerCase() }}, así que sus puestos no
                  se reservan desde aquí.
                </p>
              }

              <ul>
                @for (puesto of datos.puestos; track puesto.id) {
                  <li>
                    <span class="identificador">{{ puesto.identificador }}</span>
                    <span class="equipo">{{ puesto.descripcionEquipo ?? 'Equipo de cómputo' }}</span>
                    @if (admitePrestamoDirecto()) {
                      <a [routerLink]="['/solicitudes/puesto', puesto.id]">Reservar</a>
                    }
                  </li>
                }
              </ul>
            </section>
          } @else if (admitePrestamoDirecto()) {
            <p class="nota">Esta área aún no tiene puestos activos para préstamo directo.</p>
          } @else if (!admiteSolicitud()) {
            <p class="nota">
              Esta área no tiene puestos de cómputo y su modalidad no admite solicitudes de área
              completa.
            </p>
          }
        </div>
      </article>
    } @else {
      <p role="status">Cargando área…</p>
    }
  `,
  styles: `
    .volver {
      display: inline-block;
      margin-bottom: 1rem;
      color: var(--color-primario);
      text-decoration: none;
    }

    .detalle {
      background: #fff;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
      overflow: hidden;
    }

    img {
      width: 100%;
      max-height: 20rem;
      object-fit: cover;
    }

    .contenido {
      padding: 1.5rem;
    }

    h1 {
      margin: 0 0 0.25rem;
    }

    .ubicacion {
      margin: 0 0 1rem;
      color: #6b7280;
    }

    dl {
      display: grid;
      grid-template-columns: 10rem 1fr;
      gap: 0.4rem 1rem;
    }

    dt {
      font-weight: 600;
      color: #4b5563;
    }

    dd {
      margin: 0;
    }

    .error {
      padding: 0.8rem 1rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
    }

    .puestos {
      margin-top: 1.5rem;
      border-top: 1px solid var(--color-borde);
      padding-top: 1rem;
    }

    .puestos h2 {
      margin: 0 0 0.5rem;
      font-size: 1.05rem;
    }

    .puestos ul {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .puestos li {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 0.5rem 0.7rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
    }

    .identificador {
      font-weight: 600;
    }

    .equipo {
      flex: 1;
      color: #6b7280;
    }

    .puestos a {
      color: var(--color-primario);
    }

    .nota {
      color: #6b7280;
      font-size: 0.9rem;
    }
  `
})
export class EspacioDetallePage implements OnInit {
  private readonly api = inject(EspaciosApi);
  private readonly ruta = inject(ActivatedRoute);

  readonly area = signal<EspacioPublico | null>(null);
  readonly error = signal<string | null>(null);

  /** Carga el área indicada en la ruta. */
  ngOnInit(): void {
    const id = this.ruta.snapshot.paramMap.get('id');

    if (!id) {
      this.error.set('El área solicitada no existe.');
      return;
    }

    this.api.obtenerPublico(id).subscribe({
      next: (area) => this.area.set(area),
      error: () => this.error.set('El área solicitada no existe o no está publicada.')
    });
  }

  /** Etiqueta de la categoría del área. */
  categoria(): string {
    return etiquetaCategoria(this.area()!.categoria);
  }

  /** Etiqueta del tipo de aprobación. */
  aprobacion(): string {
    return etiquetaTipoAprobacion(this.area()!.tipoAprobacion);
  }

  /** Etiqueta de la modalidad de préstamo. */
  modalidad(): string {
    return etiquetaModalidad(this.area()!.tipoModalidadPrestamo);
  }

  /** Etiqueta del elemento de préstamo. */
  elemento(): string {
    return etiquetaElemento(this.area()!.tipoElementoPrestamo);
  }

  /** Indica si el área admite reserva autoconfirmada de sus puestos. */
  admitePrestamoDirecto(): boolean {
    const area = this.area();

    if (!area) {
      return false;
    }

    return (
      area.tipoAprobacion === TipoAprobacion.Automatica &&
      area.tipoModalidadPrestamo !== TipoModalidadPrestamo.AreaCompleta
    );
  }

  /** Indica si el área admite la radicación de una solicitud con pre-informe. */
  admiteSolicitud(): boolean {
    const area = this.area();

    if (!area) {
      return false;
    }

    return (
      area.tipoAprobacion !== TipoAprobacion.Automatica &&
      area.tipoModalidadPrestamo === TipoModalidadPrestamo.AreaCompleta
    );
  }
}
