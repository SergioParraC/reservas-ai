import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';

import { environment } from '../../../environments/environment';

/** Endpoint administrativo de verificación protegido por rol. */
@Component({
  selector: 'app-administracion',
  template: `
    <h1>Verificación administrativa</h1>
    <p class="detalle">Este endpoint solo responde a identidades con el rol <strong>Administrativo</strong>.</p>

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
