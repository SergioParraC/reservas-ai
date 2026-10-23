import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { PerfilUsuario } from '../auth/auth.models';
import { SesionStore } from '../auth/sesion.store';
import { jwtInterceptor } from './jwt.interceptor';

const PERFIL: PerfilUsuario = {
  id: 'usuario-1',
  nombreCompleto: 'Ana Administrativa',
  correoInstitucional: 'ana@uninpahu.edu.co',
  tipoActor: 1,
  roles: ['Administrativo']
};

describe('jwtInterceptor', () => {
  let http: HttpClient;
  let control: HttpTestingController;
  let store: SesionStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([jwtInterceptor])), provideHttpClientTesting()]
    });

    http = TestBed.inject(HttpClient);
    control = TestBed.inject(HttpTestingController);
    store = TestBed.inject(SesionStore);
  });

  afterEach(() => {
    control.verify();
    localStorage.clear();
  });

  it('añade Authorization solo a las solicitudes de la API cuando hay sesión', () => {
    store.guardar({ token: 'token-1', expiraEn: new Date(Date.now() + 600_000).toISOString(), perfil: PERFIL });

    http.get('/api/auth/me').subscribe();
    http.get('https://otro.example.com/datos').subscribe();

    const api = control.expectOne('/api/auth/me');
    expect(api.request.headers.get('Authorization')).toBe('Bearer token-1');

    const externa = control.expectOne('https://otro.example.com/datos');
    expect(externa.request.headers.has('Authorization')).toBeFalse();

    api.flush({});
    externa.flush({});
  });

  it('no añade el encabezado sin sesión', () => {
    http.get('/api/auth/me').subscribe();

    const api = control.expectOne('/api/auth/me');
    expect(api.request.headers.has('Authorization')).toBeFalse();
    api.flush({});
  });

  it('conserva la sesión vigente tras recargar la aplicación', () => {
    localStorage.setItem(
      'reservas.sesion',
      JSON.stringify({ token: 'token-persistido', expiraEn: new Date(Date.now() + 600_000).toISOString(), perfil: PERFIL })
    );

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([jwtInterceptor])), provideHttpClientTesting()]
    });

    TestBed.inject(HttpClient).get('/api/auth/me').subscribe();

    const api = TestBed.inject(HttpTestingController).expectOne('/api/auth/me');
    expect(api.request.headers.get('Authorization')).toBe('Bearer token-persistido');
    api.flush({});
  });

  it('usa el token de Entra cuando la sesión tiene origen federado', () => {
    store.guardar({
      token: 'token-entra',
      expiraEn: new Date(Date.now() + 600_000).toISOString(),
      origen: 'entra',
      perfil: PERFIL
    });

    http.get('/api/auth/me').subscribe();

    const api = control.expectOne('/api/auth/me');
    expect(api.request.headers.get('Authorization')).toBe('Bearer token-entra');
    api.flush({});
  });
});
