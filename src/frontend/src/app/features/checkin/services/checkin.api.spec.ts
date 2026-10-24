import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { EstadoReserva } from '../../solicitudes-estudiante/models/reserva.models';
import { CheckinApi } from './checkin.api';

const RESERVA = {
  id: 'reserva-1',
  puestoId: 'puesto-1',
  puestoIdentificador: 'PC-07',
  espacioId: 'espacio-1',
  espacioNombre: 'Laboratorio',
  inicio: '2026-11-03T10:00:00-05:00',
  fin: '2026-11-03T11:00:00-05:00',
  finPlanificado: '2026-11-03T11:00:00-05:00',
  estado: EstadoReserva.EnUso,
  creadaEn: '2026-11-02T08:00:00-05:00',
  canceladaEn: null,
  fechaCheckIn: '2026-11-03T10:02:00-05:00',
  fechaCheckOut: null,
  rowVersion: 'AAAA'
};

describe('CheckinApi', () => {
  let api: CheckinApi;
  let control: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    api = TestBed.inject(CheckinApi);
    control = TestBed.inject(HttpTestingController);
  });

  afterEach(() => control.verify());

  it('envía el código QR al endpoint de check-in de la reserva', () => {
    let recibida: unknown = null;
    api.registrarCheckIn('reserva-1', 'QR-7T3K9M2XQ4ZP8V6BN1C5HJ0WRS').subscribe((reserva) => {
      recibida = reserva;
    });

    const solicitud = control.expectOne((peticion) =>
      peticion.url.endsWith('/api/reservas/reserva-1/check-in')
    );

    expect(solicitud.request.method).toBe('POST');
    expect(solicitud.request.body).toEqual({ codigoQr: 'QR-7T3K9M2XQ4ZP8V6BN1C5HJ0WRS' });
    solicitud.flush(RESERVA);

    expect((recibida as { estado: number }).estado).toBe(EstadoReserva.EnUso);
  });

  it('registra la salida sin cuerpo', () => {
    api.registrarCheckOut('reserva-1').subscribe();

    const solicitud = control.expectOne((peticion) =>
      peticion.url.endsWith('/api/reservas/reserva-1/check-out')
    );

    expect(solicitud.request.method).toBe('POST');
    expect(solicitud.request.body).toBeNull();
    solicitud.flush({ ...RESERVA, estado: EstadoReserva.Finalizada, fin: '2026-11-03T10:45:00-05:00' });
  });

  it('propaga el 409 del contrato RFC 7807', () => {
    let estado = 0;
    api.registrarCheckIn('reserva-1', 'QR-00000000000000000000000000').subscribe({
      error: (error: { status: number }) => (estado = error.status)
    });

    control
      .expectOne((peticion) => peticion.url.endsWith('/api/reservas/reserva-1/check-in'))
      .flush(
        {
          type: 'https://api.reservas-espacios.edu/problems/check-in-codigo-invalido',
          title: 'Código de check-in inválido',
          status: 409,
          detail: 'El código QR no corresponde al puesto reservado.'
        },
        { status: 409, statusText: 'Conflict' }
      );

    expect(estado).toBe(409);
  });

  it('localiza la reserva del titular en la agenda paginada', () => {
    let encontrada: { id: string } | null = null;
    api.reserva('reserva-1').subscribe((reserva) => (encontrada = reserva));

    const solicitud = control.expectOne((peticion) => peticion.url.endsWith('/api/reservas/me'));

    expect(solicitud.request.method).toBe('GET');
    expect(solicitud.request.params.get('pagina')).toBe('1');
    expect(solicitud.request.params.get('tamano')).toBe('100');
    solicitud.flush({ elementos: [RESERVA], total: 1, pagina: 1, tamano: 100 });

    expect(encontrada).not.toBeNull();
    expect((encontrada as unknown as { id: string }).id).toBe('reserva-1');
  });
});
