import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';

import { ROLES } from '../../../core/auth/auth.models';
import { SesionStore } from '../../../core/auth/sesion.store';
import { leerProblema } from '../../../core/interceptors/problem-details.interceptor';
import { UsuariosApi } from '../../../core/usuarios/usuarios.api';
import {
  FiltroUsuarios,
  PaginaUsuarios,
  ROLES_ASIGNABLES,
  UsuarioAdministrativo
} from '../../../core/usuarios/usuarios.models';

/** Consola administrativa de cuentas: listado, filtros y cambio de rol. */
@Component({
  selector: 'app-usuarios-admin',
  imports: [ReactiveFormsModule],
  templateUrl: './usuarios-admin.page.html',
  styleUrl: './usuarios-admin.page.css'
})
export class UsuariosAdminPage {
  private readonly sesion = inject(SesionStore);
  private readonly api = inject(UsuariosApi);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly rolesAsignables = ROLES_ASIGNABLES;
  readonly esAdministrativo = computed(() => this.sesion.roles().includes(ROLES.administrativo));
  readonly cargando = signal(false);
  readonly cambiando = signal(false);
  readonly conflicto = signal<string | null>(null);
  readonly avisoRenovacion = signal<string | null>(null);
  readonly pagina = signal<PaginaUsuarios | null>(null);
  readonly cambioPendiente = signal<{ usuario: UsuarioAdministrativo; rol: string } | null>(null);

  readonly filtros = this.formularioReactivo.group({
    q: '',
    rol: '',
    vinculado: 'todos'
  });

  constructor() {
    if (this.esAdministrativo()) {
      this.cargar(1);
    }
  }

  /** Ejecuta la consulta con los filtros del formulario. */
  aplicar(): void {
    this.cargar(1);
  }

  /** Carga una página del listado administrativo. */
  cargar(numeroPagina: number): void {
    this.cargando.set(true);
    this.conflicto.set(null);

    const valores = this.filtros.getRawValue();
    const filtro: FiltroUsuarios = {
      q: valores.q.trim() || null,
      rol: valores.rol || null,
      vinculado: valores.vinculado === 'si' ? true : valores.vinculado === 'no' ? false : null,
      pagina: numeroPagina,
      tamano: 10
    };

    this.api.listar(filtro).subscribe({
      next: (pagina) => {
        this.cargando.set(false);
        this.pagina.set(pagina);
      },
      error: (error: unknown) => {
        this.cargando.set(false);
        this.conflicto.set(this.detalle(error) ?? 'No fue posible cargar las cuentas.');
      }
    });
  }

  /** Página actual del listado. */
  paginaActual(): number {
    return this.pagina()?.pagina ?? 1;
  }

  /** Total de páginas disponibles. */
  totalPaginas(): number {
    const resultado = this.pagina();

    return resultado ? Math.max(1, Math.ceil(resultado.total / resultado.tamano)) : 1;
  }

  /** Navega a la página anterior. */
  paginaAnterior(): void {
    if (this.paginaActual() > 1) {
      this.cargar(this.paginaActual() - 1);
    }
  }

  /** Navega a la página siguiente. */
  paginaSiguiente(): void {
    if (this.paginaActual() < this.totalPaginas()) {
      this.cargar(this.paginaActual() + 1);
    }
  }

  /** Registra la selección de un rol y pide confirmación explícita. */
  solicitarCambio(usuario: UsuarioAdministrativo, rol: string): void {
    if (rol === usuario.rol) {
      return;
    }

    this.cambioPendiente.set({ usuario, rol });
  }

  /** Cancela el cambio pendiente y devuelve el select a su valor previo. */
  cancelarCambio(): void {
    this.cambioPendiente.set(null);
  }

  /** Aplica el cambio de rol y advierte la necesidad de renovar la sesión. */
  confirmarCambio(): void {
    const cambio = this.cambioPendiente();

    if (!cambio) {
      return;
    }

    this.cambiando.set(true);
    this.conflicto.set(null);

    this.api.cambiarRol(cambio.usuario.id, cambio.rol).subscribe({
      next: (resultado) => {
        this.cambiando.set(false);
        this.cambioPendiente.set(null);
        this.avisoRenovacion.set(
          `Rol actualizado a ${resultado.rol}. La cuenta debe renovar su sesión para aplicar el cambio.`
        );
        this.cargar(this.paginaActual());
      },
      error: (error: unknown) => {
        this.cambiando.set(false);
        this.cambioPendiente.set(null);
        this.conflicto.set(this.detalle(error) ?? 'No fue posible actualizar el rol.');
      }
    });
  }

  private detalle(error: unknown): string | null {
    return error instanceof HttpErrorResponse ? leerProblema(error)?.detail ?? null : null;
  }
}
