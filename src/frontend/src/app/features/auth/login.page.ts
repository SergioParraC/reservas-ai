import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { EntraService } from '../../core/auth/entra.service';
import { NotificadorErrores } from '../../core/errores/notificador-errores';
import { MensajeError } from '../../shared/ui/mensaje-error/mensaje-error';

/** Pantalla de acceso local y con el correo institucional de Entra ID. */
@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, MensajeError],
  template: `
    <section class="tarjeta">
      <h1>Iniciar sesión</h1>
      <p class="ayuda">Acceso con sus credenciales locales o con el correo de la universidad.</p>

      @if (registrado()) {
        <p class="exito" role="status">Cuenta creada. Inicie sesión con su contraseña.</p>
      }

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

      <div class="separador"><span>o</span></div>

      <button type="button" class="secundario" (click)="accederConEntra()" [disabled]="entrandoEntra()">
        {{ entrandoEntra() ? 'Redirigiendo…' : 'Correo de la universidad' }}
      </button>

      <p class="registro">
        ¿No tiene cuenta? <a routerLink="/registro">Registrarse</a>
      </p>

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

    .secundario {
      margin-top: 0;
      background: #fff;
      color: var(--color-primario);
      border: 1px solid var(--color-primario);
    }

    .separador {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      color: #9ca3af;
      font-size: 0.8rem;
      margin: 0.5rem 0;
    }

    .separador::before,
    .separador::after {
      content: '';
      flex: 1;
      border-top: 1px solid var(--color-borde);
    }

    .registro {
      margin: 1rem 0 0;
      text-align: center;
      font-size: 0.9rem;
    }

    .exito {
      margin: 0;
      padding: 0.6rem 0.8rem;
      border-left: 4px solid #047857;
      background: #ecfdf5;
      color: #065f46;
      border-radius: 0.25rem;
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
  private readonly entra = inject(EntraService);
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);
  private readonly notificador = inject(NotificadorErrores);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly enviando = signal(false);
  readonly entrandoEntra = signal(false);
  readonly registrado = signal(this.ruta.snapshot.queryParamMap.get('registrado') === '1');
  readonly formulario = this.formularioReactivo.group({
    correoInstitucional: [this.ruta.snapshot.queryParamMap.get('correo') ?? '', [Validators.required, Validators.email]],
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
        const destino = this.destino();
        void this.router.navigateByUrl(destino);
      },
      error: () => {
        // El interceptor de problemas ya publicó el mensaje comprensible.
        this.enviando.set(false);
      }
    });
  }

  /** Inicia el flujo OIDC con el correo de la universidad. */
  async accederConEntra(): Promise<void> {
    this.entrandoEntra.set(true);
    this.notificador.limpiar();

    try {
      await this.entra.loginRedirect(this.destino());
    } catch {
      this.entrandoEntra.set(false);
      this.notificador.error('No fue posible iniciar el acceso con el correo de la universidad.');
    }
  }

  private destino(): string {
    return this.ruta.snapshot.queryParamMap.get('returnUrl') ?? '/inicio';
  }
}
