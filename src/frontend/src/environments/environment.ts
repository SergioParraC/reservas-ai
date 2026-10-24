export const environment = {
  produccion: true,
  apiBaseUrl: 'https://api.reservas-espacios.edu',
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
