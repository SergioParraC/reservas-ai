import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { leerProblema } from '../../../../core/interceptors/problem-details.interceptor';
import { slugProblema } from '../../../../core/errores/mensajes-problema';
import { PlantillaQrPanel } from '../../components/plantilla-qr-panel';
import {
  AFORO_MAXIMO,
  CATEGORIAS,
  CategoriaEspacio,
  combinacionValida,
  elementosDe,
  ESTADOS,
  EstadoUnidad,
  etiquetaElemento as etiquetaElementoPrestamo,
  MODALIDADES,
  TIPOS_APROBACION,
  TipoAprobacion,
  TipoElementoPrestamo,
  TipoModalidadPrestamo
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
  readonly modalidades = MODALIDADES;
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
  readonly elementosDisponibles = signal<readonly TipoElementoPrestamo[]>(
    elementosDe(TipoModalidadPrestamo.AreaCompleta)
  );
  private readonly rowVersion = signal<string | null>(null);

  readonly formulario = this.formularioReactivo.group({
    nombre: ['', [Validators.required, Validators.maxLength(100)]],
    ubicacion: ['', [Validators.required, Validators.maxLength(150)]],
    categoria: [CategoriaEspacio.Laboratorio, [Validators.required]],
    aforo: [10, [Validators.required, Validators.min(1), Validators.max(AFORO_MAXIMO)]],
    tipoAprobacion: [TipoAprobacion.Docente, [Validators.required]],
    tipoModalidadPrestamo: [TipoModalidadPrestamo.AreaCompleta, [Validators.required]],
    tipoElementoPrestamo: [TipoElementoPrestamo.LaboratorioEspecializado, [Validators.required]],
    estado: [EstadoUnidad.Activo, [Validators.required]]
  });

  /** Carga los datos del área cuando la ruta es de edición. */
  ngOnInit(): void {
    this.formulario.controls.tipoModalidadPrestamo.valueChanges.subscribe((modalidad) => {
      this.ajustarElementos(modalidad);
    });

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
          tipoModalidadPrestamo: area.tipoModalidadPrestamo,
          tipoElementoPrestamo: area.tipoElementoPrestamo,
          estado: area.estado
        });
        this.elementosDisponibles.set(elementosDe(area.tipoModalidadPrestamo));
      },
      error: () => {
        this.cargando.set(false);
        this.conflicto.set('No fue posible cargar el área solicitada.');
      }
    });
  }

  /** Crea o actualiza el área con los datos del formulario. */
  guardar(): void {
    const valores = this.formulario.getRawValue();

    if (this.formulario.invalid || this.combinacionInvalida()) {
      this.formulario.markAllAsTouched();
      this.conflicto.set(
        this.combinacionInvalida()
          ? 'La combinación de modalidad y elemento de préstamo no es válida para el área.'
          : null
      );
      return;
    }

    const base = {
      nombre: valores.nombre,
      ubicacion: valores.ubicacion,
      categoria: valores.categoria,
      aforo: valores.aforo,
      tipoAprobacion: valores.tipoAprobacion,
      tipoModalidadPrestamo: valores.tipoModalidadPrestamo,
      tipoElementoPrestamo: valores.tipoElementoPrestamo
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

  /** Indica si la modalidad y el elemento seleccionados son incoherentes. */
  combinacionInvalida(): boolean {
    const valores = this.formulario.getRawValue();
    return !combinacionValida(valores.tipoModalidadPrestamo, valores.tipoElementoPrestamo);
  }

  /** Etiqueta legible del elemento de préstamo. */
  etiquetaElemento(valor: TipoElementoPrestamo): string {
    return etiquetaElementoPrestamo(valor);
  }

  private ajustarElementos(modalidad: TipoModalidadPrestamo): void {
    const permitidos = elementosDe(modalidad);
    this.elementosDisponibles.set(permitidos);

    const actual = this.formulario.controls.tipoElementoPrestamo.value;

    if (!permitidos.includes(actual) && permitidos.length > 0) {
      this.formulario.controls.tipoElementoPrestamo.setValue(permitidos[0]);
    }
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
    const slug = slugProblema(error);

    if (error.status === 409) {
      this.conflicto.set(
        slug === 'modalidad-con-ocupacion'
          ? 'No es posible cambiar la modalidad: el área tiene ocupación futura (series o reservas vigentes).'
          : problema?.detail ?? 'El área entra en conflicto con su estado actual.'
      );
      return;
    }

    if (problema?.errors) {
      this.erroresCampo.set(problema.errors);
    }
  }
}
