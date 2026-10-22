import { TestBed } from '@angular/core/testing';

import { PerfilUsuario } from './auth.models';
import { SesionStore } from './sesion.store';

const PERFIL: PerfilUsuario = {
  id: 'usuario-1',
  nombreCompleto: 'Ana Administrativa',
  correoInstitucional: 'ana@universidad.edu',
  tipoActor: 1,
  roles: ['Administrativo']
};

describe('SesionStore', () => {
  let store: SesionStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    store = TestBed.inject(SesionStore);
  });

  afterEach(() => localStorage.clear());

  it('inicia sin sesión', () => {
    expect(store.estaAutenticado()).toBeFalse();
    expect(store.perfil()).toBeNull();
    expect(store.roles()).toEqual([]);
    expect(store.token()).toBeNull();
  });

  it('expone perfil, roles y token al iniciar sesión', () => {
    store.guardar({ token: 'token-1', expiraEn: new Date(Date.now() + 600_000).toISOString(), perfil: PERFIL });

    expect(store.estaAutenticado()).toBeTrue();
    expect(store.perfil()?.correoInstitucional).toBe('ana@universidad.edu');
    expect(store.roles()).toEqual(['Administrativo']);
    expect(store.token()).toBe('token-1');
    expect(store.tieneRol('Administrativo')).toBeTrue();
  });

  it('limpia el estado al cerrar sesión', () => {
    store.guardar({ token: 'token-1', expiraEn: new Date(Date.now() + 600_000).toISOString(), perfil: PERFIL });

    store.limpiar();

    expect(store.estaAutenticado()).toBeFalse();
    expect(store.perfil()).toBeNull();
    expect(localStorage.getItem('reservas.sesion')).toBeNull();
  });

  it('trata como no vigente un token expirado', () => {
    store.guardar({ token: 'token-1', expiraEn: new Date(Date.now() - 60_000).toISOString(), perfil: PERFIL });

    expect(store.estaAutenticado()).toBeFalse();
    expect(store.token()).toBeNull();
  });

  it('restaura la sesión persistida tras una recarga', () => {
    store.guardar({ token: 'token-1', expiraEn: new Date(Date.now() + 600_000).toISOString(), perfil: PERFIL });

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    const recargado = TestBed.inject(SesionStore);

    expect(recargado.estaAutenticado()).toBeTrue();
    expect(recargado.token()).toBe('token-1');
  });
});
