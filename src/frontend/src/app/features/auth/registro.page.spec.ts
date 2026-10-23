import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { UsuariosApi } from '../../core/usuarios/usuarios.api';
import { RegistroPage } from './registro.page';

describe('RegistroPage', () => {
  let api: jasmine.SpyObj<UsuariosApi>;
  let router: Router;

  beforeEach(() => {
    api = jasmine.createSpyObj<UsuariosApi>('UsuariosApi', ['registrar']);

    TestBed.configureTestingModule({
      imports: [RegistroPage],
      providers: [provideRouter([]), { provide: UsuariosApi, useValue: api }]
    });

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  });

  function crearPagina(): RegistroPage {
    return TestBed.createComponent(RegistroPage).componentInstance;
  }

  function completar(pagina: RegistroPage, contrasena = 'Clave.Segura.2026'): void {
    pagina.formulario.setValue({
      nombreCompleto: 'Ana Administrativa',
      correoInstitucional: 'ana@uninpahu.edu.co',
      contrasena,
      confirmacion: contrasena
    });
  }

  it('rechaza una contraseña que no cumple la política sin llamar a la API', () => {
    const pagina = crearPagina();
    completar(pagina, 'corta');

    pagina.enviar();

    expect(api.registrar).not.toHaveBeenCalled();
    expect(pagina.contrasenaInvalida()).toBeTrue();
    expect(pagina.formulario.controls.confirmacion.touched).toBeTrue();
  });

  it('muestra el rol asignado en el 201 y redirige al acceso', () => {
    api.registrar.and.returnValue(
      of({
        id: 'usuario-1',
        nombreCompleto: 'Ana Administrativa',
        correoInstitucional: 'ana@uninpahu.edu.co',
        rol: 'Administrativo'
      })
    );

    const pagina = crearPagina();
    completar(pagina);
    pagina.enviar();

    expect(api.registrar).toHaveBeenCalledWith({
      nombreCompleto: 'Ana Administrativa',
      correoInstitucional: 'ana@uninpahu.edu.co',
      contrasena: 'Clave.Segura.2026'
    });
    expect(pagina.resultado()?.rol).toBe('Administrativo');
    expect(router.navigate).toHaveBeenCalledWith(
      ['/login'],
      jasmine.objectContaining({
        queryParams: jasmine.objectContaining({ correo: 'ana@uninpahu.edu.co', registrado: 1 })
      })
    );
  });

  it('explica el 409 de correo ya registrado', () => {
    api.registrar.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: {
              type: 'https://api.reservas-espacios.edu/problems/conflicto',
              detail: 'El correo institucional ya está registrado en la plataforma.'
            }
          })
      )
    );

    const pagina = crearPagina();
    completar(pagina);
    pagina.enviar();

    expect(pagina.conflicto()).toContain('ya está registrado');
  });
});
