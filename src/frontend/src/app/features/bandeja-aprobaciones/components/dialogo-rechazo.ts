import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

/** Diálogo de rechazo que exige un motivo de al menos diez caracteres. */
@Component({
  selector: 'app-dialogo-rechazo',
  imports: [FormsModule],
  template: `
    @if (abierto()) {
      <div class="fondo" role="presentation">
        <section class="dialogo" role="dialog" aria-modal="true" aria-labelledby="titulo-rechazo">
          <h2 id="titulo-rechazo">Rechazar solicitud</h2>
          <p class="ayuda">El motivo es obligatorio, visible para el solicitante y se incluye en el correo.</p>

          <label>
            Motivo
            <textarea
              rows="4"
              maxlength="500"
              [(ngModel)]="motivo"
              placeholder="Explique por qué se rechaza la solicitud…"
            ></textarea>
          </label>

          @if (error()) {
            <p class="error" role="alert">{{ error() }}</p>
          }

          <div class="acciones">
            <button type="button" class="secundario" (click)="cancelar.emit()">Cancelar</button>
            <button type="button" class="peligro" (click)="confirmarRechazo()">Rechazar solicitud</button>
          </div>
        </section>
      </div>
    }
  `,
  styles: `
    .fondo {
      position: fixed;
      inset: 0;
      display: grid;
      place-items: center;
      padding: 1rem;
      background: rgba(17, 24, 39, 0.45);
      z-index: 40;
    }

    .dialogo {
      width: min(30rem, 100%);
      padding: 1.25rem;
      border-radius: 0.6rem;
      background: #fff;
      box-shadow: 0 20px 45px rgba(17, 24, 39, 0.25);
    }

    h2 {
      margin: 0 0 0.35rem;
      font-size: 1.1rem;
    }

    .ayuda {
      margin: 0 0 0.9rem;
      font-size: 0.85rem;
      color: #6b7280;
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      font-size: 0.9rem;
      color: #374151;
    }

    textarea {
      padding: 0.5rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      font: inherit;
      resize: vertical;
    }

    .error {
      margin: 0.6rem 0 0;
      color: var(--color-error);
      font-size: 0.85rem;
    }

    .acciones {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      margin-top: 1rem;
    }

    button {
      padding: 0.45rem 0.9rem;
      border-radius: 0.4rem;
      border: 1px solid var(--color-borde);
      background: #fff;
      font: inherit;
      cursor: pointer;
    }

    button.peligro {
      border-color: var(--color-error);
      background: var(--color-error);
      color: #fff;
    }
  `
})
export class DialogoRechazo {
  readonly abierto = input(false);
  readonly confirmar = output<string>();
  readonly cancelar = output<void>();

  readonly motivo = signal('');
  readonly error = signal<string | null>(null);

  /** Valida la longitud mínima y emite el motivo. */
  confirmarRechazo(): void {
    const texto = this.motivo().trim();

    if (texto.length < 10) {
      this.error.set('El motivo debe tener al menos 10 caracteres.');
      return;
    }

    this.error.set(null);
    this.confirmar.emit(texto);
    this.motivo.set('');
  }
}
