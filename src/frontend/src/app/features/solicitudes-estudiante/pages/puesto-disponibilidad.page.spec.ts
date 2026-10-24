import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { ReservasStore } from '../estado/reservas.store';
import {
  DisponibilidadPuesto,
  EstadoReserva,
  ReservaDetalle
} from '../models/reserva.models';
import { PuestoDisponibilidadPage } from './puesto-disponibilidad.page';

const DISPONIBILIDAD: DisponibilidadPuesto = {
  puestoId: 'puesto-1',
  identificador: 'PC-01',
  espacioId: 'espacio-1',
  espacioNombre: 'Sala de Cómputo 1',
  zonaHoraria: 'America/Bogota',
  franjas: [
    {
      inicio: '2026-11-03T10:00:00-05:00',
      fin: '2026-11-03T11:00:00-05:00',
      disponible: true,
      motivo: null
    },
    {
      inicio: '2026-11-03T11:00:00-05:00',
      fin: '2026-11-03T12:00:00-05:00',
      disponible: false,
      motivo: 'Ocupada'
    }
  ]
};

const RESERVA: ReservaDetalle = {
  id: 'reserva-1',
  puestoId: 'puesto-1',
  puestoIdentificador: 'PC-01',
  espacioId: 'espacio-1',
  espacioNombre: 'Sala de Cómputo 1',
  inicio: '2026-11-03T10:00:00-05:00',
  fin: '2026-11-03T11:00:00-05:00',
  finPlanificado: '2026-11-03T11:00:00-05:00',
  estado: EstadoReserva.Confirmada,
  creadaEn: '2026-11-02T08:00:00-05:00',
  canceladaEn: null,
  fechaCheckIn: null,
  fechaCheckOut: null,
  rowVersion: 'AAAA'
};

function configurar(disponibilidad: DisponibilidadPuesto | null, error: string | null = null) {
  const tienda = {
    disponibilidad: signal(disponibilidad),
    cargando: signal(false),
    error: signal<string | null>(error),
    cargarDisponibilidad: jasmine.createSpy('cargarDisponibilidad').and.returnValue(of(disponibilidad)),
    reservar: jasmine.createSpy('reservar').and.returnValue(of(RESERVA))
  };

  TestBed.configureTestingModule({
    imports: [PuestoDisponibilidadPage],
    providers: [
      provideRouter([]),
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ puestoId: 'puesto-1' }) } }
      },
      { provide: ReservasStore, useValue: tienda }
    ]
  });

  return tienda;
}

describe('PuestoDisponibilidadPage', () => {
  it('muestra las franjas y deshabilita la ocupada', () => {
    const tienda = configurar(DISPONIBILIDAD);
    const fixture = TestBed.createComponent(PuestoDisponibilidadPage);
    fixture.detectChanges();

    const botones = fixture.nativeElement.querySelectorAll('.franja');

    expect(tienda.cargarDisponibilidad).toHaveBeenCalledWith('puesto-1', jasmine.objectContaining({ duracionMinutos: 60 }));
    expect(botones.length).toBe(2);
    expect(botones[1].disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Sala de Cómputo 1');
  });

  it('confirma la reserva de la franja elegida y muestra el resultado', () => {
    const tienda = configurar(DISPONIBILIDAD);
    const fixture = TestBed.createComponent(PuestoDisponibilidadPage);
    fixture.detectChanges();

    (fixture.nativeElement.querySelectorAll('.franja')[0] as HTMLButtonElement).click();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.confirmar') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(tienda.reservar).toHaveBeenCalledWith({
      puestoId: 'puesto-1',
      inicio: DISPONIBILIDAD.franjas[0].inicio,
      fin: DISPONIBILIDAD.franjas[0].fin
    });
    expect(fixture.nativeElement.textContent).toContain('Reserva confirmada');
    expect(fixture.nativeElement.textContent).toContain('Ver mis reservas');
  });

  it('bloquea la confirmación sin franja seleccionada', () => {
    configurar(DISPONIBILIDAD);
    const fixture = TestBed.createComponent(PuestoDisponibilidadPage);
    fixture.detectChanges();

    const confirmar = fixture.nativeElement.querySelector('.confirmar') as HTMLButtonElement;

    expect(confirmar.disabled).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Seleccione una franja disponible.');
  });

  it('muestra el mensaje del conflicto devuelto por la API', () => {
    configurar(null, 'La franja ya fue ocupada por otra persona.');
    const fixture = TestBed.createComponent(PuestoDisponibilidadPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('La franja ya fue ocupada por otra persona.');
  });

  it('muestra el mensaje de cupo agotado', () => {
    configurar(null, 'Alcanzó el máximo de reservas activas. Cancele una reserva para continuar.');
    const fixture = TestBed.createComponent(PuestoDisponibilidadPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Alcanzó el máximo de reservas activas');
  });
});
