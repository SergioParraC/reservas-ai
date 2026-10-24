import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { CamaraEscaner } from '../components/camara-escaner';
import { CheckinStore } from '../estado/checkin.store';
import { ReservaAsistencia } from '../models/checkin.models';
import { CheckinApi } from '../services/checkin.api';
import { EscanerPage } from './escaner.page';

const CODIGO = 'QR-7T3K9M2XQ4ZP8V6BN1C5HJ0WRS';

const CONFIRMADA: ReservaAsistencia = {
  id: 'reserva-1',
  puestoId: 'puesto-1',
  puestoIdentificador: 'PC-07',
  espacioNombre: 'Laboratorio de Cómputo',
  inicio: '2026-11-03T10:00:00-05:00',
  fin: '2026-11-03T11:00:00-05:00',
  finPlanificado: '2026-11-03T11:00:00-05:00',
  estado: 1,
  fechaCheckIn: null,
  fechaCheckOut: null
};

const EN_USO: ReservaAsistencia = {
  ...CONFIRMADA,
  estado: 4,
  fechaCheckIn: '2026-11-03T10:02:00-05:00'
};

const FINALIZADA: ReservaAsistencia = {
  ...EN_USO,
  estado: 5,
  fin: '2026-11-03T10:45:00-05:00',
  fechaCheckOut: '2026-11-03T10:45:00-05:00'
};

function problema(slug: string, status = 409, detail?: string): HttpErrorResponse {
  return new HttpErrorResponse({
    status,
    error: {
      type: `https://api.reservas-espacios.edu/problems/${slug}`,
      title: 'Conflicto',
      status,
      ...(detail ? { detail } : {})
    }
  });
}

type CasoRechazo = { slug: string; status: number; esperado: string };

const RECHAZOS: CasoRechazo[] = [
  { slug: 'check-in-fuera-de-ventana', status: 409, esperado: 'fuera de la ventana' },
  { slug: 'check-in-codigo-invalido', status: 409, esperado: 'no corresponde al puesto' },
  { slug: 'transicion-invalida', status: 409, esperado: 'no admite esta operación' },
  { slug: 'reserva-ajena', status: 403, esperado: 'pertenece a otro titular' }
];

function configurar(reserva: ReservaAsistencia) {
  const api = jasmine.createSpyObj<CheckinApi>('CheckinApi', [
    'reserva',
    'registrarCheckIn',
    'registrarCheckOut'
  ]);

  api.reserva.and.returnValue(of(reserva));
  api.registrarCheckIn.and.returnValue(of(EN_USO));
  api.registrarCheckOut.and.returnValue(of(FINALIZADA));

  TestBed.configureTestingModule({
    imports: [EscanerPage],
    providers: [
      provideRouter([]),
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ reservaId: 'reserva-1' }) } }
      },
      { provide: CheckinApi, useValue: api }
    ]
  });

  const fixture = TestBed.createComponent(EscanerPage);
  fixture.detectChanges();
  fixture.detectChanges();

  return { api, fixture, tienda: TestBed.inject(CheckinStore) };
}

describe('EscanerPage', () => {
  beforeEach(() => {
    delete (window as { BarcodeDetector?: unknown }).BarcodeDetector;
  });

  it('muestra la franja, la ventana admitida y el escáner de una reserva confirmada', () => {
    const { fixture, api } = configurar(CONFIRMADA);
    const texto = fixture.nativeElement.textContent as string;

    expect(api.reserva).toHaveBeenCalledWith('reserva-1');
    expect(texto).toContain('PC-07');
    expect(texto).toContain('10:00 - 11:00');
    expect(texto).toContain('Fin planificado:');
    expect(texto).toContain('Ventana admitida (±15 min)');
    expect(texto).toContain('09:45 - 10:15');
    expect(texto).toContain('Confirmada');
    expect(fixture.nativeElement.querySelector('app-camara-escaner')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.salida button')).toBeNull();
  });

  it('registra el ingreso con el código transcrito y deja la reserva en uso', () => {
    const { fixture, api, tienda } = configurar(CONFIRMADA);

    const camara = fixture.debugElement.query(By.directive(CamaraEscaner))
      .componentInstance as CamaraEscaner;

    camara.codigoManual = CODIGO;
    fixture.detectChanges();

    const boton = fixture.nativeElement.querySelector('app-camara-escaner button') as HTMLButtonElement;
    boton.click();
    fixture.detectChanges();

    expect(api.registrarCheckIn).toHaveBeenCalledWith('reserva-1', CODIGO);
    expect(tienda.estado()).toBe(4);
    expect(fixture.nativeElement.textContent).toContain('En uso');
    expect(fixture.nativeElement.textContent).toContain('Ingreso registrado');
    expect(fixture.nativeElement.querySelector('app-camara-escaner')).toBeNull();
  });

  it('la salida está siempre habilitada mientras la reserva siga en uso', () => {
    const { fixture, api } = configurar(EN_USO);
    const boton = fixture.nativeElement.querySelector('.salida button') as HTMLButtonElement;

    expect(boton).not.toBeNull();
    expect(boton.disabled).toBeFalse();

    boton.click();
    fixture.detectChanges();

    expect(api.registrarCheckOut).toHaveBeenCalledWith('reserva-1');
  });

  it('tras la salida anticipada informa el remanente liberado', () => {
    const { fixture } = configurar(EN_USO);
    const boton = fixture.nativeElement.querySelector('.salida button') as HTMLButtonElement;

    boton.click();
    fixture.detectChanges();

    const texto = fixture.nativeElement.textContent as string;

    expect(texto).toContain('Finalizada');
    expect(texto).toContain('Salida registrada a las 10:45');
    expect(texto).toContain('Se liberaron 15 minuto(s)');
    expect(texto).toContain('Ver disponibilidad del puesto');
    expect(fixture.nativeElement.querySelector('.salida button')).toBeNull();
  });

  for (const caso of RECHAZOS) {
    it(`muestra el rechazo ${caso.status} ${caso.slug} sin cambiar el estado`, () => {
      const { fixture, api } = configurar(CONFIRMADA);
      api.registrarCheckIn.and.returnValue(throwError(() => problema(caso.slug, caso.status)));

      fixture.componentInstance.registrarIngreso(CODIGO);
      fixture.detectChanges();

      const texto = fixture.nativeElement.textContent as string;

      expect(texto).toContain(caso.esperado);
      expect(texto).toContain('Confirmada');
    });
  }
});
