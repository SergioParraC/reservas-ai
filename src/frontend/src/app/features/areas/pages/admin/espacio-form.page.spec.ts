import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { throwError } from 'rxjs';

import { CategoriaEspacio, EstadoUnidad, TipoAprobacion, TipoElementoPrestamo, TipoModalidadPrestamo } from '../../models/espacio.models';
import { EspaciosApi } from '../../services/espacios.api';
import { EspacioFormPage } from './espacio-form.page';

describe('EspacioFormPage', () => {
  let api: jasmine.SpyObj<EspaciosApi>;

  beforeEach(() => {
    api = jasmine.createSpyObj<EspaciosApi>('EspaciosApi', [
      'crear',
      'actualizar',
      'obtener',
      'subirImagen',
      'eliminarImagen'
    ]);

    TestBed.configureTestingModule({
      imports: [EspacioFormPage],
      providers: [
        provideRouter([]),
        { provide: EspaciosApi, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({}) } }
        }
      ]
    });
  });

  function crearPagina(): EspacioFormPage {
    const fixture = TestBed.createComponent(EspacioFormPage);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  function datosValidos(aforo: number) {
    return {
      nombre: 'Laboratorio de Redes',
      ubicacion: 'Bloque B',
      categoria: CategoriaEspacio.Laboratorio,
      aforo,
      tipoAprobacion: TipoAprobacion.Docente,
      tipoModalidadPrestamo: TipoModalidadPrestamo.AreaCompleta,
      tipoElementoPrestamo: TipoElementoPrestamo.LaboratorioEspecializado,
      estado: EstadoUnidad.Activo
    };
  }

  it('no envía el formulario cuando el aforo es cero', () => {
    const pagina = crearPagina();

    pagina.formulario.setValue(datosValidos(0));
    pagina.guardar();

    expect(api.crear).not.toHaveBeenCalled();
    expect(pagina.aforoInvalido()).toBeTrue();
    expect(pagina.formulario.controls.aforo.touched).toBeTrue();
  });

  it('muestra el conflicto de aforo devuelto por la API', () => {
    api.crear.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: {
              type: 'https://api.reservas-espacios.edu/problems/conflicto',
              detail: 'El área ya alcanzó su aforo de 10 puestos activos.'
            }
          })
      )
    );

    const pagina = crearPagina();
    pagina.formulario.setValue(datosValidos(10));
    pagina.guardar();

    expect(api.crear).toHaveBeenCalled();
    expect(pagina.conflicto()).toContain('aforo');
  });

  it('pinta los errores por campo de un 400', () => {
    api.crear.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: {
              type: 'https://api.reservas-espacios.edu/problems/solicitud-invalida',
              errors: { aforo: ['El aforo no puede superar el máximo institucional de 500 personas.'] }
            }
          })
      )
    );

    const pagina = crearPagina();
    pagina.formulario.setValue(datosValidos(10));
    pagina.guardar();

    expect(pagina.errorDe('aforo')).toContain('máximo institucional');
  });

  it('muestra el mensaje de modalidad con ocupación futura', () => {
    api.crear.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: {
              type: 'https://api.reservas-espacios.edu/problems/modalidad-con-ocupacion',
              detail: 'No es posible cambiar la modalidad de préstamo: el área tiene ocupación futura.'
            }
          })
      )
    );

    const pagina = crearPagina();
    pagina.formulario.setValue(datosValidos(10));
    pagina.guardar();

    expect(pagina.conflicto()).toContain('ocupación');
  });

  it('bloquea una combinación incoherente de modalidad y elemento', () => {
    const pagina = crearPagina();
    pagina.formulario.setValue({
      ...datosValidos(10),
      tipoModalidadPrestamo: TipoModalidadPrestamo.PuestoIndividual,
      tipoElementoPrestamo: TipoElementoPrestamo.Auditorio
    });

    pagina.guardar();

    expect(api.crear).not.toHaveBeenCalled();
    expect(pagina.combinacionInvalida()).toBeTrue();
    expect(pagina.conflicto()).toContain('combinación');
  });

  it('ajusta los elementos disponibles al cambiar de modalidad', () => {
    const pagina = crearPagina();

    pagina.formulario.controls.tipoModalidadPrestamo.setValue(TipoModalidadPrestamo.PuestoIndividual);

    expect(pagina.elementosDisponibles()).toEqual([TipoElementoPrestamo.EquipoComputo]);
    expect(pagina.formulario.controls.tipoElementoPrestamo.value).toBe(TipoElementoPrestamo.EquipoComputo);
  });
});
