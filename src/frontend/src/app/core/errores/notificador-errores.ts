import { Injectable, signal } from '@angular/core';

/**
 * Estado global de mensajes de error derivados del contrato RFC 7807.
 */
@Injectable({ providedIn: 'root' })
export class NotificadorErrores {
  private readonly estadoMensaje = signal<string | null>(null);
  private readonly estadoSesionExpirada = signal(false);

  /** Último mensaje de error comprensible para el usuario. */
  readonly mensaje = this.estadoMensaje.asReadonly();

  /** Indica que la sesión se perdió y el usuario fue redirigido al acceso. */
  readonly sesionExpirada = this.estadoSesionExpirada.asReadonly();

  /** Registra un mensaje de error. */
  error(mensaje: string): void {
    this.estadoMensaje.set(mensaje);
  }

  /** Marca la sesión como expirada. */
  marcarSesionExpirada(): void {
    this.estadoSesionExpirada.set(true);
    this.estadoMensaje.set('La sesión expiró. Vuelva a iniciar sesión.');
  }

  /** Limpia los mensajes. */
  limpiar(): void {
    this.estadoMensaje.set(null);
    this.estadoSesionExpirada.set(false);
  }
}
