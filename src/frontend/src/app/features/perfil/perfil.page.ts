import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';
import { EntraService } from '../../core/auth/entra.service';
import { SesionStore } from '../../core/auth/sesion.store';
import { leerProblema } from '../../core/interceptors/problem-details.interceptor';
import { UsuariosApi } from '../../core/usuarios/usuarios.api';
import { MensajeError } from '../../shared/ui/mensaje-error/mensaje-error';

/** Configuración de la cuenta: estado del vínculo y alta/baja de la identidad institucional. */
@Component({
  selector: 'app-perfil',
  imports: [MensajeError],
  template: `
    <section class="tarjeta">
      <h1>Mi cuenta</h1>

      @if (perfil(); as datos) {
        <dl>
          <dt>Nombre</dt>
          <dd>{{ datos.nombreCompleto }}</dd>
          <dt>Correo</dt>
          <dd>{{ datos.correoInstitucional }}</dd>
          <dt>Rol</dt>
          <dd>{{ datos.roles.join(', ') || 'Sin rol asignado' }}</dd>
          <dt>Acceso</dt>
          <dd>{{ origen() === 'entra' ? 'Correo de la universidad' : 'Contraseña local' }}</dd>
        </dl>

        <p class="estado" [class.vinculado]="vinculado()" role="status">
          {{ vinculado() ? 'Cuenta vinculada con Entra ID' : 'Cuenta sin vínculo institucional' }}
        </p>

        @if (origen() === 'entra') {
          <p class="ayuda">La sesión institucional ya está vinculada a esta cuenta.</p>
        } @else if (!vinculado()) {
          <button type="button" (click)="vincular()" [disabled]="ocupado()">
            {{ ocupado() ? 'Vinculando…' : 'Vincular cuenta de la universidad' }}
          </button>
        } @else {
          <button type="button" class="secundario" (click)="desvincular()" [disabled]="ocupado()">
            {{ ocupado() ? 'Desvinculando…' : 'Desvincular' }}
          </button>
        }
      }

      @if (mensaje(); as texto) {
        <p class="mensaje" role="status">{{ texto }}</p>
      }

      @if (conflicto(); as texto) {
        <p class="conflicto" role="alert">{{ texto }}</p>
      }

      <app-mensaje-error />
    </section>
  `,
  styles: `
    .tarjeta {
      max-width: 34rem;
      margin: 2rem auto;
      padding: 2rem;
      background: #fff;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
    }

    dl {
      display: grid;
      grid-template-columns: 8rem 1fr;
      gap: 0.4rem 1rem;
      margin: 0 0 1.25rem;
    }

    dt {
      font-weight: 600;
      color: #4b5563;
    }

    dd {
      margin: 0;
    }

    .estado {
      margin: 0 0 1rem;
      padding: 0.6rem 0.8rem;
      border-left: 4px solid #b45309;
      background: #fffbeb;
      border-radius: 0.25rem;
    }

    .estado.vinculado {
      border-left-color: #047857;
      background: #ecfdf5;
      color: #065f46;
    }

    .ayuda {
      margin: 0 0 1rem;
      color: #6b7280;
      font-size: 0.9rem;
    }

    .secundario {
      background: #fff;
      color: var(--color-error);
      border: 1px solid var(--color-error);
    }

    .mensaje {
      margin-top: 1rem;
      padding: 0.6rem 0.8rem;
      border-left: 4px solid var(--color-primario);
      background: #eff6ff;
      border-radius: 0.25rem;
    }

    .conflicto {
      margin-top: 1rem;
      padding: 0.6rem 0.8rem;
      border-left: 4px solid #b45309;
      background: #fffbeb;
      border-radius: 0.25rem;
    }
  `
})
export class PerfilPage {
  private readonly sesion = inject(SesionStore);
  private readonly auth = inject(AuthService);
  private readonly entra = inject(EntraService);
  private readonly api = inject(UsuariosApi);

  readonly perfil = this.sesion.perfil;
  readonly origen = this.sesion.origen;
  readonly ocupado = signal(false);
  readonly mensaje = signal<string | null>(null);
  readonly conflicto = signal<string | null>(null);
  private readonly estadoVinculo = signal<boolean | null>(null);
  readonly vinculado = computed(
    () => this.estadoVinculo() ?? this.perfil()?.vinculado ?? this.origen() === 'entra'
  );

  /** Obtiene el token de Entra y vincula la cuenta local. */
  async vincular(): Promise<void> {
    this.iniciar();

    try {
      const token = await this.entra.adquirirToken();

      if (!token) {
        this.ocupado.set(false);
        this.mensaje.set('Inicie sesión con el correo de la universidad para vincular su cuenta.');
        return;
      }

      this.api.vincular(token).subscribe({
        next: (estado) => {
          this.ocupado.set(false);
          this.estadoVinculo.set(estado.vinculado);
          this.mensaje.set('Cuenta vinculada. Ya puede entrar con el correo de la universidad.');
          this.refrescarPerfil();
        },
        error: (error: unknown) => this.fallar(error)
      });
    } catch {
      this.fallar(new Error('Sin token'));
    }
  }

  /** Retira el vínculo federado y conserva la contraseña local. */
  desvincular(): void {
    this.iniciar();

    this.api.desvincular().subscribe({
      next: () => {
        this.ocupado.set(false);
        this.estadoVinculo.set(false);
        this.mensaje.set('Vínculo retirado. Puede seguir entrando con su contraseña local.');
        this.refrescarPerfil();
      },
      error: (error: unknown) => this.fallar(error)
    });
  }

  private iniciar(): void {
    this.ocupado.set(true);
    this.mensaje.set(null);
    this.conflicto.set(null);
  }

  private fallar(error: unknown): void {
    this.ocupado.set(false);

    if (!(error instanceof HttpErrorResponse)) {
      this.mensaje.set('No fue posible completar la operación con Entra ID.');
      return;
    }

    const problema = leerProblema(error);
    this.conflicto.set(problema?.detail ?? 'No fue posible completar la operación.');
  }

  private refrescarPerfil(): void {
    this.auth.cargarPerfil().subscribe({
      next: (perfil) => this.estadoVinculo.set(perfil.vinculado ?? this.origen() === 'entra'),
      error: () => undefined
    });
  }
}
