import { computed, Injectable, signal } from '@angular/core';

import { PerfilUsuario, Sesion } from './auth.models';

const CLAVE_ALMACENAMIENTO = 'reservas.sesion';

/**
 * Estado de la sesión basado en Signals. Persiste el par token/perfil en
 * `localStorage` para sobrevivir a una recarga de la aplicación.
 */
@Injectable({ providedIn: 'root' })
export class SesionStore {
  private readonly estado = signal<Sesion | null>(this.leerAlmacenamiento());

  /** Sesión actual o `null` si no hay. */
  readonly sesion = this.estado.asReadonly();

  /** Perfil del portador. */
  readonly perfil = computed<PerfilUsuario | null>(() => this.estado()?.perfil ?? null);

  /** Roles concedidos al portador. */
  readonly roles = computed<readonly string[]>(() => this.estado()?.perfil.roles ?? []);

  /** Token vigente o `null` si la sesión expiró o no existe. */
  readonly token = computed<string | null>(() => (this.vigente() ? this.estado()?.token ?? null : null));

  /** Indica si hay una sesión con token vigente. */
  readonly estaAutenticado = computed(() => this.vigente());

  /** Guarda una sesión completa y la persiste. */
  guardar(sesion: Sesion): void {
    localStorage.setItem(CLAVE_ALMACENAMIENTO, JSON.stringify(sesion));
    this.estado.set(sesion);
  }

  /** Completa o reemplaza el perfil de la sesión actual (por ejemplo tras `/api/auth/me`). */
  guardarPerfil(perfil: PerfilUsuario): void {
    const actual = this.estado();

    if (!actual) {
      return;
    }

    this.guardar({ ...actual, perfil });
  }

  /** Elimina la sesión del estado y del almacenamiento. */
  limpiar(): void {
    localStorage.removeItem(CLAVE_ALMACENAMIENTO);
    this.estado.set(null);
  }

  /** Indica si la sesión vigente incluye el rol indicado. */
  tieneRol(rol: string): boolean {
    return this.roles().includes(rol);
  }

  private vigente(): boolean {
    const sesion = this.estado();

    if (!sesion) {
      return false;
    }

    const expira = Date.parse(sesion.expiraEn);

    return Number.isNaN(expira) ? true : expira > Date.now();
  }

  private leerAlmacenamiento(): Sesion | null {
    const guardada = localStorage.getItem(CLAVE_ALMACENAMIENTO);

    if (!guardada) {
      return null;
    }

    try {
      return JSON.parse(guardada) as Sesion;
    } catch {
      localStorage.removeItem(CLAVE_ALMACENAMIENTO);
      return null;
    }
  }
}
