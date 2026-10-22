import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { TIPOS_ACTOR } from '../../core/auth/auth.models';
import { SesionStore } from '../../core/auth/sesion.store';

/** Página protegida mínima de verificación de la sesión. */
@Component({
  selector: 'app-inicio',
  imports: [RouterLink],
  template: `
    <h1>Panel de inicio</h1>

    @if (perfil(); as datos) {
      <section class="tarjeta">
        <p class="nombre">{{ datos.nombreCompleto }}</p>
        <p class="detalle">{{ datos.correoInstitucional }}</p>
        <p class="detalle">Tipo de actor: {{ tipoActor() }}</p>
        <p class="detalle">Roles: {{ datos.roles.join(', ') }}</p>
        <a routerLink="/administracion" class="enlace">Ir a la verificación administrativa</a>
      </section>
    }

    @if (sinPermisos()) {
      <p class="aviso" role="status">No tiene permisos para la sección solicitada.</p>
    }

    @if (mensajeApi(); as texto) {
      <p class="aviso" role="status">{{ texto }}</p>
    }
  `,
  styles: `
    .tarjeta {
      padding: 1.5rem;
      background: #fff;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
    }

    .nombre {
      margin: 0;
      font-size: 1.15rem;
      font-weight: 700;
    }

    .detalle {
      margin: 0.35rem 0;
      color: #4b5563;
    }

    .enlace {
      display: inline-block;
      margin-top: 1rem;
      color: var(--color-primario);
    }

    .aviso {
      margin-top: 1rem;
      padding: 0.6rem 0.8rem;
      border-left: 4px solid #d97706;
      background: #fffbeb;
      border-radius: 0.25rem;
    }
  `
})
export class InicioPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly sesion = inject(SesionStore);
  private readonly ruta = inject(ActivatedRoute);

  readonly perfil = this.sesion.perfil;
  readonly tipoActor = computed(() => TIPOS_ACTOR[this.perfil()?.tipoActor ?? 0] ?? 'Sin asignar');
  readonly sinPermisos = signal(this.ruta.snapshot.queryParamMap.get('motivo') === 'sin-permisos');
  readonly mensajeApi = signal<string | null>(null);

  /** Refresca el perfil desde la API al entrar en la página. */
  ngOnInit(): void {
    this.auth.cargarPerfil().subscribe({
      error: () => this.mensajeApi.set('No se pudo actualizar el perfil desde la API.')
    });
  }
}
