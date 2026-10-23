export const environment = {
  produccion: false,
  // Cadena vacía: el dev-server usa proxy.conf.json hacia la API local y evita CORS.
  apiBaseUrl: '',
  entraId: {
    clientId: '',
    authority: 'https://login.microsoftonline.com/common',
    redirectUri: '/auth/entra',
    scopes: ['api://reservas-espacios/access_as_user']
  }
};
