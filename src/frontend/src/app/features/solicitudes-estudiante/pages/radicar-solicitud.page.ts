import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { mensajeDeProblema, slugProblema } from '../../../core/errores/mensajes-problema';
import {
  SolicitudRadicada,
  SolicitudRadicacion,
  tamanoLegible
} from '../../../core/solicitudes/solicitudes.models';
import { SolicitudesApi } from '../../../core/solicitudes/solicitudes.api';
import { CalendarioResumen } from '../../administracion/configuracion/components/calendario-resumen';
import { EspacioPublico, TipoAprobacion, TipoModalidadPrestamo } from '../../areas/models/espacio.models';
import { EspaciosApi } from '../../areas/services/espacios.api';

/** Tamaño máximo del pre-informe, espejo de `Solicitudes:TamanoMaximoPdfBytes`. */
const TAMANO_MAXIMO_PDF = 5 * 1024 * 1024;

/** Radicación de una solicitud de área completa con su pre-informe PDF. */
@Component({
  selector: 'app-radicar-solicitud',
  imports: [ReactiveFormsModule, RouterLink, CalendarioResumen],
  templateUrl: './radicar-solicitud.page.html',
  styles: `
    .volver {
      display: inline-block;
      margin-bottom: 1rem;
      color: var(--color-primario);
      text-decoration: none;
    }

    .area {
      margin: 0.25rem 0 0.75rem;
      color: #374151;
    }

    .resumen {
      display: block;
      margin-bottom: 1rem;
    }

    .formulario {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
      gap: 0.9rem;
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
      font-size: 0.9rem;
      color: #374151;
    }

    label.ancho {
      grid-column: 1 / -1;
    }

    input,
    textarea {
      padding: 0.45rem 0.6rem;
      border: 1px solid var(--color-borde);
      border-radius: 0.4rem;
      font: inherit;
    }

    .archivo {
      font-size: 0.8rem;
      color: #6b7280;
    }

    .validacion {
      color: var(--color-error);
      font-size: 0.8rem;
    }

    .acciones {
      grid-column: 1 / -1;
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-top: 0.5rem;
    }

    .acciones button {
      padding: 0.55rem 1.1rem;
      border: none;
      border-radius: 0.4rem;
      background: var(--color-primario);
      color: #fff;
      font: inherit;
      cursor: pointer;
    }

    .acciones button:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .error {
      padding: 0.8rem 1rem;
      border-left: 4px solid var(--color-error);
      background: #fef2f2;
      color: var(--color-error);
      border-radius: 0.25rem;
    }

    .exito {
      padding: 1rem 1.25rem;
      border-left: 4px solid #10b981;
      background: #ecfdf5;
      border-radius: 0.4rem;
    }

    .exito h2 {
      margin: 0 0 0.4rem;
      font-size: 1.1rem;
    }

    .folio {
      font-size: 0.8rem;
      color: #047857;
      word-break: break-all;
    }
  `
})
export class RadicarSolicitudPage implements OnInit {
  private readonly api = inject(EspaciosApi);
  private readonly solicitudes = inject(SolicitudesApi);
  private readonly ruta = inject(ActivatedRoute);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly espacioId = this.ruta.snapshot.paramMap.get('espacioId') ?? '';
  readonly area = signal<EspacioPublico | null>(null);
  readonly error = signal<string | null>(null);
  readonly errorArchivo = signal<string | null>(null);
  readonly archivo = signal<File | null>(null);
  readonly enviando = signal(false);
  readonly radicada = signal<SolicitudRadicada | null>(null);

  readonly formulario = this.formularioReactivo.group({
    materia: ['', [Validators.required, Validators.maxLength(120)]],
    docenteResponsable: ['', [Validators.required, Validators.maxLength(150)]],
    justificacion: ['', [Validators.required, Validators.maxLength(1000)]],
    fecha: ['', [Validators.required]],
    horaInicio: ['14:00', [Validators.required]],
    horaFin: ['16:00', [Validators.required]]
  });

  readonly horasInvalidas = computed(() => {
    const valores = this.formulario.getRawValue();
    return Boolean(valores.horaInicio) && Boolean(valores.horaFin) && valores.horaFin <= valores.horaInicio;
  });

