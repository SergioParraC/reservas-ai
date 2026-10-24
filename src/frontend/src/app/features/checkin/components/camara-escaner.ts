import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  EventEmitter,
  OnDestroy,
  Output,
  ViewChild,
  inject,
  signal
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { interval } from 'rxjs';

/** Cadencia de la lectura de fotogramas; suficiente para una placa estática. */
const INTERVALO_LECTURA_MS = 400;

/** Ventana mínima entre dos emisiones del mismo código para no repetir el check-in. */
const ESPERA_ENTRE_EMISIONES_MS = 3000;

/**
 * Lectura del QR de la placa con la API nativa del navegador y respaldo de entrada manual.
 * Si `BarcodeDetector` no existe, si el contexto no es seguro o si el permiso se deniega, la
 * pantalla mantiene el campo de código y explica el motivo sin bloquear el flujo.
 */
@Component({
  selector: 'app-camara-escaner',
  imports: [FormsModule],
  template: `
    <section class="escaner">
      <video #video class="visor" [class.oculto]="!camaraActiva()" autoplay muted playsinline></video>

      @if (camaraActiva()) {
        <p class="pista" role="status">Apunte la cámara al QR impreso en la placa del puesto.</p>
      }

      @if (mensajeCamara(); as mensaje) {
        <p class="aviso-camara" role="alert">{{ mensaje }}</p>
      }

      <form class="manual" (ngSubmit)="usarCodigoManual()">
        <label>
          Código de la placa
          <input
            name="codigo"
            type="text"
            autocomplete="off"
            spellcheck="false"
            placeholder="QR-..."
            [(ngModel)]="codigoManual"
          />
        </label>
        <button type="submit" [disabled]="codigoManual.trim().length === 0" (click)="usarCodigoManual()">
          Usar código
        </button>
      </form>

      <p class="nota">
        ¿La cámara no lee el código? Escríbalo tal como aparece impreso en la placa del puesto.
      </p>
    </section>
  `,
  styles: `
    .escaner {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      padding: 1rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.75rem;
      background: #fff;
    }

    .visor {
      width: 100%;
      max-height: 18rem;
      border-radius: 0.5rem;
      background: #111827;
      object-fit: cover;
    }

    .visor.oculto {
      display: none;
    }

    .pista,
    .nota {
      margin: 0;
      color: #6b7280;
      font-size: 0.85rem;
    }

    .aviso-camara {
      margin: 0;
      padding: 0.6rem 0.8rem;
      border-left: 4px solid #f59e0b;
      background: #fffbeb;
      color: #92400e;
      border-radius: 0.25rem;
      font-size: 0.85rem;
    }

    .manual {
      display: flex;
      align-items: end;
      flex-wrap: wrap;
      gap: 0.75rem;
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      font-size: 0.9rem;
      color: #374151;
      flex: 1 1 12rem;
    }

    input {
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      font: inherit;
    }

    button {
      padding: 0.45rem 0.9rem;
      border: 1px solid var(--color-primario);
      border-radius: 0.4rem;
      background: #fff;
      color: var(--color-primario);
      font: inherit;
      cursor: pointer;
    }

    button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `
})
export class CamaraEscaner implements AfterViewInit, OnDestroy {
  private readonly destroyRef = inject(DestroyRef);

  @ViewChild('video') private video?: ElementRef<HTMLVideoElement>;

  /** Código leído por la cámara o transcrito a mano. */
  @Output() readonly codigo = new EventEmitter<string>();

  /** Indica si el navegador expone `BarcodeDetector`. */
  readonly apiDisponible = signal(
    typeof window !== 'undefined' && typeof window.BarcodeDetector === 'function'
  );

  /** Indica si la cámara está encendida y el detector listo. */
  readonly camaraActiva = signal(false);

  /** Mensaje accionable cuando la cámara o la API no están disponibles. */
  readonly mensajeCamara = signal<string | null>(null);

  codigoManual = '';

