import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { SesionStore } from '../../core/auth/sesion.store';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink],
  template: `
    <header class="encabezado">
      <a routerLink="/inicio" class="marca">Reservas de Espacios</a>
      @if (autenticado()) {
        <nav class="navegacion">
          <span class="correo">{{ correo() }}</span>
          <a routerLink="/inicio">Inicio</a>
          <button type="button" (click)="cerrarSesion()">Cerrar sesión</button>
        </nav>
      }
    </header>

    <main class="contenido">
      <router-outlet />
    </main>
  `,
  styles: `
    .encabezado {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
      padding: 1rem 1.5rem;
      background: #fff;
      border-bottom: 1px solid var(--color-borde);
    }

    .marca {
      font-weight: 700;
      color: var(--color-primario);
      text-decoration: none;
    }

    .navegacion {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .correo {
      color: #6b7280;
      font-size: 0.9rem;
    }

    .contenido {
      padding: 2rem 1.5rem;
      max-width: 60rem;
      margin: 0 auto;
    }
  `
})
export class Shell {
  private readonly auth = inject(AuthService);
  private readonly sesion = inject(SesionStore);

  readonly autenticado = this.sesion.estaAutenticado;
  readonly correo = computed(() => this.sesion.perfil()?.correoInstitucional ?? '');

  cerrarSesion(): void {
    this.auth.logout();
  }
}
