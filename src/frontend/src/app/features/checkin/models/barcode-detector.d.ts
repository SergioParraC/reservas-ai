/**
 * Tipado mínimo de la API nativa `BarcodeDetector` (Chromium). No se instala ninguna dependencia
 * npm: la detección de capacidad se hace con `'BarcodeDetector' in window` y, cuando falta, la
 * pantalla ofrece la entrada manual del código.
 */
interface DetectedBarcode {
  rawValue: string;
  format: string;
  boundingBox: DOMRectReadOnly;
}

interface BarcodeDetectorOptions {
  formats?: string[];
}

interface BarcodeDetector {
  detect(fuente: ImageBitmapSource): Promise<DetectedBarcode[]>;
}

interface BarcodeDetectorConstructor {
  new (opciones?: BarcodeDetectorOptions): BarcodeDetector;
  getSupportedFormats?(): Promise<string[]>;
}

interface Window {
  /** Constructor nativo del detector de códigos; ausente en Firefox y Safari. */
  BarcodeDetector?: BarcodeDetectorConstructor;
}
