namespace Backend.Presentation.Api.Contracts;

/// <summary>Cuerpo del check-in: el código impreso en la placa QR del puesto reservado.</summary>
public sealed record SolicitudCheckIn(string? CodigoQr);
