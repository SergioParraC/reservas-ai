import { TestBed, discardPeriodicTasks, fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';

import { CamaraEscaner } from './camara-escaner';

const CODIGO = 'QR-7T3K9M2XQ4ZP8V6BN1C5HJ0WRS';

type DetectorGlobal = {
  BarcodeDetector?: new (opciones?: { formats?: string[] }) => {
    detect(fuente: unknown): Promise<Array<{ rawValue: string }>>;
  };
};

/** Instala un detector simulado que devuelve el código indicado en cada fotograma. */
function instalarDetector(codigo: string | null): jasmine.Spy {
  const detect = jasmine.createSpy('detect').and.resolveTo(codigo ? [{ rawValue: codigo }] : []);
  const constructor = function () {
    return { detect };
  } as unknown as NonNullable<Window['BarcodeDetector']>;

  (window as DetectorGlobal).BarcodeDetector = constructor;

  return detect;
}

function quitarDetector(): void {
  delete (window as DetectorGlobal).BarcodeDetector;
}

/** Flujo falso con una pista que registra su liberación. */
function flujoFalso(pista: jasmine.SpyObj<MediaStreamTrack>): MediaStream {
  return {
    getTracks: () => [pista],
    getVideoTracks: () => [pista]
  } as unknown as MediaStream;
}

function espiaCamara(resultado: () => Promise<MediaStream>): jasmine.Spy {
  if (!navigator.mediaDevices) {
    Object.defineProperty(navigator, 'mediaDevices', {
      value: {} as MediaDevices,
      configurable: true
    });
  }

  return spyOn(navigator.mediaDevices, 'getUserMedia').and.callFake(resultado);
}

describe('CamaraEscaner', () => {
  afterEach(() => {
    quitarDetector();
    localStorage.clear();
  });

  it('sin BarcodeDetector mantiene la entrada manual y lo explica', () => {
    quitarDetector();

    const fixture = TestBed.createComponent(CamaraEscaner);
    fixture.detectChanges();
    fixture.detectChanges();

    const componente = fixture.componentInstance;
    const emitidos: string[] = [];
    componente.codigo.subscribe((codigo) => emitidos.push(codigo));

    expect(componente.apiDisponible()).toBeFalse();
    expect(componente.camaraActiva()).toBeFalse();
    expect(componente.mensajeCamara()).toContain('no admite la lectura de QR con cámara');

    componente.codigoManual = `  ${CODIGO}  `;
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();

    expect(emitidos).toEqual([CODIGO]);
    expect(componente.codigoManual).toBe('');
  });

  it('lee el QR con la cámara trasera y libera las pistas al destruirse', fakeAsync(() => {
    const pista = jasmine.createSpyObj<MediaStreamTrack>('MediaStreamTrack', ['stop']);
    const detect = instalarDetector(CODIGO);
    const camara = espiaCamara(() => Promise.resolve(flujoFalso(pista)));

    const fixture = TestBed.createComponent(CamaraEscaner);
    const emitidos: string[] = [];
    fixture.componentInstance.codigo.subscribe((codigo) => emitidos.push(codigo));

    fixture.detectChanges();
    flushMicrotasks();
    fixture.detectChanges();

    expect(camara).toHaveBeenCalledWith({ video: { facingMode: 'environment' }, audio: false });
    expect(fixture.componentInstance.camaraActiva()).toBeTrue();

    tick(500);
    flushMicrotasks();

    expect(detect).toHaveBeenCalled();
    expect(emitidos).toEqual([CODIGO]);

    tick(500);
    flushMicrotasks();

    expect(emitidos.length).toBe(1);

    fixture.destroy();
    discardPeriodicTasks();

    expect(pista.stop).toHaveBeenCalled();
  }));

  it('con el permiso denegado explica el motivo y conserva el código manual', fakeAsync(() => {
    instalarDetector(CODIGO);
    espiaCamara(() => Promise.reject(new DOMException('denegado', 'NotAllowedError')));

    const fixture = TestBed.createComponent(CamaraEscaner);
    fixture.detectChanges();
    flushMicrotasks();
    fixture.detectChanges();

    const componente = fixture.componentInstance;

    expect(componente.camaraActiva()).toBeFalse();
    expect(componente.mensajeCamara()).toContain('Permiso de cámara denegado');
    expect(fixture.nativeElement.querySelector('input')).not.toBeNull();

    discardPeriodicTasks();
    fixture.destroy();
  }));

  it('sin cámara física informa que no se encontró un dispositivo', fakeAsync(() => {
    instalarDetector(CODIGO);
    espiaCamara(() => Promise.reject(new DOMException('sin cámara', 'NotFoundError')));

    const fixture = TestBed.createComponent(CamaraEscaner);
    fixture.detectChanges();
    flushMicrotasks();
    fixture.detectChanges();

    expect(fixture.componentInstance.mensajeCamara()).toContain('No se encontró una cámara');

    discardPeriodicTasks();
    fixture.destroy();
  }));
});
