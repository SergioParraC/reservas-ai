import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { PerfilUsuario } from '../auth/auth.models';
import { SesionStore } from '../auth/sesion.store';
import { NotificadorErrores } from '../errores/notificador-errores';
import { problemDetailsInterceptor } from './problem-details.interceptor';

const PERFIL: PerfilUsuario = {
  id: 'usuario-1',
  nombreCompleto: 'Ana Administrativa',
  correoInstitucional: 'ana@uninpahu.edu.co',
  tipoActor: 1,
  roles: ['Administrativo']
};

describe('problemDetailsInterceptor', () => {
  let http: HttpClient;
  let control: HttpTestingController;
  let store: SesionStore;
  let notificador: NotificadorErrores;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });

    http = TestBed.inject(HttpClient);
    control = TestBed.inject(HttpTestingController);
    store = TestBed.inject(SesionStore);
    notificador = TestBed.inject(NotificadorErrores);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    control.verify();
    localStorage.clear();
  });

  it('descarta la sesión y redirige al login ante sesion-expirada', () => {
    spyOn(router, 'navigate');
    store.guardar({ token: 'token-1', expiraEn: new Date(Date.now() + 600_000).toISOString(), perfil: PERFIL });

    http.get('/api/auth/me').subscribe({ error: () => undefined });
    control.expectOne('/api/auth/me').flush(
      {
        type: 'https://api.reservas-espacios.edu/problems/sesion-expirada',
        title: 'Sesión expirada',
        status: 401,
        detail: 'La sesión expiró. Vuelva a iniciar sesión.'
      },
      { status: 401, statusText: 'Unauthorized' }
    );

    expect(store.estaAutenticado()).toBeFalse();
    expect(notificador.sesionExpirada()).toBeTrue();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], { queryParams: { motivo: 'sesion-expirada' } });
  });

  it('muestra el detail del problema ante un 403', () => {
    http.get('/api/administracion/verificacion').subscribe({ error: () => undefined });
    control.expectOne('/api/administracion/verificacion').flush(
      {
        type: 'https://api.reservas-espacios.edu/problems/permisos-insuficientes',
        title: 'Permisos insuficientes',
        status: 403,
        detail: 'No tiene permisos para realizar esta operación.'
      },
      { status: 403, statusText: 'Forbidden' }
    );

    expect(notificador.mensaje()).toBe('No tiene permisos para realizar esta operación.');
  });

  it('aplica un mensaje genérico cuando la respuesta no cumple RFC 7807', () => {
    http.get('/api/auth/me').subscribe({ error: () => undefined });
    control.expectOne('/api/auth/me').flush('<html>Proxy error</html>', {
      status: 500,
      statusText: 'Server Error'
    });

    expect(notificador.mensaje()).toBe('Ocurrió un error inesperado. Intente nuevamente.');
  });

  it('explica el conflicto de franja no disponible', () => {
    http.post('/api/reservas', {}).subscribe({ error: () => undefined });
    control.expectOne('/api/reservas').flush(
      {
        type: 'https://api.reservas-espacios.edu/problems/franja-no-disponible',
        title: 'Franja no disponible',
        status: 409,
        detail: 'La franja solicitada ya está ocupada en el puesto.'
      },
      { status: 409, statusText: 'Conflict' }
    );

    expect(notificador.mensaje()).toContain('ya está ocupada');
  });

  it('explica el límite de reservas activas', () => {
    http.post('/api/reservas', {}).subscribe({ error: () => undefined });
    control.expectOne('/api/reservas').flush(
      {
        type: 'https://api.reservas-espacios.edu/problems/limite-reservas-alcanzado',
        title: 'Límite de reservas alcanzado',
        status: 409,
        detail: 'La cuenta alcanzó el máximo de 2 reservas activas.'
      },
      { status: 409, statusText: 'Conflict' }
    );

    expect(notificador.mensaje()).toContain('máximo de reservas activas');
  });

  it('explica la cancelación fuera de plazo', () => {
    http.delete('/api/reservas/reserva-1').subscribe({ error: () => undefined });
    control.expectOne('/api/reservas/reserva-1').flush(
      {
        type: 'https://api.reservas-espacios.edu/problems/fuera-de-plazo',
        title: 'Fuera de plazo',
        status: 409,
        detail: 'La reserva ya no puede cancelarse: faltan menos de 30 minutos para su inicio.'
      },
      { status: 409, statusText: 'Conflict' }
    );

    expect(notificador.mensaje()).toContain('ya no puede cancelarse');
  });
});
