import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { EspaciosApi } from '../services/espacios.api';
import { PlantillaQrPanel } from './plantilla-qr-panel';

describe('PlantillaQrPanel', () => {
  let api: jasmine.SpyObj<EspaciosApi>;
  let crearObjetoUrl: jasmine.Spy;
  let revocarObjetoUrl: jasmine.Spy;

  beforeEach(() => {
    api = jasmine.createSpyObj<EspaciosApi>('EspaciosApi', ['plantilla']);
    crearObjetoUrl = spyOn(URL, 'createObjectURL').and.returnValue('blob:plantilla-qr');
    revocarObjetoUrl = spyOn(URL, 'revokeObjectURL');

    TestBed.configureTestingModule({
      imports: [PlantillaQrPanel],
      providers: [{ provide: EspaciosApi, useValue: api }]
    });
  });

  it('solicita el blob, crea el objeto URL y lo revoca al destruir', () => {
    const blob = new Blob(['<svg/>'], { type: 'image/svg+xml' });
    api.plantilla.and.returnValue(of({ blob, totalHojas: 2 }));

    const fixture = TestBed.createComponent(PlantillaQrPanel);
    fixture.componentRef.setInput('espacioIds', ['area-1']);
    const panel = fixture.componentInstance;

    panel.generar();

    expect(api.plantilla).toHaveBeenCalledWith(['area-1'], true, 1);
    expect(crearObjetoUrl).toHaveBeenCalledWith(blob);
    expect(panel.urlSegura()).not.toBeNull();
    expect(panel.totalHojas()).toBe(2);

    fixture.destroy();

    expect(revocarObjetoUrl).toHaveBeenCalledWith('blob:plantilla-qr');
  });

  it('no solicita la plantilla sin áreas seleccionadas', () => {
    const fixture = TestBed.createComponent(PlantillaQrPanel);
    fixture.componentRef.setInput('espacioIds', []);

    fixture.componentInstance.generar();

    expect(api.plantilla).not.toHaveBeenCalled();
  });
});
