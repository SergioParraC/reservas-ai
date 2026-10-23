import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { PerfilUsuario } from '../../core/auth/auth.models';
import { EntraService } from '../../core/auth/entra.service';
import { SesionStore } from '../../core/auth/sesion.store';
import { UsuariosApi } from '../../core/usuarios/usuarios.api';
import { PerfilPage } from './perfil.page';

function perfil(vinculado: boolean): PerfilUsuario {
  return {
    id: 'usuario-1',
    nombreCompleto: 'Ana Administrativa',
    correoInstitucional: 'ana@uninpahu.edu.co',
    tipoActor: 3,
    roles: ['Estudiante'],
    vinculado
  };
}

describe('PerfilPage', () => {
  let api: jasmine.SpyObj<UsuariosApi>;
  let entra: jasmine.SpyObj<EntraService>;
  let auth: jasmine.SpyObj<AuthService>;
  let sesion: SesionStore;

  beforeEach(() => {
    localStorage.clear();
    api = jasmine.createSpyObj<UsuariosApi>('UsuariosApi', ['vincular', 'desvincular']);
    entra = jasmine.createSpyObj<EntraService>('EntraService', ['adquirirToken']);
    entra.adquirirToken.and.resolveTo('entra-token');
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['cargarPerfil']);
    auth.cargarPerfil.and.returnValue(of(perfil(true)));

    TestBed.configureTestingModule({
      imports: [PerfilPage],
      providers: [
        provideRouter([]),
        { provide: UsuariosApi, useValue: api },
        { provide: EntraService, useValue: entra },
        { provide: AuthService, useValue: auth }
      ]
    });

    sesion = TestBed.inject(SesionStore);
  });

  afterEach(() => localStorage.clear());

  function guardarSesion(vinculado: boolean): void {
    sesion.guardar({
      token: 'token-local',
      expiraEn: new Date(Date.now() + 600_000).toISOString(),
      origen: 'local',
      perfil: perfil(vinculado)
    });
  }

  it('muestra el estado sin vínculo y permite vincular la cuenta', async () => {
    guardarSesion(false);
    api.vincular.and.returnValue(of({ vinculado: true, correoInstitucional: 'ana@uninpahu.edu.co' }));

    const fixture = TestBed.createComponent(PerfilPage);
    const pagina = fixture.componentInstance;
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Cuenta sin vínculo institucional');

    await pagina.vincular();

    expect(entra.adquirirToken).toHaveBeenCalled();
    expect(api.vincular).toHaveBeenCalledWith('entra-token');
    expect(pagina.vinculado()).toBeTrue();
    expect(pagina.mensaje()).toContain('Cuenta vinculada');
    expect(auth.cargarPerfil).toHaveBeenCalled();
  });

  it('muestra el estado vinculado y permite desvincular', () => {
    guardarSesion(true);
    api.desvincular.and.returnValue(of(undefined));
    auth.cargarPerfil.and.returnValue(of(perfil(false)));

    const fixture = TestBed.createComponent(PerfilPage);
    const pagina = fixture.componentInstance;
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Cuenta vinculada con Entra ID');

    pagina.desvincular();

    expect(api.desvincular).toHaveBeenCalled();
    expect(pagina.vinculado()).toBeFalse();
  });

  it('explica el 409 cuando el correo institucional no coincide', async () => {
    guardarSesion(false);
    api.vincular.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: {
              type: 'https://api.reservas-espacios.edu/problems/conflicto',
              detail: 'El correo de la cuenta institucional no coincide con el correo registrado.'
            }
          })
      )
    );

    const fixture = TestBed.createComponent(PerfilPage);
    const pagina = fixture.componentInstance;

    await pagina.vincular();

    expect(pagina.conflicto()).toContain('no coincide');
  });
});
