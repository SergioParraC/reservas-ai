# Tasks

## 1. Bloque 1 — Backend .NET: dominio, aplicación, EF Core y API

- [ ] 1.1 Crear en `Backend.Domain` `FranjaHoraria`, `PoliticaReserva`, `EstadoReserva`, `MensajesReserva` y `Reserva` (`Confirmar`/`Cancelar`); verificar con pruebas unitarias de solapamiento contiguo y parcial, duración fuera de rango, cancelación ajena y cancelación fuera de plazo.
- [ ] 1.2 Implementar `MotorDisponibilidad` (`GenerarFranjas`, `HaySolapamiento`) sin estado ni relojes implícitos; verificar con pruebas unitarias de franja ocupada, antelación mínima, horizonte máximo y reserva cancelada ignorada.
- [ ] 1.3 Añadir `OpcionesReservas` (`AntelacionCancelacionMinutos = 30`, `MaxReservasActivasPorUsuario = 2`), su enlace en `DependencyInjection` y la sección `Reservas` en `appsettings.json`; verificar con `dotnet build src/Backend.sln` y el arranque de la API con zona horaria válida e inválida.
- [ ] 1.4 Declarar `IReservasRepository`, `IConsultaReservas` y los DTOs de disponibilidad y reservas; verificar con `dotnet test tests/Backend.ArchitectureTests` que la aplicación no referencia EF Core ni ASP.NET Core.
- [ ] 1.5 Implementar los handlers de disponibilidad, alta autoconfirmada, consulta propia y cancelación, incluida la validación del cupo de reservas activas; verificar con pruebas unitarias (xUnit + Moq) los resultados de éxito, `400`, `403`, `404` y `409` (franja ocupada, puesto no reservable y cupo agotado).
- [ ] 1.6 Configurar `ReservaConfiguration`, `DbSet<Reserva>` y la migración `Reservas` con `RowVersion`, índice filtrado `IX_Reservas_PuestoId_Inicio_Activa` e índices de consulta; verificar con `dotnet ef database update` y una consulta a `sys.indexes`.
- [ ] 1.7 Implementar `ReservasRepository` (transacción `Serializable`, `UPDLOCK/HOLDLOCK`, conteo del cupo y reintentos) y `ConsultaReservas` con `AsNoTracking`; verificar con Testcontainers dos altas concurrentes sobre la misma franja (una sola reserva), dos altas concurrentes del mismo usuario que no superan el cupo y dos cancelaciones concurrentes (una `409`).
- [ ] 1.8 Exponer `ReservasController` y la acción de disponibilidad autenticada en `PuestosController`, con los slugs `franja-no-disponible`, `puesto-no-reservable`, `fuera-de-plazo` y `limite-reservas-alcanzado`; verificar con pruebas de integración los códigos `200`, `201`, `204`, `400`, `401`, `403`, `404`, `409` y el contrato `application/problem+json`.
- [ ] 1.9 Documentar endpoints, reglas (cupo, plazo de cancelación e inmutabilidad del QR), configuración y migración en `docs/reservas-prestamo-directo.md`; verificar que cada comando y URL del documento responde como se describe contra la API local.
- [ ] 1.10 Ejecutar `dotnet test src/Backend.sln` y confirmar la suite completa en verde antes de pasar al bloque 2.

## 2. Bloque 2 — Frontend Angular 20 e integración

- [ ] 2.1 Crear `features/solicitudes-estudiante` con `solicitudes.routes.ts`, `models/reserva.models.ts`, `mappers/reserva.mapper.ts` y `reservas.api.ts`; verificar con `npm run build` y pruebas de servicio con `HttpTestingController` de URLs, `HttpParams` y cuerpo del `POST`.
- [ ] 2.2 Implementar `reservas.store.ts` con Signals (`franjas`, `reservas`, `cargando`, `error`, `cancelando`); verificar con pruebas unitarias la carga de disponibilidad, el error `409` (franja ocupada y cupo agotado) y la cancelación exitosa.
- [ ] 2.3 Implementar `puesto-disponibilidad.page` y `franja-lista` con selección de fecha, duración y confirmación inmediata; verificar con pruebas de componente que las franjas ocupadas se deshabilitan y que el `201` muestra la reserva creada.
- [ ] 2.4 Implementar `mis-reservas.page` y `reserva-card` con filtro por estado, marca de cancelable según el margen y confirmación previa al `DELETE`; verificar con pruebas de componente los casos `204`, `403`, `409` y el mensaje de cupo agotado al intentar una nueva reserva.
- [ ] 2.5 Registrar `SOLICITUDES_ROUTES` bajo `authGuard` en `app.routes.ts`, añadir "Mis reservas" en `shared/shell` y el enlace por puesto en `espacio-detalle.page` solo para áreas con aprobación automática; verificar con `npm start` que sin sesión redirige al acceso y que un área con aprobación docente no muestra el enlace.
- [ ] 2.6 Ejecutar `npm test`, `npm run build` y el humo integral (consulta de franjas → reserva → "Mis reservas" → segunda reserva solapada rechazada con `409` → cancelación dentro del plazo), registrando el resultado en el reporte de la etapa.

## Workflow follow-up

- Ejecutar `openspec validate 03-direct-booking-core --strict` y corregir advertencias antes de la revisión.
- Archivar el cambio con el flujo de OpenSpec una vez aprobado el commit atómico.
