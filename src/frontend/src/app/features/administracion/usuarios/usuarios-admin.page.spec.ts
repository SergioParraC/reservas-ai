import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { PerfilUsuario } from '../../../core/auth/auth.models';
import { SesionStore } from '../../../core/auth/sesion.store';
import { UsuariosApi } from '../../../core/usuarios/usuarios.api';
import { UsuarioAdministrativo } from '../../../core/usuarios/usuarios.models';
import { UsuariosAdminPage } from './usuarios-admin.page';

const ESTUDIANTE: UsuarioAdministrativo = {
  id: 'usuario-2',
  nombreCompleto: 'Luis Estudiante',
  correoInstitucional: 'luis@uninpahu.edu.co',
  rol: 'Estudiante',
  tipoActor: 3,
  activo: true,
  vinculado: false,
  creadoEn: new Date().toISOString(),
  ultimoAccesoEn: null
};

function perfil(roles: string[]): PerfilUsuario {
  return {
    id: 'usuario-1',
    nombreCompleto: 'Ana',
    correoInstitucional: 'ana@uninpahu.edu.co',
    tipoActor: 1,
    roles
  };
}

describe('UsuariosAdminPage', () => {
  let api: jasmine.SpyObj<UsuariosApi>;
  let sesion: SesionStore;

  beforeEach(() => {
    localStorage.clear();
    api = jasmine.createSpyObj<UsuariosApi>('UsuariosApi', ['listar', 'cambiarRol']);

    TestBed.configureTestingModule({
      imports: [UsuariosAdminPage],
      providers: [provideRouter([]), { provide: UsuariosApi, useValue: api }]
    });

    sesion = TestBed.inject(SesionStore);
  });

  afterEach(() => localStorage.clear());

  function guardarSesion(roles: string[]): void {
    sesion.guardar({
      token: 'token-1',
      expiraEn: new Date(Date.now() + 600_000).toISOString(),
      perfil: perfil(roles)
    });
  }

  function paginaListada() {
    return {
      elementos: [ESTUDIANTE],
      total: 1,
      pagina: 1,
      tamano: 10
    };
  }

  it('bloquea la consola cuando la sesión no es administrativa', () => {
    guardarSesion(['Estudiante']);
    const fixture = TestBed.createComponent(UsuariosAdminPage);

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No tiene permisos');
    expect(api.listar).not.toHaveBeenCalled();
  });

  it('lista las cuentas para un administrador', () => {
    guardarSesion(['Administrativo']);
    api.listar.and.returnValue(of(paginaListada()));

    const fixture = TestBed.createComponent(UsuariosAdminPage);
    fixture.detectChanges();
    fixture.detectChanges();

    expect(api.listar).toHaveBeenCalledWith(jasmine.objectContaining({ pagina: 1, tamano: 10 }));
    expect(fixture.nativeElement.textContent).toContain('Luis Estudiante');
  });

  it('muestra el 409 del último administrador o de la autoedición', () => {
    guardarSesion(['Administrativo']);
    api.listar.and.returnValue(of(paginaListada()));
    api.cambiarRol.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: {
              type: 'https://api.reservas-espacios.edu/problems/conflicto',
              detail: 'El sistema debe conservar al menos un administrador activo.'
            }
          })
      )
    );

    const fixture = TestBed.createComponent(UsuariosAdminPage);
    const pagina = fixture.componentInstance;
    fixture.detectChanges();

    pagina.solicitarCambio(ESTUDIANTE, 'Docente');
    pagina.confirmarCambio();

    expect(api.cambiarRol).toHaveBeenCalledWith(ESTUDIANTE.id, 'Docente');
    expect(pagina.conflicto()).toContain('administrador activo');
  });
});
