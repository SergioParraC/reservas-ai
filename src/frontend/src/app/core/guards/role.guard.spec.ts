import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, RouterStateSnapshot, UrlTree } from '@angular/router';

import { PerfilUsuario } from '../auth/auth.models';
import { SesionStore } from '../auth/sesion.store';
import { roleGuard } from './role.guard';

const PERFIL_ESTUDIANTE: PerfilUsuario = {
  id: 'usuario-2',
  nombreCompleto: 'Luis Estudiante',
  correoInstitucional: 'luis@universidad.edu',
  tipoActor: 3,
  roles: ['Estudiante']
};

describe('roleGuard', () => {
  let store: SesionStore;

  const estado = {} as RouterStateSnapshot;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    store = TestBed.inject(SesionStore);
  });

  afterEach(() => localStorage.clear());

  it('redirige al inicio cuando falta el rol exigido', () => {
    store.guardar({ token: 'token-1', expiraEn: new Date(Date.now() + 600_000).toISOString(), perfil: PERFIL_ESTUDIANTE });
    const ruta = { data: { roles: ['Administrativo'] } } as unknown as ActivatedRouteSnapshot;

    const resultado = TestBed.runInInjectionContext(() => roleGuard(ruta, estado));

    expect(resultado instanceof UrlTree).toBeTrue();
    expect((resultado as UrlTree).toString()).toContain('/inicio');
  });

  it('permite el acceso cuando el rol estÃ¡ presente', () => {
    store.guardar({ token: 'token-1', expiraEn: new Date(Date.now() + 600_000).toISOString(), perfil: PERFIL_ESTUDIANTE });
    const ruta = { data: { roles: ['Estudiante'] } } as unknown as ActivatedRouteSnapshot;

    expect(TestBed.runInInjectionContext(() => roleGuard(ruta, estado))).toBeTrue();
  });

  it('permite el acceso cuando la ruta no exige roles', () => {
    const ruta = { data: {} } as unknown as ActivatedRouteSnapshot;

    expect(TestBed.runInInjectionContext(() => roleGuard(ruta, estado))).toBeTrue();
  });
});
