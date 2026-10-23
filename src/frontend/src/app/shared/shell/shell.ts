import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

import { ROLES } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { SesionStore } from '../../core/auth/sesion.store';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink],
  template: `
    <header class="encabezado">
      <a routerLink="/inicio" class="marca">Reservas de Espacios</a>

      <nav class="navegacion">
        <a routerLink="/catalogo">Catálogo</a>

        @if (autenticado()) {
          <a routerLink="/inicio">Inicio</a>
          <a routerLink="/mis-reservas">Mis reservas</a>
          @if (esDocente()) {
            <a routerLink="/reservas-semestrales/mis-series">Mis series</a>
          }
          @if (puedeAprobar()) {
            <a routerLink="/bandeja-aprobaciones">Bandeja</a>
          }
          @if (esAdministrativo()) {
            <a routerLink="/administracion/configuracion">Calendario</a>
            <a routerLink="/administracion/espacios">Espacios</a>
            <a routerLink="/administracion/usuarios">Usuarios</a>
          }
          <a routerLink="/perfil">Mi cuenta</a>
          <span class="correo">{{ correo() }}</span>
          <span class="origen">{{ origen() === 'entra' ? 'Acceso institucional' : 'Acceso local' }}</span>
          <button type="button" (click)="cerrarSesion()">Cerrar sesión</button>
        } @else {
          <a routerLink="/login">Acceder</a>
          <a routerLink="/registro">Registrarse</a>
        }
      </nav>
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
      flex-wrap: wrap;
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
      flex-wrap: wrap;
      gap: 0.9rem;
    }

    .navegacion a {
      color: #374151;
      text-decoration: none;
      font-size: 0.9rem;
    }

    .correo {
      color: #6b7280;
      font-size: 0.85rem;
    }

    .origen {
      padding: 0.15rem 0.5rem;
      border-radius: 999px;
      background: #eff6ff;
      color: #1d4ed8;
      font-size: 0.75rem;
    }

    .contenido {
      padding: 2rem 1.5rem;
      max-width: 70rem;
      margin: 0 auto;
    }
  `
})
export class Shell {
  private readonly auth = inject(AuthService);
  private readonly sesion = inject(SesionStore);

  readonly autenticado = this.sesion.estaAutenticado;
  readonly correo = computed(() => this.sesion.perfil()?.correoInstitucional ?? '');
  readonly origen = this.sesion.origen;
  readonly esAdministrativo = computed(() => this.sesion.roles().includes(ROLES.administrativo));
  readonly esDocente = computed(() => this.sesion.roles().includes(ROLES.docente));
  readonly puedeAprobar = computed(() => this.esDocente() || this.esAdministrativo());

  cerrarSesion(): void {
    this.auth.logout();
  }
}
