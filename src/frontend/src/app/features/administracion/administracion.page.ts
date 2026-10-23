import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { environment } from '../../../environments/environment';

/** Panel de administración con los accesos a cada módulo y la verificación protegida por rol. */
@Component({
  selector: 'app-administracion',
  imports: [RouterLink],
  template: `
    <h1>Administración</h1>
    <p class="detalle">
      Módulos disponibles para identidades con el rol <strong>Administrativo</strong>.
    </p>

    <nav class="modulos">
      <a routerLink="/administracion/configuracion">
        <strong>Calendario institucional</strong>
        <span>Periodo académico vigente y franjas no operativas.</span>
      </a>
      <a routerLink="/administracion/espacios">
        <strong>Espacios</strong>
        <span>Áreas, puestos, modalidad de préstamo, fotografías y QR.</span>
      </a>
      <a routerLink="/administracion/usuarios">
        <strong>Usuarios</strong>
        <span>Cuentas, roles y vinculación institucional.</span>
      </a>
      <a routerLink="/bandeja-aprobaciones">
        <strong>Bandeja de aprobaciones</strong>
        <span>Solicitudes con pre-informe de cualquier área.</span>
      </a>
    </nav>

    @if (mensaje(); as texto) {
      <p class="resultado" role="status">{{ texto }}</p>
    }

    <button type="button" (click)="verificar()" [disabled]="consultando()">
      {{ consultando() ? 'Consultando…' : 'Consultar endpoint' }}
    </button>
  `,
  styles: `
    .detalle {
      color: #4b5563;
    }

    .modulos {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
      gap: 1rem;
      margin: 1.25rem 0;
    }

    .modulos a {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      padding: 1rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.5rem;
      background: #fff;
      color: inherit;
      text-decoration: none;
    }

    .modulos a:hover {
      border-color: var(--color-primario);
    }

    .modulos strong {
      color: var(--color-primario);
    }

    .modulos span {
      font-size: 0.85rem;
      color: #6b7280;
    }

    .resultado {
      margin: 1rem 0;
      padding: 0.6rem 0.8rem;
      border-left: 4px solid var(--color-primario);
      background: #eff6ff;
      border-radius: 0.25rem;
    }
  `
})
export class AdministracionPage {
  private readonly http = inject(HttpClient);

  readonly consultando = signal(false);
  readonly mensaje = signal<string | null>(null);

  /** Invoca el endpoint administrativo; un `403` lo publica el interceptor de problemas. */
  verificar(): void {
    this.consultando.set(true);

    this.http.get<{ mensaje: string }>(`${environment.apiBaseUrl}/api/administracion/verificacion`).subscribe({
      next: (respuesta) => {
        this.consultando.set(false);
        this.mensaje.set(respuesta.mensaje);
      },
      error: () => this.consultando.set(false)
    });
  }
}
