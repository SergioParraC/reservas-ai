import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { leerProblema } from '../../../../core/interceptors/problem-details.interceptor';
import { PlantillaQrPanel } from '../../components/plantilla-qr-panel';
import { EspacioDetalle, EstadoUnidad } from '../../models/espacio.models';
import { EspaciosApi } from '../../services/espacios.api';

/** Alta y baja de puestos individuales de un área. */
@Component({
  selector: 'app-puestos',
  imports: [ReactiveFormsModule, RouterLink, PlantillaQrPanel],
  templateUrl: './puestos.page.html',
  styleUrl: './puestos.page.css'
})
export class PuestosPage implements OnInit {
  private readonly api = inject(EspaciosApi);
  private readonly ruta = inject(ActivatedRoute);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly areaId = signal<string | null>(this.ruta.snapshot.paramMap.get('id'));
  readonly area = signal<EspacioDetalle | null>(null);
  readonly cargando = signal(false);
  readonly agregando = signal(false);
  readonly conflicto = signal<string | null>(null);
  readonly mensaje = signal<string | null>(null);
  readonly erroresCampo = signal<Record<string, string[]>>({});
  readonly estadoActivo = EstadoUnidad.Activo;
  readonly idsPlantilla = computed<string[]>(() => {
    const id = this.areaId();

    return id ? [id] : [];
  });

  readonly formulario = this.formularioReactivo.group({
    identificador: ['', [Validators.required, Validators.maxLength(50)]],
    descripcionEquipo: ['', [Validators.maxLength(150)]]
  });

  /** Carga el área con sus puestos. */
  ngOnInit(): void {
    this.cargar();
  }

  /** Crea un puesto y refresca el listado. */
  agregar(): void {
    if (this.formulario.invalid) {
      this.formulario.markAllAsTouched();
      return;
    }

    const id = this.areaId();

    if (!id) {
      return;
    }

    const valores = this.formulario.getRawValue();

    this.agregando.set(true);
    this.conflicto.set(null);
    this.mensaje.set(null);
    this.erroresCampo.set({});

    this.api
      .crearPuesto(id, {
        identificador: valores.identificador,
        descripcionEquipo: valores.descripcionEquipo || null
      })
      .subscribe({
        next: (puesto) => {
          this.agregando.set(false);
          this.mensaje.set(`Puesto ${puesto.identificador} registrado con su código QR.`);
          this.formulario.reset({ identificador: '', descripcionEquipo: '' });
          this.cargar();
        },
        error: (error: unknown) => this.manejarError(error)
      });
  }

  /** Da de baja lógica el puesto y refresca el listado. */
  desactivar(puestoId: string): void {
    const id = this.areaId();

    if (!id) {
      return;
    }

    this.erroresCampo.set({});
    this.conflicto.set(null);

    this.api.desactivarPuesto(id, puestoId).subscribe({
      next: () => {
        this.mensaje.set('Puesto dado de baja. Su código QR se conserva.');
        this.cargar();
      },
      error: (error: unknown) => this.manejarError(error)
    });
  }

  /** Primer error del servidor asociado al campo. */
  errorDe(campo: string): string | null {
    return this.erroresCampo()[campo]?.[0] ?? null;
  }

  /** Indica si el identificador debe mostrar su validación local. */
  identificadorInvalido(): boolean {
    const control = this.formulario.controls.identificador;
    return control.touched && control.invalid;
  }

  private cargar(): void {
    const id = this.areaId();

    if (!id) {
      return;
    }

    this.cargando.set(true);

    this.api.obtener(id).subscribe({
      next: (area) => {
        this.cargando.set(false);
        this.area.set(area);
      },
      error: (error: unknown) => {
        this.cargando.set(false);
        this.manejarError(error);
      }
    });
  }

  private manejarError(error: unknown): void {
    this.agregando.set(false);

    if (!(error instanceof HttpErrorResponse)) {
      return;
    }

    const problema = leerProblema(error);

    if (error.status === 409) {
      this.conflicto.set(problema?.detail ?? 'El puesto entra en conflicto con el aforo del área.');
      return;
    }

    if (problema?.errors) {
      this.erroresCampo.set(problema.errors);
    }
  }
}