  private flujo: MediaStream | null = null;
  private detector: BarcodeDetector | null = null;
  private leyendo = false;
  private ultimoCodigo: string | null = null;
  private ultimaEmision = 0;

  /** Arranca la cámara después del primer render para que el `<video>` exista. */
  ngAfterViewInit(): void {
    void this.iniciar();
  }

  /** Libera las pistas del `MediaStream` al salir de la pantalla. */
  ngOnDestroy(): void {
    this.liberarPistas();
  }

  /** Emite el código transcrito y limpia el campo. */
  usarCodigoManual(): void {
    const codigo = this.codigoManual.trim();

    if (codigo.length === 0) {
      return;
    }

    this.codigoManual = '';
    this.emitir(codigo);
  }

  private async iniciar(): Promise<void> {
    if (!this.apiDisponible()) {
      this.mensajeCamara.set(
        'Este navegador no admite la lectura de QR con cámara. Escriba el código de la placa.'
      );
      return;
    }

    try {
      if (!navigator.mediaDevices?.getUserMedia) {
        throw new Error('media-devices-no-disponible');
      }

      this.flujo = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment' },
        audio: false
      });

      this.adjuntarFlujo();
      this.detector = new window.BarcodeDetector!({ formats: ['qr_code'] });
      this.camaraActiva.set(true);
      this.mensajeCamara.set(null);

      interval(INTERVALO_LECTURA_MS)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(() => void this.detectar());
    } catch (error) {
      this.camaraActiva.set(false);
      this.mensajeCamara.set(this.mensajeDeCamara(error));
      this.liberarPistas();
    }
  }

  private adjuntarFlujo(): void {
    if (!this.video || !this.flujo) {
      return;
    }

    const elemento = this.video.nativeElement;

    try {
      elemento.srcObject = this.flujo;
      void elemento.play().catch(() => undefined);
    } catch {
      // Un navegador sin soporte de `srcObject` deja el flujo inactivo: la entrada manual sigue.
    }
  }

  private async detectar(): Promise<void> {
    if (!this.detector || !this.video || this.leyendo) {
      return;
    }

    this.leyendo = true;

    try {
      const codigos = await this.detector.detect(this.video.nativeElement);
      const valor = codigos
        .map((codigo) => codigo.rawValue?.trim() ?? '')
        .find((texto) => texto.length > 0);

      if (valor) {
        this.emitir(valor);
      }
    } catch {
      // Fotograma no disponible (cámara arrancando): se reintenta en el siguiente ciclo.
    } finally {
      this.leyendo = false;
    }
  }

  private emitir(codigo: string): void {
    const ahora = Date.now();
    const repetido =
      codigo === this.ultimoCodigo && ahora - this.ultimaEmision < ESPERA_ENTRE_EMISIONES_MS;

    if (repetido) {
      return;
    }

    this.ultimoCodigo = codigo;
    this.ultimaEmision = ahora;
    this.codigo.emit(codigo);
  }

  private mensajeDeCamara(error: unknown): string {
    const nombre = error instanceof DOMException ? error.name : '';

    switch (nombre) {
      case 'NotAllowedError':
      case 'SecurityError':
        return 'Permiso de cámara denegado. Escriba el código de la placa o habilite la cámara en el navegador.';
      case 'NotFoundError':
        return 'No se encontró una cámara disponible. Escriba el código de la placa.';
      case 'NotReadableError':
        return 'La cámara está en uso por otra aplicación. Escriba el código de la placa.';
      default:
        return 'No fue posible iniciar la cámara. Escriba el código de la placa para continuar.';
    }
  }

  private liberarPistas(): void {
    const pistas = this.flujo?.getTracks() ?? [];
    pistas.forEach((pista) => pista.stop());

    if (this.video) {
      try {
        this.video.nativeElement.srcObject = null;
      } catch {
        // Sin soporte de `srcObject` no hay nada que liberar en el elemento.
      }
    }

    this.flujo = null;
    this.detector = null;
    this.camaraActiva.set(false);
  }
}
