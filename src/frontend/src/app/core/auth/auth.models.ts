/** Token de acceso emitido por la API local. */
export interface TokenEmitido {
  token: string;
  tipoToken: string;
  expiraEn: string;
  roles: string[];
}

/** Origen de la credencial que sostiene la sesión. */
export type OrigenSesion = 'local' | 'entra';

/** Perfil del portador devuelto por `GET /api/auth/me`. */
export interface PerfilUsuario {
  id: string;
  nombreCompleto: string;
  correoInstitucional: string;
  tipoActor: number;
  roles: string[];
  vinculado?: boolean;
}

/** Sesión persistida en el almacenamiento local. */
export interface Sesion {
  token: string;
  expiraEn: string;
  perfil: PerfilUsuario;
  origen?: OrigenSesion;
}

/** Credenciales locales enviadas al inicio de sesión. */
export interface CredencialesLocales {
  correoInstitucional: string;
  contrasena: string;
}

/** Roles oficiales del dominio. */
export const ROLES = {
  administrativo: 'Administrativo',
  docente: 'Docente',
  estudiante: 'Estudiante'
} as const;

/** Nombres legibles del tipo de actor persistido por la API. */
export const TIPOS_ACTOR: Record<number, string> = {
  1: 'Administrativo',
  2: 'Docente',
  3: 'Estudiante'
};
