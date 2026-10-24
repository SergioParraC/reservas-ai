import { HttpErrorResponse } from '@angular/common/http';

import {
  avisoRemanenteLiberado,
  formatearVentanaCheckIn,
  mensajeDeErrorCheckIn
} from './reserva.mapper';
import { calcularVentanaCheckIn, dentroDeVentana } from '../../checkin/models/checkin.models';
import {
  EstadoReserva,
  etiquetaEstadoReserva,
  puedeRegistrarIngreso,
  puedeRegistrarSalida
} from '../models/reserva.models';

function problema(slug: string): HttpErrorResponse {
  return new HttpErrorResponse({
    status: 409,
    error: {
      type: `https://api.reservas-espacios.edu/problems/${slug}`,
      title: 'Conflicto',
      status: 409,
      detail: `Detalle de ${slug}.`
    }
  });
}

describe('reserva.mapper · asistencia', () => {
  it('calcula la ventana simétrica de check-in y respeta sus extremos', () => {
    const ventana = calcularVentanaCheckIn('2026-11-03T10:00:00-05:00', 15);

    expect(formatearVentanaCheckIn(ventana.desde, ventana.hasta)).toBe('09:45 - 10:15');
    expect(ventana.toleranciaMinutos).toBe(15);
    expect(dentroDeVentana(ventana, new Date('2026-11-03T09:45:00-05:00'))).toBeTrue();
    expect(dentroDeVentana(ventana, new Date('2026-11-03T10:15:00-05:00'))).toBeTrue();
    expect(dentroDeVentana(ventana, new Date('2026-11-03T09:44:00-05:00'))).toBeFalse();
    expect(dentroDeVentana(ventana, new Date('2026-11-03T10:16:00-05:00'))).toBeFalse();
  });

  it('traduce la tolerancia configurada en la ventana mostrada', () => {
    const diez = calcularVentanaCheckIn('2026-11-03T10:00:00-05:00', 10);
    const veinte = calcularVentanaCheckIn('2026-11-03T10:00:00-05:00', 20);

    expect(formatearVentanaCheckIn(diez.desde, diez.hasta)).toBe('09:50 - 10:10');
    expect(formatearVentanaCheckIn(veinte.desde, veinte.hasta)).toBe('09:40 - 10:20');
  });

  it('describe el remanente liberado tras una salida anticipada', () => {
    const aviso = avisoRemanenteLiberado(
      '2026-11-03T10:00:00-05:00',
      '2026-11-03T10:45:00-05:00',
      '2026-11-03T11:00:00-05:00'
    );

    expect(aviso).toContain('Salida registrada a las 10:45');
    expect(aviso).toContain('Se liberaron 15 minuto(s)');
    expect(aviso).toContain('10:00 - 10:45');
    expect(aviso).toContain('planificada hasta las 11:00');
  });

  it('no informa remanente cuando la franja se agotó', () => {
    const aviso = avisoRemanenteLiberado(
      '2026-11-03T10:00:00-05:00',
      '2026-11-03T11:00:00-05:00',
      '2026-11-03T11:00:00-05:00'
    );

    expect(aviso).toContain('no queda remanente');
  });

  it('traduce los rechazos del contrato a mensajes accionables', () => {
    expect(mensajeDeErrorCheckIn(problema('check-in-fuera-de-ventana'))).toBe(
      'Detalle de check-in-fuera-de-ventana.'
    );
    expect(mensajeDeErrorCheckIn(problema('check-in-codigo-invalido'))).toContain(
      'no corresponde al puesto'
    );
    expect(mensajeDeErrorCheckIn(problema('reserva-ajena'))).toContain('otro titular');
    expect(mensajeDeErrorCheckIn(problema('transicion-invalida'))).toContain(
      'Detalle de transicion-invalida'
    );
    expect(mensajeDeErrorCheckIn(new Error('sin red'))).toContain('No fue posible registrar');
  });

  it('etiqueta los cinco estados y resuelve las acciones del flujo presencial', () => {
    expect(etiquetaEstadoReserva(EstadoReserva.Confirmada)).toBe('Confirmada');
    expect(etiquetaEstadoReserva(EstadoReserva.Cancelada)).toBe('Cancelada');
    expect(etiquetaEstadoReserva(EstadoReserva.NoShow)).toBe('No-show');
    expect(etiquetaEstadoReserva(EstadoReserva.EnUso)).toBe('En uso');
    expect(etiquetaEstadoReserva(EstadoReserva.Finalizada)).toBe('Finalizada');

    expect(puedeRegistrarIngreso(EstadoReserva.Confirmada)).toBeTrue();
    expect(puedeRegistrarIngreso(EstadoReserva.EnUso)).toBeFalse();
    expect(puedeRegistrarSalida(EstadoReserva.EnUso)).toBeTrue();
    expect(puedeRegistrarSalida(EstadoReserva.Finalizada)).toBeFalse();
  });
});
