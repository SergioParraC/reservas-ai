import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { EntraService } from '../../core/auth/entra.service';
import { SesionStore } from '../../core/auth/sesion.store';
import { NotificadorErrores } from '../../core/errores/notificador-errores';

/** Procesa la redirección de Entra ID y crea la sesión con origen federado. */
@Component({
  selector: 'app-entra-callback',
  template: `
    <section class="tarjeta" role="status">
      <h1>Acceso institucional</h1>
      <p>{{ mensaje() }}</p>
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
      text-align: center;
    }
  `
})
export class EntraCallbackPage implements OnInit {
  private readonly entra = inject(EntraService);
  private readonly sesion = inject(SesionStore);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notificador = inject(NotificadorErrores);

  readonly mensaje = signal('Validando su cuenta institucional…');

  /** Ejecuta `handleRedirectPromise` y resuelve el perfil local vinculado. */
  async ngOnInit(): Promise<void> {
    try {
      const resultado = await this.entra.procesarCallback();

      if (!resultado) {
        this.fallar();
        return;
      }

      // El token se guarda antes de pedir el perfil para que el interceptor lo adjunte.
      this.sesion.guardar({
        token: resultado.token,
        expiraEn: resultado.expiraEn,
        origen: 'entra',
        perfil: {
          id: '',
          nombreCompleto: '',
          correoInstitucional: '',
          tipoActor: 0,
          roles: []
        }
      });

      this.auth.cargarPerfil().subscribe({
        next: () => void this.router.navigateByUrl(resultado.retorno, { replaceUrl: true }),
        error: () => {
          this.sesion.limpiar();
          void this.router.navigate(['/login'], { queryParams: { motivo: 'entra' } });
        }
      });
    } catch {
      this.fallar();
    }
  }

  private fallar(): void {
    this.sesion.limpiar();
    this.notificador.error('No fue posible completar el acceso con el correo de la universidad.');
    void this.router.navigate(['/login'], { queryParams: { motivo: 'entra' } });
  }
}
