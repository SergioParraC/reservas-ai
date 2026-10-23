import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { PerfilUsuario } from '../../core/auth/auth.models';
import { EntraService } from '../../core/auth/entra.service';
import { NotificadorErrores } from '../../core/errores/notificador-errores';
import { LoginPage } from './login.page';

const PERFIL: PerfilUsuario = {
  id: 'usuario-1',
  nombreCompleto: 'Ana Administrativa',
  correoInstitucional: 'ana@uninpahu.edu.co',
  tipoActor: 1,
  roles: ['Administrativo']
};

describe('LoginPage', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let entra: jasmine.SpyObj<EntraService>;
  let notificador: NotificadorErrores;

  beforeEach(() => {
    localStorage.clear();
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['login', 'logout', 'cargarPerfil', 'restaurarSesion']);
    entra = jasmine.createSpyObj<EntraService>('EntraService', [
      'loginRedirect',
      'adquirirToken',
      'procesarCallback',
      'cerrarSesion'
    ]);
    entra.loginRedirect.and.resolveTo();

    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: EntraService, useValue: entra }
      ]
    });

    notificador = TestBed.inject(NotificadorErrores);
  });

  afterEach(() => localStorage.clear());

  it('autentica cuando el formulario es válido', () => {
    auth.login.and.returnValue(of(PERFIL));
    const fixture = TestBed.createComponent(LoginPage);
    const pagina = fixture.componentInstance;

    pagina.formulario.setValue({ correoInstitucional: 'ana@uninpahu.edu.co', contrasena: 'Clave.Segura.2026' });
    pagina.enviar();

    expect(auth.login).toHaveBeenCalledWith({
      correoInstitucional: 'ana@uninpahu.edu.co',
      contrasena: 'Clave.Segura.2026'
    });
  });

  it('no envía un formulario inválido y marca los campos', () => {
    const fixture = TestBed.createComponent(LoginPage);
    const pagina = fixture.componentInstance;

    pagina.formulario.setValue({ correoInstitucional: 'no-es-un-correo', contrasena: '' });
    pagina.enviar();

    expect(auth.login).not.toHaveBeenCalled();
    expect(pagina.formulario.controls.correoInstitucional.touched).toBeTrue();
    expect(pagina.formulario.controls.contrasena.touched).toBeTrue();
  });

  it('muestra el mensaje del problema publicado por el interceptor', () => {
    const fixture = TestBed.createComponent(LoginPage);

    notificador.error('El correo institucional o la contraseña no son válidos.');
    fixture.detectChanges();

    const alerta = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement | null;

    expect(alerta?.textContent).toContain('El correo institucional o la contraseña no son válidos.');
  });

  it('invoca loginRedirect al usar el correo de la universidad', async () => {
    const fixture = TestBed.createComponent(LoginPage);

    await fixture.componentInstance.accederConEntra();

    expect(entra.loginRedirect).toHaveBeenCalledWith('/inicio');
  });
});
