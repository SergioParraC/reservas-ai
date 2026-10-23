import { TestBed } from '@angular/core/testing';

import { DialogoRechazo } from './dialogo-rechazo';

describe('DialogoRechazo', () => {
  function crear(abierto = true): { componente: DialogoRechazo; elemento: HTMLElement } {
    const fixture = TestBed.createComponent(DialogoRechazo);
    fixture.componentRef.setInput('abierto', abierto);
    fixture.detectChanges();

    return {
      componente: fixture.componentInstance,
      elemento: fixture.nativeElement as HTMLElement
    };
  }

  it('no se muestra cuando está cerrado', () => {
    const { elemento } = crear(false);

    expect(elemento.querySelector('[role="dialog"]')).toBeNull();
  });

  it('exige un motivo de al menos diez caracteres', () => {
    const { componente } = crear();

    componente.motivo.set('corto');
    componente.confirmarRechazo();

    expect(componente.error()).toContain('al menos 10 caracteres');
  });

  it('emite el motivo válido y limpia el formulario', () => {
    const { componente } = crear();
    let emitido = '';

    componente.confirmar.subscribe((motivo) => (emitido = motivo));
    componente.motivo.set('La franja se cruza con una práctica programada.');
    componente.confirmarRechazo();

    expect(emitido).toContain('práctica programada');
    expect(componente.motivo()).toBe('');
    expect(componente.error()).toBeNull();
  });
});
