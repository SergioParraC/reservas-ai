/** Sesión abierta que ocupa un puesto en el instante consultado. */
export interface SesionEnCurso {
  reservaId: string;
  puestoIdentificador: string;
  titular: string;
  inicio: string;
  fin: string;
  fechaCheckIn: string | null;
}

/** Desglose de ocupación de un área en el instante consultado. */
export interface AreaOcupacion {
  espacioId: string;
  nombre: string;
  aforo: number;
  puestosActivos: number;
  enUso: number;
  pendientesCheckIn: number;
  sesionesSinCerrar: number;
  libres: number;
  porcentajeOcupacion: number;
  proximaLiberacion: string | null;
  enCurso: SesionEnCurso[];
}

/** Totales institucionales del mapa de ocupación. */
export interface TotalesOcupacion {
  aforo: number;
  puestosActivos: number;
  enUso: number;
  pendientesCheckIn: number;
  sesionesSinCerrar: number;
  libres: number;
  porcentajeOcupacion: number;
}

/** Respuesta de `GET /api/monitoreo/ocupacion-actual`. */
export interface OcupacionActual {
  generadoEn: string;
  toleranciaMinutos: number;
  totales: TotalesOcupacion;
  areas: AreaOcupacion[];
}

/** Estado visual de un puesto o de una sesión en el tablero. */
export type EstadoPuesto = 'libre' | 'en-uso' | 'pendiente' | 'no-show';

/** Etiquetas de la leyenda del tablero, en el orden en que se muestran. */
export const ESTADOS_TABLERO: ReadonlyArray<{ estado: EstadoPuesto; etiqueta: string }> = [
  { estado: 'libre', etiqueta: 'Libre' },
  { estado: 'en-uso', etiqueta: 'En uso' },
  { estado: 'pendiente', etiqueta: 'Pendiente' },
  { estado: 'no-show', etiqueta: 'No-show' }
];
