import { TestBed } from '@angular/core/testing';

import { FranjaLista } from './franja-lista';
import { FranjaDisponible } from '../models/reserva.models';

const LIBRE: FranjaDisponible = {
  inicio: '2026-11-03T10:00:00-05:00',
  fin: '2026-11-03T11:00:00-05:00',
  disponible: true,
  motivo: null
};

const OCUPADA: FranjaDisponible = {
  inicio: '2026-11-03T11:00:00-05:00',
  fin: '2026-11-03T12:00:00-05:00',
  disponible: false,
  motivo: 'Ocupada'
};

describe('FranjaLista', () => {
  function crear(franjas: FranjaDisponible[]) {
    const fixture = TestBed.createComponent(FranjaLista);
    fixture.componentInstance.franjas = franjas;
    fixture.detectChanges();

    return fixture;
  }

  it('deshabilita las franjas ocupadas y muestra el motivo', () => {
    const fixture = crear([LIBRE, OCUPADA]);
    const botones = fixture.nativeElement.querySelectorAll('.franja');

    expect(botones.length).toBe(2);
    expect(botones[1].disabled).toBeTrue();
    expect(botones[1].textContent).toContain('Ocupada');
    expect(botones[0].disabled).toBeFalse();
  });

  it('emite la franja seleccionada al elegir una disponible', () => {
    const fixture = crear([LIBRE, OCUPADA]);
    const emitidas: FranjaDisponible[] = [];

    fixture.componentInstance.seleccionar.subscribe((franja) => emitidas.push(franja));
    (fixture.nativeElement.querySelector('.franja') as HTMLButtonElement).click();

    expect(emitidas).toEqual([LIBRE]);
  });

  it('no emite nada al pulsar una franja ocupada', () => {
    const fixture = crear([OCUPADA]);
    const emitidas: FranjaDisponible[] = [];

    fixture.componentInstance.seleccionar.subscribe((franja) => emitidas.push(franja));
    fixture.componentInstance.elegir(OCUPADA);

    expect(emitidas.length).toBe(0);
  });

  it('marca la franja seleccionada', () => {
    const fixture = crear([LIBRE]);
    fixture.componentInstance.seleccionada = LIBRE;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.franja').getAttribute('aria-pressed')).toBe('true');
  });
});
