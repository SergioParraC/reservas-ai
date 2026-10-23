import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, RouterStateSnapshot, UrlTree } from '@angular/router';

import { PerfilUsuario } from '../auth/auth.models';
import { SesionStore } from '../auth/sesion.store';
import { authGuard } from './auth.guard';

const PERFIL: PerfilUsuario = {
  id: 'usuario-1',
  nombreCompleto: 'Ana Administrativa',
  correoInstitucional: 'ana@uninpahu.edu.co',
  tipoActor: 1,
  roles: ['Administrativo']
};

describe('authGuard', () => {
  let store: SesionStore;

  const ruta = {} as ActivatedRouteSnapshot;
  const estado = { url: '/inicio' } as RouterStateSnapshot;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    store = TestBed.inject(SesionStore);
  });

  afterEach(() => localStorage.clear());

  it('redirige al login cuando no hay sesión', () => {
    const resultado = TestBed.runInInjectionContext(() => authGuard(ruta, estado));

    expect(resultado instanceof UrlTree).toBeTrue();
    expect((resultado as UrlTree).toString()).toContain('/login');
  });

  it('permite el acceso con una sesión vigente', () => {
    store.guardar({ token: 'token-1', expiraEn: new Date(Date.now() + 600_000).toISOString(), perfil: PERFIL });

    const resultado = TestBed.runInInjectionContext(() => authGuard(ruta, estado));

    expect(resultado).toBeTrue();
  });

  it('acepta una sesión federada de Entra ID', () => {
    store.guardar({
      token: 'token-entra',
      expiraEn: new Date(Date.now() + 600_000).toISOString(),
      origen: 'entra',
      perfil: PERFIL
    });

    const resultado = TestBed.runInInjectionContext(() => authGuard(ruta, estado));

    expect(resultado).toBeTrue();
  });
});
