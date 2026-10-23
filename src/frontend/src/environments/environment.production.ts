export const environment = {
  produccion: true,
  apiBaseUrl: 'https://api.reservas-espacios.edu',
  entraId: {
    clientId: '',
    authority: 'https://login.microsoftonline.com/common',
    redirectUri: '/auth/entra',
    scopes: ['api://reservas-espacios/access_as_user']
  }
};
