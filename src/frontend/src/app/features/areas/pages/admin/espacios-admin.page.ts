import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { EspacioCard } from '../../components/espacio-card';
import { EspaciosAdminStore } from '../../estado/espacios-admin.store';
import { CATEGORIAS, CategoriaEspacio, ESTADOS, EstadoUnidad } from '../../models/espacio.models';

/** Mantenimiento administrativo de áreas: listado, filtros y acciones. */
@Component({
  selector: 'app-espacios-admin',
  imports: [ReactiveFormsModule, RouterLink, EspacioCard],
  templateUrl: './espacios-admin.page.html',
  styleUrl: './espacios-admin.page.css'
})
export class EspaciosAdminPage implements OnInit {
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;
  readonly tienda = inject(EspaciosAdminStore);
  readonly categorias = CATEGORIAS;
  readonly estados = ESTADOS;

  readonly filtros = this.formularioReactivo.group({
    q: '',
    categoria: '',
    estado: ''
  });

  /** Carga la primera página al entrar. */
  ngOnInit(): void {
    this.tienda.cargar({ pagina: 1 });
  }

  /** Aplica los filtros del formulario. */
  filtrar(): void {
    const valores = this.filtros.getRawValue();
    const categoria = Number(valores.categoria);
    const estado = Number(valores.estado);

    this.tienda.filtrar({
      q: valores.q.trim() || null,
      categoria: Number.isFinite(categoria) && categoria > 0 ? (categoria as CategoriaEspacio) : null,
      estado: Number.isFinite(estado) && estado > 0 ? (estado as EstadoUnidad) : null
    });
  }

  /** Retrocede una página. */
  anterior(): void {
    this.tienda.irAPagina((this.tienda.resultado()?.pagina ?? 1) - 1);
  }

  /** Avanza una página. */
  siguiente(): void {
    this.tienda.irAPagina((this.tienda.resultado()?.pagina ?? 1) + 1);
  }

  /** Da de baja lógica el área. */
  desactivar(id: string): void {
    this.tienda.desactivar(id);
  }
}
