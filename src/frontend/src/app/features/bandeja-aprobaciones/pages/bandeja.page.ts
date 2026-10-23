import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import {
  etiquetaEstadoSolicitud,
  EstadoSolicitud,
  ESTADOS_SOLICITUD,
  FiltrosBandeja
} from '../../../core/solicitudes/solicitudes.models';
import { EspacioCatalogo } from '../../areas/models/espacio.models';
import { EspaciosApi } from '../../areas/services/espacios.api';
import { BandejaStore } from '../estado/bandeja.store';

/** Bandeja única de aprobaciones con filtros por estado, área y rango de fechas. */
@Component({
  selector: 'app-bandeja',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './bandeja.page.html',
  styleUrl: './bandeja.page.css'
})
export class BandejaPage implements OnInit {
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;
  private readonly espaciosApi = inject(EspaciosApi);

  readonly tienda = inject(BandejaStore);
  readonly estados = ESTADOS_SOLICITUD;
  readonly areas = signal<EspacioCatalogo[]>([]);

  readonly filtros = this.formularioReactivo.group({
    estado: '',
    espacioId: '',
    desde: '',
    hasta: ''
  });

  /** Carga las áreas y la primera página de la bandeja. */
  ngOnInit(): void {
    this.espaciosApi.listarCatalogo({ pagina: 1, tamano: 50 }).subscribe({
      next: (pagina) => this.areas.set(pagina.elementos),
      error: () => this.areas.set([])
    });

    this.cargar(1);
  }

  /** Aplica los filtros desde la primera página. */
  aplicar(): void {
    this.cargar(1);
  }

  /** Navega a la página indicada. */
  ir(pagina: number): void {
    if (pagina < 1 || pagina > this.tienda.totalPaginas()) {
      return;
    }

    this.cargar(pagina);
  }

  /** Etiqueta legible del estado. */
  etiqueta(estado: EstadoSolicitud): string {
    return etiquetaEstadoSolicitud(estado);
  }

  /** Filtros actuales normalizados. */
  filtrosActuales(pagina: number): FiltrosBandeja {
    const valores = this.filtros.getRawValue();

    return {
      estado: valores.estado ? (Number(valores.estado) as EstadoSolicitud) : null,
      espacioId: valores.espacioId || null,
      desde: valores.desde || null,
      hasta: valores.hasta || null,
      pagina,
      tamano: 10
    };
  }

  private cargar(pagina: number): void {
    this.tienda.cargar(this.filtrosActuales(pagina)).subscribe();
  }
}
