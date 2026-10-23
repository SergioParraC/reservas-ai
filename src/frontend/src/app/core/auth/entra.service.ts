import { Injectable, InjectionToken, inject } from '@angular/core';
import {
  AccountInfo,
  AuthenticationResult,
  IPublicClientApplication,
  PublicClientApplication
} from '@azure/msal-browser';

import { environment } from '../../../environments/environment';

/** Token de inyección que permite sustituir MSAL por un mock en las pruebas. */
export const MSAL_INSTANCE = new InjectionToken<IPublicClientApplication>('MSAL_INSTANCE');

const CLAVE_RETORNO = 'reservas.retorno-entra';

/** Resultado del callback OIDC ya canjeado por un access token del API. */
export interface ResultadoEntra {
  token: string;
  expiraEn: string;
  retorno: string;
}

/**
 * Adaptador de MSAL Browser con flujo `loginRedirect` y PKCE. La SPA solo necesita
 * adquirir tokens; no se usa `@azure/msal-angular`.
 */
@Injectable({ providedIn: 'root' })
export class EntraService {
  private readonly proporcionada = inject(MSAL_INSTANCE, { optional: true });
  private instancia: IPublicClientApplication | null = null;
  private inicializacion: Promise<void> | null = null;

  /** Indica si el registro de aplicación está configurado en el entorno. */
  readonly configurado = environment.entraId.clientId.trim().length > 0;

  /** Inicia el flujo de acceso institucional y guarda el destino de retorno. */
  async loginRedirect(retorno = '/inicio'): Promise<void> {
    sessionStorage.setItem(CLAVE_RETORNO, retorno);
    const instancia = await this.motor();

    await instancia.loginRedirect({
      scopes: [...environment.entraId.scopes],
      redirectUri: this.urlRetorno()
    });
  }

  /** Procesa la redirección de Entra y devuelve el token listo para la API. */
  async procesarCallback(): Promise<ResultadoEntra | null> {
    const instancia = await this.motor();
    const resultado = await instancia.handleRedirectPromise();
    const cuenta = resultado?.account ?? instancia.getAllAccounts()[0];

    if (!cuenta) {
      return null;
    }

    instancia.setActiveAccount(cuenta);
    const token = await this.adquirirConCuenta(instancia, cuenta);
    const retorno = sessionStorage.getItem(CLAVE_RETORNO) ?? '/inicio';
    sessionStorage.removeItem(CLAVE_RETORNO);

    return {
      token: token.accessToken,
      expiraEn: this.expiracion(token),
      retorno
    };
  }

  /** Obtiene un token silencioso para la cuenta activa, o `null` si no hay cuenta. */
  async adquirirToken(): Promise<string | null> {
    const instancia = await this.motor();
    const cuenta = instancia.getActiveAccount() ?? instancia.getAllAccounts()[0];

    if (!cuenta) {
      return null;
    }

    const token = await this.adquirirConCuenta(instancia, cuenta);

    return token.accessToken;
  }

  /** Cierra la sesión de MSAL sin afectar la sesión local almacenada. */
  async cerrarSesion(): Promise<void> {
    if (!this.proporcionada && !this.configurado) {
      return;
    }

    const instancia = await this.motor();
    const cuenta = instancia.getActiveAccount() ?? undefined;

    await instancia.logoutRedirect({ account: cuenta, postLogoutRedirectUri: this.urlRetorno() });
  }

  private async motor(): Promise<IPublicClientApplication> {
    const instancia = this.obtenerInstancia();

    if (!this.inicializacion) {
      this.inicializacion = instancia.initialize();
    }

    await this.inicializacion;

    return instancia;
  }

  private obtenerInstancia(): IPublicClientApplication {
    if (this.proporcionada) {
      return this.proporcionada;
    }

    if (!this.configurado) {
      throw new Error('El acceso con el correo de la universidad no está configurado.');
    }

    this.instancia ??= new PublicClientApplication({
      auth: {
        clientId: environment.entraId.clientId,
        authority: environment.entraId.authority,
        redirectUri: this.urlRetorno()
      },
      cache: { cacheLocation: 'localStorage' }
    });

    return this.instancia;
  }

  private adquirirConCuenta(
    instancia: IPublicClientApplication,
    cuenta: AccountInfo
  ): Promise<AuthenticationResult> {
    return instancia.acquireTokenSilent({
      scopes: [...environment.entraId.scopes],
      account: cuenta
    });
  }

  private urlRetorno(): string {
    const configurada = environment.entraId.redirectUri;

    return configurada.startsWith('http')
      ? configurada
      : new URL(configurada, window.location.origin).toString();
  }

  private expiracion(resultado: AuthenticationResult): string {
    const expira = resultado.expiresOn ?? new Date(Date.now() + 3_600_000);

    return expira.toISOString();
  }
}
