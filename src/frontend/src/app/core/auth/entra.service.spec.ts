import { TestBed } from '@angular/core/testing';
import { AccountInfo, AuthenticationResult, IPublicClientApplication } from '@azure/msal-browser';

import { EntraService, MSAL_INSTANCE } from './entra.service';

describe('EntraService', () => {
  const cuenta = { username: 'ana@uninpahu.edu.co' } as AccountInfo;
  let msal: jasmine.SpyObj<IPublicClientApplication>;
  let servicio: EntraService;

  beforeEach(() => {
    sessionStorage.clear();
    msal = jasmine.createSpyObj<IPublicClientApplication>('IPublicClientApplication', [
      'initialize',
      'loginRedirect',
      'handleRedirectPromise',
      'acquireTokenSilent',
      'getAllAccounts',
      'getActiveAccount',
      'setActiveAccount',
      'logoutRedirect'
    ]);

    const resultado = {
      accessToken: 'entra-token',
      expiresOn: new Date(Date.now() + 3_600_000),
      account: cuenta
    } as AuthenticationResult;

    msal.initialize.and.resolveTo();
    msal.loginRedirect.and.resolveTo();
    msal.handleRedirectPromise.and.resolveTo(resultado);
    msal.acquireTokenSilent.and.resolveTo(resultado);
    msal.getAllAccounts.and.returnValue([cuenta]);
    msal.getActiveAccount.and.returnValue(cuenta);
    msal.logoutRedirect.and.resolveTo();

    TestBed.configureTestingModule({
      providers: [{ provide: MSAL_INSTANCE, useValue: msal }]
    });

    servicio = TestBed.inject(EntraService);
  });

  afterEach(() => sessionStorage.clear());

  it('inicia el redirect con los scopes configurados y guarda el destino', async () => {
    await servicio.loginRedirect('/perfil');

    expect(msal.loginRedirect).toHaveBeenCalledWith(
      jasmine.objectContaining({ scopes: jasmine.any(Array), redirectUri: jasmine.stringMatching('/auth/entra$') })
    );
    expect(sessionStorage.getItem('reservas.retorno-entra')).toBe('/perfil');
  });

  it('procesa el callback y devuelve el token con su expiración', async () => {
    const resultado = await servicio.procesarCallback();

    expect(msal.handleRedirectPromise).toHaveBeenCalled();
    expect(msal.setActiveAccount).toHaveBeenCalledWith(cuenta);
    expect(resultado?.token).toBe('entra-token');
    expect(resultado?.retorno).toBe('/inicio');
    expect(Date.parse(resultado?.expiraEn ?? '')).toBeGreaterThan(Date.now());
  });

  it('adquiere un token silencioso para la cuenta activa', async () => {
    await expectAsync(servicio.adquirirToken()).toBeResolvedTo('entra-token');
    expect(msal.acquireTokenSilent).toHaveBeenCalledWith(
      jasmine.objectContaining({ account: cuenta })
    );
  });
});
