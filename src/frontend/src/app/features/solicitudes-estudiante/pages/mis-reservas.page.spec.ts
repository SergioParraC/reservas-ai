import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { ReservasStore } from '../estado/reservas.store';
import { EstadoReserva, PaginaReservas } from '../models/reserva.models';
import { MisReservasPage } from './mis-reservas.page';

const PAGINA: PaginaReservas = {
  elementos: [
    {
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
    },
    {
      id: 'reserva-2',
      puestoId: 'puesto-2',
      puestoIdentificador: 'PC-02',
      espacioNombre: 'Sala de Cómputo 1',
      inicio: '2026-11-03T11:00:00-05:00',
      fin: '2026-11-03T12:00:00-05:00',
      estado: EstadoReserva.Confirmada,
      creadaEn: '2026-11-02T08:00:00-05:00',
      canceladaEn: null,
      cancelable: false
    }
  ],
  total: 2,
  pagina: 1,
  tamano: 10
};

function configurar(error: string | null = null, aviso: string | null = null) {
  const tienda = {
    reservas: signal<PaginaReservas | null>(PAGINA),
    cargandoReservas: signal(false),
    error: signal<string | null>(error),
    aviso: signal<string | null>(aviso),
    cancelando: signal<string | null>(null),
    totalPaginas: signal(1),
    cargarMisReservas: jasmine.createSpy('cargarMisReservas').and.returnValue(of(PAGINA)),
    cancelar: jasmine.createSpy('cancelar').and.returnValue(of(true))
  };

  TestBed.configureTestingModule({
    imports: [MisReservasPage],
    providers: [provideRouter([]), { provide: ReservasStore, useValue: tienda }]
  });

  return tienda;
}

describe('MisReservasPage', () => {
  it('lista las reservas y solo ofrece cancelar las que están en plazo', () => {
    configurar();
    const fixture = TestBed.createComponent(MisReservasPage);
    fixture.detectChanges();

    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('PC-01');
    expect(texto).toContain('PC-02');
    expect(texto).toContain('2 reserva(s)');
    expect(texto).toContain('Ya no puede cancelarse');
    expect(fixture.nativeElement.querySelectorAll('button').length).toBe(3);
  });

  it('cancela con confirmación y recarga la agenda', () => {
    const tienda = configurar();
    spyOn(window, 'confirm').and.returnValue(true);

    const fixture = TestBed.createComponent(MisReservasPage);
    fixture.detectChanges();

    const botonCancelar = fixture.nativeElement.querySelector('app-reserva-card button') as HTMLButtonElement;
    botonCancelar.click();

    expect(window.confirm).toHaveBeenCalled();
    expect(tienda.cancelar).toHaveBeenCalledWith('reserva-1');
    expect(tienda.cargarMisReservas).toHaveBeenCalledTimes(2);
  });

  it('no cancela cuando el usuario descarta la confirmación', () => {
    const tienda = configurar();
    spyOn(window, 'confirm').and.returnValue(false);

    const fixture = TestBed.createComponent(MisReservasPage);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('app-reserva-card button') as HTMLButtonElement).click();

    expect(tienda.cancelar).not.toHaveBeenCalled();
  });

  it('muestra el mensaje de plazo vencido devuelto por la API', () => {
    configurar('La reserva ya no puede cancelarse: falta menos del plazo mínimo.');
    const fixture = TestBed.createComponent(MisReservasPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('falta menos del plazo mínimo');
  });

  it('muestra el mensaje de reserva ajena devuelto por la API', () => {
    configurar('La reserva pertenece a otro usuario.');
    const fixture = TestBed.createComponent(MisReservasPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('pertenece a otro usuario');
  });

  it('muestra el aviso de cancelación exitosa', () => {
    configurar(null, 'Reserva cancelada. La franja vuelve a estar disponible.');
    const fixture = TestBed.createComponent(MisReservasPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Reserva cancelada');
  });
});
