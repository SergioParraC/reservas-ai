export const environment = {
  produccion: true,
  // Mismo origen: Caddy sirve el SPA y reenvía /api/* a la API, así no hay CORS.
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
