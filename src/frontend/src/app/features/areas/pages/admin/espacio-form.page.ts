import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { leerProblema } from '../../../../core/interceptors/problem-details.interceptor';
import { PlantillaQrPanel } from '../../components/plantilla-qr-panel';
import {
  AFORO_MAXIMO,
  CATEGORIAS,
  CategoriaEspacio,
  ESTADOS,
  EstadoUnidad,
  TIPOS_APROBACION,
  TipoAprobacion
} from '../../models/espacio.models';
import { EspaciosApi } from '../../services/espacios.api';

/** Alta y edición de un área con validadores espejo del dominio. */
@Component({
  selector: 'app-espacio-form',
  imports: [ReactiveFormsModule, RouterLink, PlantillaQrPanel],
  templateUrl: './espacio-form.page.html',
  styleUrl: './espacio-form.page.css'
})
export class EspacioFormPage implements OnInit {
  private readonly api = inject(EspaciosApi);
  private readonly router = inject(Router);
  private readonly ruta = inject(ActivatedRoute);
  private readonly formularioReactivo = inject(FormBuilder).nonNullable;

  readonly categorias = CATEGORIAS;
  readonly tiposAprobacion = TIPOS_APROBACION;
  readonly estados = ESTADOS;
  readonly aforoMaximo = AFORO_MAXIMO;

  readonly areaId = signal<string | null>(this.ruta.snapshot.paramMap.get('id'));
  readonly editando = computed(() => this.areaId() !== null);
  readonly idsPlantilla = computed<string[]>(() => {
    const id = this.areaId();

    return id ? [id] : [];
  });

  readonly cargando = signal(false);
  readonly guardando = signal(false);
  readonly conflicto = signal<string | null>(null);
  readonly erroresCampo = signal<Record<string, string[]>>({});
  readonly fotografiaUrl = signal<string | null>(null);
  private readonly rowVersion = signal<string | null>(null);

  readonly formulario = this.formularioReactivo.group({
    nombre: ['', [Validators.required, Validators.maxLength(100)]],
    ubicacion: ['', [Validators.required, Validators.maxLength(150)]],
    categoria: [CategoriaEspacio.Laboratorio, [Validators.required]],
    aforo: [10, [Validators.required, Validators.min(1), Validators.max(AFORO_MAXIMO)]],
    tipoAprobacion: [TipoAprobacion.Docente, [Validators.required]],
    estado: [EstadoUnidad.Activo, [Validators.required]]
  });

  /** Carga los datos del área cuando la ruta es de edición. */
  ngOnInit(): void {
    const id = this.areaId();

    if (!id) {
      return;
    }

    this.cargando.set(true);

    this.api.obtener(id).subscribe({
      next: (area) => {
        this.cargando.set(false);
        this.rowVersion.set(area.rowVersion);
        this.fotografiaUrl.set(area.fotografiaUrl);
        this.formulario.patchValue({
          nombre: area.nombre,
          ubicacion: area.ubicacion,
          categoria: area.categoria,
          aforo: area.aforo,
          tipoAprobacion: area.tipoAprobacion,
          estado: area.estado
        });
      },
      error: () => {
        this.cargando.set(false);
        this.conflicto.set('No fue posible cargar el área solicitada.');
      }
    });
  }

  /** Crea o actualiza el área con los datos del formulario. */
  guardar(): void {
    if (this.formulario.invalid) {
      this.formulario.markAllAsTouched();
      return;
    }

    const valores = this.formulario.getRawValue();
    const base = {
      nombre: valores.nombre,
      ubicacion: valores.ubicacion,
      categoria: valores.categoria,
      aforo: valores.aforo,
      tipoAprobacion: valores.tipoAprobacion
    };

    this.guardando.set(true);
    this.conflicto.set(null);
    this.erroresCampo.set({});

    const id = this.areaId();

    if (id) {
      this.api
        .actualizar(id, {
          ...base,
          estado: valores.estado,
          rowVersion: this.rowVersion()
        })
        .subscribe({
          next: () => {
            this.guardando.set(false);
            void this.router.navigate(['/administracion/espacios']);
          },
          error: (error: unknown) => this.manejarError(error)
        });

      return;
    }

    this.api.crear(base).subscribe({
      next: (creada) => {
        this.guardando.set(false);
        void this.router.navigate(['/administracion/espacios', creada.id, 'puestos']);
      },
      error: (error: unknown) => this.manejarError(error)
    });
  }

  /** Primer error del servidor asociado al campo. */
  errorDe(campo: string): string | null {
    return this.erroresCampo()[campo]?.[0] ?? null;
  }

  /** Indica si el nombre debe mostrar su validación local. */
  nombreInvalido(): boolean {
    const control = this.formulario.controls.nombre;
    return control.touched && control.invalid;
  }

  /** Indica si la ubicación debe mostrar su validación local. */
  ubicacionInvalida(): boolean {
    const control = this.formulario.controls.ubicacion;
    return control.touched && control.invalid;
  }

  /** Indica si el aforo debe mostrar su validación local. */
  aforoInvalido(): boolean {
    const control = this.formulario.controls.aforo;
    return control.touched && control.invalid;
  }

  /** Sube la fotografía del área en edición. */
  subirFoto(evento: Event): void {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    const id = this.areaId();

    if (!archivo || !id) {
      return;
    }

    this.api.subirImagen(id, archivo).subscribe({
      next: (respuesta) => this.fotografiaUrl.set(respuesta.urlFotografia),
      error: (error: unknown) => this.manejarError(error)
    });
  }

  /** Elimina la fotografía del área. */
  quitarFoto(): void {
    const id = this.areaId();

    if (!id) {
      return;
    }

    this.api.eliminarImagen(id).subscribe({
      next: () => this.fotografiaUrl.set(null),
      error: (error: unknown) => this.manejarError(error)
    });
  }

  private manejarError(error: unknown): void {
    this.guardando.set(false);

    if (!(error instanceof HttpErrorResponse)) {
      return;
    }

    const problema = leerProblema(error);

    if (error.status === 409) {
      this.conflicto.set(problema?.detail ?? 'El área entra en conflicto con su estado actual.');
      return;
    }

    if (problema?.errors) {
      this.erroresCampo.set(problema.errors);
    }
  }
}
