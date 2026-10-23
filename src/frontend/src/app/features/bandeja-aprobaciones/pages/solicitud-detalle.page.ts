import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import {
  etiquetaEstadoSolicitud,
  EstadoSolicitud,
  FiltrosBandeja
} from '../../../core/solicitudes/solicitudes.models';
import { DialogoRechazo } from '../components/dialogo-rechazo';
import { BandejaStore } from '../estado/bandeja.store';

/** Detalle de una solicitud con la descarga del pre-informe y las decisiones del revisor. */
@Component({
  selector: 'app-solicitud-detalle',
  imports: [RouterLink, DatePipe, DialogoRechazo],
  templateUrl: './solicitud-detalle.page.html',
  styles: `
    .volver {
      display: inline-block;
      margin-bottom: 1rem;
      color: var(--color-primario);
      text-decoration: none;
    }

    .detalle {
      padding: 1.25rem 1.5rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.6rem;
      background: #fff;
    }

    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
    }

    h1 {
      margin: 0;
      font-size: 1.25rem;
    }

    .chip {
      padding: 0.15rem 0.6rem;
      border-radius: 999px;
      background: #eff6ff;
      color: #1d4ed8;
      font-size: 0.8rem;
    }

    dl {
      display: grid;
      grid-template-columns: 9rem 1fr;
      gap: 0.45rem 1rem;
      margin: 1rem 0;
    }

    dt {
      font-weight: 600;
      color: #4b5563;
    }

    dd {
      margin: 0;
    }

    .acciones {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
      border-top: 1px solid var(--color-borde);
      padding-top: 1rem;
    }

    button {
      padding: 0.45rem 0.95rem;
      border: 1px solid var(--color-primario);
      border-radius: 0.4rem;
      background: var(--color-primario);
      color: #fff;
      font: inherit;
      cursor: pointer;
    }

    button.secundario {
      background: #fff;
      color: var(--color-primario);
    }

    button.aprobar {
      border-color: #10b981;
      background: #10b981;
    }

    button.peligro {
      border-color: var(--color-error);
      background: var(--color-error);
    }

    button:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .terminal {
      margin: 0.75rem 0 0;
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

    .aviso {
      padding: 0.8rem 1rem;
      border-left: 4px solid #10b981;
      background: #ecfdf5;
      color: #047857;
      border-radius: 0.25rem;
    }
  `
})
export class SolicitudDetallePage implements OnInit {
  private readonly ruta = inject(ActivatedRoute);

  readonly tienda = inject(BandejaStore);
  readonly estadoRadicado = EstadoSolicitud.Radicado;
  readonly estadoRevision = EstadoSolicitud.EnRevision;
  readonly estadoAprobado = EstadoSolicitud.Aprobado;
  readonly estadoRechazado = EstadoSolicitud.Rechazado;

  readonly solicitudId = this.ruta.snapshot.paramMap.get('id') ?? '';
  readonly dialogoAbierto = signal(false);
  readonly procesando = computed(() => this.tienda.procesando() === this.solicitudId);
  readonly solicitud = computed(() => this.tienda.buscar(this.solicitudId));

  private readonly filtros: FiltrosBandeja = { pagina: 1, tamano: 50 };

  /** Carga la bandeja cuando se entra directamente al detalle. */
  ngOnInit(): void {
    if (!this.tienda.pagina()) {
      this.tienda.cargar(this.filtros).subscribe();
    }
  }

  /** Etiqueta legible del estado. */
  etiqueta(estado: EstadoSolicitud): string {
    return etiquetaEstadoSolicitud(estado);
  }

  /** Descarga el pre-informe en una pestaña nueva. */
  abrirPdf(): void {
    this.tienda.preinforme(this.solicitudId).subscribe((url) => {
      if (url) {
        window.open(url, '_blank');
      }
    });
  }

  /** Registra la revisión de la solicitud. */
  tomarEnRevision(): void {
    this.tienda
      .cambiarEstado(this.solicitudId, { estado: EstadoSolicitud.EnRevision }, this.filtros)
      .subscribe();
  }

  /** Aprueba la solicitud y ocupa la franja del área. */
  aprobar(): void {
    this.tienda
      .cambiarEstado(this.solicitudId, { estado: EstadoSolicitud.Aprobado }, this.filtros)
      .subscribe();
  }

  /** Rechaza la solicitud con el motivo validado por el diálogo. */
  rechazar(motivo: string): void {
    this.dialogoAbierto.set(false);
    this.tienda
      .cambiarEstado(this.solicitudId, { estado: EstadoSolicitud.Rechazado, motivo }, this.filtros)
      .subscribe();
  }
}
