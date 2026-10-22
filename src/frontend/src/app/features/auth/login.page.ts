import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { NotificadorErrores } from '../../core/errores/notificador-errores';
import { MensajeError } from '../../shared/ui/mensaje-error/mensaje-error';

/** Pantalla de acceso con formulario reactivo y errores del contrato RFC 7807. */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, MensajeError],
  template: `
    <section class="tarjeta">
      <h1>Iniciar sesión</h1>
      <p class="ayuda">Acceso con credenciales institucionales para el personal administrativo.</p>

      <form [formGroup]="formulario" (ngSubmit)="enviar()" novalidate>
        <label for="correo">Correo institucional</label>
        <input id="correo" type="email" formControlName="correoInstitucional" autocomplete="username" />
        @if (correoInvalido()) {
          <p class="validacion">Ingrese un correo institucional válido.</p>
        }

        <label for="contrasena">Contraseña</label>
        <input id="contrasena" type="password" formControlName="contrasena" autocomplete="current-password" />
        @if (contrasenaInvalida()) {
          <p class="validacion">La contraseña es obligatoria.</p>
        }

        <button type="submit" [disabled]="enviando()">
          {{ enviando() ? 'Ingresando…' : 'Ingresar' }}
        </button>
      </form>

      <app-mensaje-error />
    </section>
  `,
  styles: `
    .tarjeta {
      max-width: 26rem;
      margin: 2rem auto;
      padding: 2rem;
      background: #fff;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .ayuda {
      margin: 0 0 1rem;
      color: #6b7280;
      font-size: 0.9rem;
    }

    form {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
    }

    label {
      font-weight: 600;
      font-size: 0.9rem;
    }

    button {
      margin-top: 1rem;
    }

    .validacion {
      margin: 0;
      color: var(--color-error);
      font-size: 0.85rem;
    }
  `
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);
  private readonly notificador = inject(NotificadorErrores);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly enviando = signal(false);
  readonly formulario = this.formularioReactivo.group({
    correoInstitucional: ['', [Validators.required, Validators.email]],
    contrasena: ['', [Validators.required]]
  });

  constructor() {
    this.notificador.limpiar();
  }

  /** Indica si el correo debe mostrar su mensaje de validación. */
  correoInvalido(): boolean {
    const control = this.formulario.controls.correoInstitucional;
    return control.touched && control.invalid;
  }

  /** Indica si la contraseña debe mostrar su mensaje de validación. */
  contrasenaInvalida(): boolean {
    const control = this.formulario.controls.contrasena;
    return control.touched && control.invalid;
  }

  /** Envía las credenciales y navega al destino original. */
  enviar(): void {
    if (this.formulario.invalid) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.notificador.limpiar();

    this.auth.login(this.formulario.getRawValue()).subscribe({
      next: () => {
        this.enviando.set(false);
        const destino = this.ruta.snapshot.queryParamMap.get('returnUrl') ?? '/inicio';
        void this.router.navigateByUrl(destino);
      },
      error: () => {
        // El interceptor de problemas ya publicó el mensaje comprensible.
        this.enviando.set(false);
      }
    });
  }
}
