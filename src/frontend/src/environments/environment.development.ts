export const environment = {
  produccion: false,
  // Cadena vacía: el dev-server usa proxy.conf.json hacia la API local y evita CORS.
  apiBaseUrl: '',
  checkIn: {
    // Espejo de Reservas:VentanaToleranciaCheckInMinutos (la API valida con su propio valor).
    ventanaToleranciaMinutos: 15
  },
  entraId: {
    clientId: '',
    authority: 'https://login.microsoftonline.com/common',
    redirectUri: '/auth/entra',
    scopes: ['api://reservas-espacios/access_as_user']
  }
};
