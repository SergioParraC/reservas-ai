import { Component, inject } from '@angular/core';

import { NotificadorErrores } from '../../../core/errores/notificador-errores';

@Component({
  selector: 'app-mensaje-error',
  template: `
    @if (mensaje(); as texto) {
      <p class="mensaje" role="alert">{{ texto }}</p>
    }
  `,
  styles: `
    .mensaje {
      margin: 0.75rem 0 0;
      padding: 0.6rem 0.8rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
      font-size: 0.9rem;
    }
  `
})
export class MensajeError {
  private readonly notificador = inject(NotificadorErrores);

  readonly mensaje = this.notificador.mensaje;
}
