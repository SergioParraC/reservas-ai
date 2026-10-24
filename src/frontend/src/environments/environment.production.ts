export const environment = {
  produccion: true,
  // Mismo origen: Caddy sirve el SPA y reenvía /api/* a la API, así no hay CORS.
  apiBaseUrl: '',
  checkIn: {
    // Espejo de Reservas:VentanaToleranciaCheckInMinutos (la API valida con su propio valor).
    ventanaToleranciaMinutos: 15
  },
  entraId: {
    clientId: 'aa840431-5f08-4f6e-887f-afe6346663ed',
    authority: 'https://login.microsoftonline.com/e512294f-d02c-4380-927d-e8427efc06df',
    redirectUri: '/auth/entra',
    scopes: ['api://aa840431-5f08-4f6e-887f-afe6346663ed/access_as_user']
  }
};
