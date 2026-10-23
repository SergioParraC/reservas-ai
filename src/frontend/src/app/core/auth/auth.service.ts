import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, switchMap, tap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { CredencialesLocales, PerfilUsuario, Sesion, TokenEmitido } from './auth.models';
import { SesionStore } from './sesion.store';

/**
 * Autenticación local de la SPA: inicio y cierre de sesión y carga del perfil.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly sesion = inject(SesionStore);
  private readonly router = inject(Router);

  /** Indica si hay una sesión con token vigente. */
  readonly estaAutenticado = this.sesion.estaAutenticado;

  /** Roles concedidos al portador. */
  readonly roles = this.sesion.roles;

  /** Inicia sesión y deja el perfil completo en el estado de sesión. */
  login(credenciales: CredencialesLocales): Observable<PerfilUsuario> {
    return this.http
      .post<TokenEmitido>(`${environment.apiBaseUrl}/api/auth/login`, credenciales)
      .pipe(
        switchMap((emitido) => {
          // El token se guarda antes de pedir el perfil para que el interceptor lo adjunte.
          this.sesion.guardar({
            token: emitido.token,
            expiraEn: emitido.expiraEn,
            origen: 'local',
            perfil: {
              id: '',
              nombreCompleto: '',
              correoInstitucional: credenciales.correoInstitucional,
              tipoActor: 0,
              roles: emitido.roles
            }
          });

          return this.http.get<PerfilUsuario>(`${environment.apiBaseUrl}/api/auth/me`).pipe(
            tap((perfil) => this.sesion.guardarPerfil(perfil))
          );
        })
      );
  }

  /** Devuelve el perfil vigente desde la API. */
  cargarPerfil(): Observable<PerfilUsuario> {
    return this.http
      .get<PerfilUsuario>(`${environment.apiBaseUrl}/api/auth/me`)
      .pipe(tap((perfil) => this.sesion.guardarPerfil(perfil)));
  }

  /** Descarta la sesión y vuelve a la pantalla de acceso. */
  logout(): void {
    this.sesion.limpiar();
    void this.router.navigate(['/login']);
  }

  /** Reconstruye la sesión desde el almacenamiento (por ejemplo tras recargar). */
  restaurarSesion(): Sesion | null {
    return this.sesion.sesion();
  }
}
