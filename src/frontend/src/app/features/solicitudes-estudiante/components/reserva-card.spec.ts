import { TestBed } from '@angular/core/testing';

import { ReservaCard } from './reserva-card';
import { EstadoReserva, ReservaResumen } from '../models/reserva.models';

const RESERVA: ReservaResumen = {
  id: 'reserva-1',
  puestoId: 'puesto-1',
  puestoIdentificador: 'PC-01',
  espacioNombre: 'Sala de Cómputo 1',
  inicio: '2026-11-03T10:00:00-05:00',
  fin: '2026-11-03T11:00:00-05:00',
  estado: EstadoReserva.Confirmada,
  creadaEn: '2026-11-02T08:00:00-05:00',
  canceladaEn: null,
  cancelable: true
};

describe('ReservaCard', () => {
  function crear(reserva: ReservaResumen) {
    const fixture = TestBed.createComponent(ReservaCard);
    fixture.componentInstance.reserva = reserva;
    fixture.detectChanges();

    return fixture;
  }

  it('muestra el puesto, el espacio y el estado', () => {
    const fixture = crear(RESERVA);
    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('Sala de Cómputo 1');
    expect(texto).toContain('PC-01');
    expect(texto).toContain('Confirmada');
    expect(texto).toContain('10:00');
    expect(texto).toContain('11:00');
  });

  it('emite la reserva al pulsar cancelar cuando está dentro del plazo', () => {
    const fixture = crear(RESERVA);
    const emitidas: ReservaResumen[] = [];

    fixture.componentInstance.cancelar.subscribe((reserva) => emitidas.push(reserva));
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();

    expect(emitidas).toEqual([RESERVA]);
  });

  it('oculta el botón y explica el plazo cuando ya no es cancelable', () => {
    const fixture = crear({ ...RESERVA, cancelable: false });

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Ya no puede cancelarse');
  });

  it('no ofrece cancelación en una reserva ya cancelada', () => {
    const fixture = crear({
      ...RESERVA,
      estado: EstadoReserva.Cancelada,
      cancelable: false,
      canceladaEn: '2026-11-02T09:00:00-05:00'
    });

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Cancelada');
  });
});
