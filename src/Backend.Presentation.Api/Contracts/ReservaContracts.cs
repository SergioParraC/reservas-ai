namespace Backend.Presentation.Api.Contracts;

/// <summary>Cuerpo del alta de una reserva sobre un puesto de cómputo.</summary>
public sealed record SolicitudCrearReserva(Guid PuestoId, DateTimeOffset Inicio, DateTimeOffset Fin);
