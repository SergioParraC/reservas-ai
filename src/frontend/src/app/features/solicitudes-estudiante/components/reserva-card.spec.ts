import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ReservaCard } from './reserva-card';
import { EstadoReserva, ReservaResumen } from '../models/reserva.models';

const RESERVA: ReservaResumen = {
  id: 'reserva-1',
  puestoId: 'puesto-1',
  puestoIdentificador: 'PC-01',
  espacioNombre: 'Sala de Cómputo 1',
  inicio: '2026-11-03T10:00:00-05:00',
  fin: '2026-11-03T11:00:00-05:00',
  finPlanificado: '2026-11-03T11:00:00-05:00',
  estado: EstadoReserva.Confirmada,
  creadaEn: '2026-11-02T08:00:00-05:00',
  canceladaEn: null,
  fechaCheckIn: null,
  fechaCheckOut: null,
  cancelable: true
};

describe('ReservaCard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
  });

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

  it('ofrece el escáner solo mientras la reserva está confirmada', () => {
    const confirmada = crear(RESERVA);
    const enlace = confirmada.nativeElement.querySelector('a.accion') as HTMLAnchorElement;

    expect(enlace.textContent).toContain('Escanear para ingresar');
    expect(enlace.getAttribute('href')).toBe('/checkin/reserva-1');

    const enUso = crear({ ...RESERVA, estado: EstadoReserva.EnUso, cancelable: false });
    const textos = Array.from(enUso.nativeElement.querySelectorAll('a.accion')).map(
      (nodo) => (nodo as HTMLAnchorElement).textContent ?? ''
    );

    expect(textos.join(' ')).toContain('Registrar salida');
    expect(textos.join(' ')).not.toContain('Escanear para ingresar');
  });

  it('habilita la salida únicamente en EnUso', () => {
    const confirmada = crear(RESERVA);
    const finalizada = crear({
      ...RESERVA,
      estado: EstadoReserva.Finalizada,
      cancelable: false,
      fin: '2026-11-03T10:45:00-05:00',
      fechaCheckIn: '2026-11-03T10:02:00-05:00',
      fechaCheckOut: '2026-11-03T10:45:00-05:00'
    });

    expect(confirmada.componentInstance.salidaHabilitada()).toBeFalse();
    expect(finalizada.componentInstance.salidaHabilitada()).toBeFalse();
    expect(
      crear({ ...RESERVA, estado: EstadoReserva.EnUso }).componentInstance.salidaHabilitada()
    ).toBeTrue();
  });

  it('traduce los estados nuevos y muestra el fin acortado con las marcas de asistencia', () => {
    const fixture = crear({
      ...RESERVA,
      estado: EstadoReserva.Finalizada,
      cancelable: false,
      fin: '2026-11-03T10:45:00-05:00',
      fechaCheckIn: '2026-11-03T10:02:00-05:00',
      fechaCheckOut: '2026-11-03T10:45:00-05:00'
    });
    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('Finalizada');
    expect(texto).toContain('10:45');
    expect(texto).toContain('Fin planificado: 11:00');
    expect(texto).toContain('Ingreso: 10:02');
    expect(texto).toContain('Salida: 10:45');
  });

  it('explica la liberación por inasistencia', () => {
    const fixture = crear({
      ...RESERVA,
      estado: EstadoReserva.NoShow,
      cancelable: false,
      canceladaEn: '2026-11-03T10:15:00-05:00'
    });

    expect(fixture.nativeElement.textContent).toContain('No-show');
    expect(fixture.nativeElement.textContent).toContain('Liberada por inasistencia');
  });
});
