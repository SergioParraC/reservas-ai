/** Solicitud del formulario público de registro. */
export interface RegistroUsuarioSolicitud {
  nombreCompleto: string;
  correoInstitucional: string;
  contrasena: string;
}

/** Respuesta del autorregistro con el rol inicial asignado. */
export interface RegistroUsuarioRespuesta {
  id: string;
  nombreCompleto: string;
  correoInstitucional: string;
  rol: string;
}

/** Estado del vínculo federado de la cuenta autenticada. */
export interface EstadoVinculacion {
  vinculado: boolean;
  correoInstitucional: string;
}

/** Fila de la consola administrativa de usuarios. */
export interface UsuarioAdministrativo {
  id: string;
  nombreCompleto: string;
  correoInstitucional: string;
  rol: string | null;
  tipoActor: number;
  activo: boolean;
  vinculado: boolean;
  creadoEn: string;
  ultimoAccesoEn: string | null;
}

/** Página de resultados de la API. */
export interface PaginaUsuarios {
  elementos: UsuarioAdministrativo[];
  total: number;
  pagina: number;
  tamano: number;
}

/** Filtros de la consola de usuarios. */
export interface FiltroUsuarios {
  q?: string | null;
  rol?: string | null;
  vinculado?: boolean | null;
  pagina: number;
  tamano: number;
}

/** Resultado del cambio de rol administrativo. */
export interface CambioRolRespuesta {
  id: string;
  correoInstitucional: string;
  rol: string;
  requiereRenovacion: boolean;
}

/** Roles oficiales que la consola puede asignar. */
export const ROLES_ASIGNABLES = ['Administrativo', 'Docente', 'Estudiante'] as const;
