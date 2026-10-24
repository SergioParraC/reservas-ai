import { ROLES } from './core/auth/auth.models';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

import { routes } from './app.routes';

describe('rutas de asistencia y monitoreo', () => {
  it('protege el escáner con una sesión autenticada y sin rol específico', () => {
    const escaner = routes.find((ruta) => ruta.path === 'checkin/:reservaId');

    expect(escaner).toBeDefined();
    expect(escaner?.canActivate).toEqual([authGuard]);
    expect(escaner?.title).toBe('Check-in presencial');
  });

  it('redirige /checkin sin identificador a la agenda del titular', () => {
    const alias = routes.find((ruta) => ruta.path === 'checkin');

    expect(alias?.pathMatch).toBe('full');
    expect(alias?.redirectTo).toBe('mis-reservas');
  });

  it('protege el monitoreo con sesión y rol Administrativo', () => {
    const monitoreo = routes.find((ruta) => ruta.path === 'administracion/monitoreo');

    expect(monitoreo?.canActivate).toEqual([authGuard, roleGuard]);
    expect((monitoreo?.data as { roles: string[] }).roles).toEqual([ROLES.administrativo]);
  });

  it('mantiene las rutas administrativas existentes con su rol', () => {
    const usuarios = routes.find((ruta) => ruta.path === 'administracion/usuarios');

    expect((usuarios?.data as { roles: string[] }).roles).toEqual([ROLES.administrativo]);
  });
});