  /** Carga el área y valida que admita solicitudes de área completa. */
  ngOnInit(): void {
    if (!this.espacioId) {
      this.error.set('El área solicitada no existe.');
      return;
    }

    this.api.obtenerPublico(this.espacioId).subscribe({
      next: (area) => {
        this.area.set(area);

        if (
          area.tipoModalidadPrestamo !== TipoModalidadPrestamo.AreaCompleta ||
          area.tipoAprobacion === TipoAprobacion.Automatica
        ) {
          this.error.set('El área no admite solicitudes con pre-informe.');
        }
      },
      error: () => this.error.set('El área solicitada no existe o no está publicada.')
    });
  }

  /** Valida y adjunta el archivo comprobando tipo y tamaño localmente. */
  seleccionarArchivo(evento: Event): void {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0] ?? null;

    this.errorArchivo.set(null);

    if (!archivo) {
      this.archivo.set(null);
      return;
    }

    if (archivo.type !== 'application/pdf') {
      this.archivo.set(null);
      this.errorArchivo.set('El pre-informe debe ser un archivo PDF.');
      return;
    }

    if (archivo.size > TAMANO_MAXIMO_PDF) {
      this.archivo.set(null);
      this.errorArchivo.set('El pre-informe no puede superar los 5 MB.');
      return;
    }

    this.archivo.set(archivo);
  }

  /** Radica la solicitud con `FormData`. */
  radicar(): void {
    this.error.set(null);

    if (this.formulario.invalid || this.horasInvalidas() || !this.archivo()) {
      this.formulario.markAllAsTouched();

      if (!this.archivo()) {
        this.errorArchivo.set('El pre-informe es obligatorio.');
      }

      return;
    }

    const valores = this.formulario.getRawValue();
    const solicitud: SolicitudRadicacion = {
      espacioId: this.espacioId,
      inicio: this.instanteIso(valores.fecha, valores.horaInicio),
      fin: this.instanteIso(valores.fecha, valores.horaFin),
      materia: valores.materia,
      docenteResponsable: valores.docenteResponsable,
      justificacion: valores.justificacion,
      archivo: this.archivo()!
    };

    this.enviando.set(true);

    this.solicitudes.radicar(solicitud).subscribe({
      next: (radicada) => {
        this.enviando.set(false);
        this.radicada.set(radicada);
      },
      error: (error: unknown) => {
        this.enviando.set(false);
        this.error.set(this.mensajeDeError(error));
      }
    });
  }

  /** Indica si la materia muestra su validación local. */
  materiaInvalida(): boolean {
    const control = this.formulario.controls.materia;
    return control.touched && control.invalid;
  }

  /** Indica si el docente muestra su validación local. */
  docenteInvalido(): boolean {
    const control = this.formulario.controls.docenteResponsable;
    return control.touched && control.invalid;
  }

  /** Indica si la justificación muestra su validación local. */
  justificacionInvalida(): boolean {
    const control = this.formulario.controls.justificacion;
    return control.touched && control.invalid;
  }

  /** Tamaño legible del archivo. */
  tamano(bytes: number): string {
    return tamanoLegible(bytes);
  }

  private mensajeDeError(error: unknown): string {
    const slug = slugProblema(error);

    if (slug === 'archivo-demasiado-grande') {
      return 'La API rechazó el archivo por superar el tamaño máximo de 5 MB (413).';
    }

    if (slug === 'archivo-no-valido') {
      return 'La API rechazó el archivo por no ser un PDF válido (415).';
    }

    return mensajeDeProblema(error);
  }

  private instanteIso(fecha: string, hora: string): string {
    const [anio, mes, dia] = fecha.split('-').map((parte) => Number(parte));
    const [horas, minutos] = hora.split(':').map((parte) => Number(parte));
    const referencia = new Date(anio, mes - 1, dia, horas, minutos, 0, 0);
    const desplazamiento = -referencia.getTimezoneOffset();
    const signo = desplazamiento >= 0 ? '+' : '-';
    const absoluto = Math.abs(desplazamiento);
    const offset = `${signo}${String(Math.floor(absoluto / 60)).padStart(2, '0')}:${String(
      absoluto % 60
    ).padStart(2, '0')}`;

    return `${fecha}T${String(horas).padStart(2, '0')}:${String(minutos).padStart(2, '0')}:00${offset}`;
  }
}
