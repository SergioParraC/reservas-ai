import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { NotificadorErrores } from '../../core/errores/notificador-errores';
import { leerProblema } from '../../core/interceptors/problem-details.interceptor';
import { UsuariosApi } from '../../core/usuarios/usuarios.api';
import { RegistroUsuarioRespuesta } from '../../core/usuarios/usuarios.models';
import { MensajeError } from '../../shared/ui/mensaje-error/mensaje-error';

/** Política de contraseña espejo de la configurada en ASP.NET Core Identity. */
const PATRON_CONTRASENA = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{10,}$/;

/** Formulario público de autorregistro con confirmación de contraseña. */
@Component({
  selector: 'app-registro',
  imports: [ReactiveFormsModule, RouterLink, MensajeError],
  template: `
    <section class="tarjeta">
      <h1>Crear cuenta</h1>
      <p class="ayuda">
        Use su correo institucional. La primera cuenta registrada recibe el rol administrativo;
        las siguientes nacen como estudiantes.
      </p>

      @if (resultado(); as creada) {
        <div class="exito" role="status">
          <p>Cuenta creada con el rol <strong>{{ creada.rol }}</strong>.</p>
          <a routerLink="/login" [queryParams]="{ correo: creada.correoInstitucional, registrado: 1 }">
            Ir al inicio de sesión
          </a>
        </div>
      }

      @if (conflicto(); as mensaje) {
        <p class="conflicto" role="alert">{{ mensaje }}</p>
      }

      <form [formGroup]="formulario" (ngSubmit)="enviar()" novalidate>
        <label for="nombre">Nombre completo</label>
        <input id="nombre" formControlName="nombreCompleto" autocomplete="name" />
        @if (errorDe('nombreCompleto') ?? nombreInvalido()) {
          <p class="validacion">{{ errorDe('nombreCompleto') ?? 'El nombre completo es obligatorio.' }}</p>
        }

        <label for="correo">Correo institucional</label>
        <input id="correo" type="email" formControlName="correoInstitucional" autocomplete="username" />
        @if (errorDe('correoInstitucional') ?? correoInvalido()) {
          <p class="validacion">
            {{ errorDe('correoInstitucional') ?? 'Ingrese un correo institucional válido.' }}
          </p>
        }

        <label for="contrasena">Contraseña</label>
        <input id="contrasena" type="password" formControlName="contrasena" autocomplete="new-password" />
        @if (errorDe('contrasena') ?? contrasenaInvalida()) {
          <p class="validacion">
            {{ errorDe('contrasena') ?? 'Use al menos 10 caracteres con mayúscula, minúscula, número y símbolo.' }}
          </p>
        }

        <label for="confirmacion">Repetir contraseña</label>
        <input id="confirmacion" type="password" formControlName="confirmacion" autocomplete="new-password" />
        @if (confirmacionInvalida()) {
          <p class="validacion">Las contraseñas no coinciden.</p>
        }

        <button type="submit" [disabled]="enviando()">
          {{ enviando() ? 'Registrando…' : 'Crear cuenta' }}
        </button>
      </form>

      <p class="acceso">¿Ya tiene cuenta? <a routerLink="/login">Iniciar sesión</a></p>

      <app-mensaje-error />
    </section>
  `,
  styles: `
    .tarjeta {
      max-width: 28rem;
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

    .validacion {
      margin: 0;
      color: var(--color-error);
      font-size: 0.85rem;
    }

    .exito {
      padding: 0.8rem;
      border-left: 4px solid #047857;
      background: #ecfdf5;
      color: #065f46;
      border-radius: 0.25rem;
    }

    .exito p {
      margin: 0 0 0.4rem;
    }

    .conflicto {
      margin: 0;
      padding: 0.6rem 0.8rem;
      border-left: 4px solid #b45309;
      background: #fffbeb;
      color: #92400e;
      border-radius: 0.25rem;
    }

    .acceso {
      margin: 1rem 0 0;
      text-align: center;
      font-size: 0.9rem;
    }
  `
})
export class RegistroPage {
  private readonly api = inject(UsuariosApi);
  private readonly router = inject(Router);
  private readonly notificador = inject(NotificadorErrores);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly enviando = signal(false);
  readonly resultado = signal<RegistroUsuarioRespuesta | null>(null);
  readonly conflicto = signal<string | null>(null);
  readonly erroresCampo = signal<Record<string, string[]>>({});

  readonly formulario = this.formularioReactivo.group({
    nombreCompleto: ['', [Validators.required, Validators.maxLength(200)]],
    correoInstitucional: ['', [Validators.required, Validators.email]],
    contrasena: ['', [Validators.required, Validators.pattern(PATRON_CONTRASENA)]],
    confirmacion: ['', [Validators.required]]
  });

  constructor() {
    this.notificador.limpiar();
  }

  /** Primer mensaje del servidor para el campo indicado. */
  errorDe(campo: string): string | null {
    return this.erroresCampo()[campo]?.[0] ?? null;
  }

  /** Indica si el nombre debe mostrar su validación local. */
  nombreInvalido(): boolean {
    const control = this.formulario.controls.nombreCompleto;
    return control.touched && control.invalid;
  }

  /** Indica si el correo debe mostrar su validación local. */
  correoInvalido(): boolean {
    const control = this.formulario.controls.correoInstitucional;
    return control.touched && control.invalid;
  }

  /** Indica si la contraseña incumple la política local. */
  contrasenaInvalida(): boolean {
    const control = this.formulario.controls.contrasena;
    return control.touched && control.invalid;
  }

  /** Indica si la confirmación no coincide con la contraseña. */
  confirmacionInvalida(): boolean {
    const control = this.formulario.controls.confirmacion;
    const contrasena = this.formulario.controls.contrasena.value;

    return control.touched && (control.invalid || control.value !== contrasena);
  }

  /** Envía el formulario y traduce los `400` por campo y el `409` de conflicto. */
  enviar(): void {
    if (
      this.formulario.invalid
      || this.formulario.controls.confirmacion.value !== this.formulario.controls.contrasena.value
    ) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.conflicto.set(null);
    this.erroresCampo.set({});
    this.notificador.limpiar();

    const valores = this.formulario.getRawValue();

    this.api
      .registrar({
        nombreCompleto: valores.nombreCompleto,
        correoInstitucional: valores.correoInstitucional,
        contrasena: valores.contrasena
      })
      .subscribe({
        next: (creada) => {
          this.enviando.set(false);
          this.resultado.set(creada);
          void this.router.navigate(['/login'], {
            queryParams: { correo: creada.correoInstitucional, registrado: 1 }
          });
        },
        error: (error: unknown) => {
          this.enviando.set(false);

          if (!(error instanceof HttpErrorResponse)) {
            return;
          }

          const problema = leerProblema(error);

          if (error.status === 409) {
            this.conflicto.set(problema?.detail ?? 'El correo institucional ya está registrado.');
            return;
          }

          if (problema?.errors) {
            this.erroresCampo.set(problema.errors);
          }
        }
      });
  }
}
