# Proposal

## Why

El catálogo ya publica áreas y puestos con su código QR, pero un puesto activo sigue sin agenda: hoy nadie puede saber si está ocupado ni apartarlo, así que la institución vuelve a conciliar horarios por fuera del sistema y reproduce el doble-apartado que el proyecto debe eliminar. Sin un motor de disponibilidad y un control transaccional de traslapes, la Etapa 3 (*check-in* por QR y liberación por inasistencia) carecería de reservas sobre las cuales operar.

## What Changes

- Incorporar al dominio la entidad `Reserva` (raíz de agregado) con el value object inmutable `FranjaHoraria`, el enum `EstadoReserva` y el servicio de dominio `MotorDisponibilidad`, ejecutable en C# puro.
- Exponer puertos de aplicación sin tipos del ORM: `IReservasRepository` (alta condicionada a disponibilidad y transición de estado) e `IConsultaReservas` (agenda del titular y lectura de ocupación), más los handlers y DTOs de reservas.
- Persistir `Reservas` con su `RowVersion` de concurrencia optimista, índice único filtrado sobre `(PuestoId, Inicio)` para reservas vigentes e índices de consulta por puesto y por usuario; migración aditiva.
- Exponer `GET /api/puestos/{id}/disponibilidad`, `POST /api/reservas`, `GET /api/reservas/me` y `DELETE /api/reservas/{id}` conservando el contrato RFC 7807.
- Reservar de forma autoconfirmada e inmediata solo sobre puestos activos de áreas activas con `TipoAprobacion.Automatica`; cualquier otra combinación se rechaza sin crear la reserva.
- Limitar a `Reservas:MaxReservasActivasPorUsuario` (por defecto `2`) las reservas `Confirmada` simultáneas de una misma cuenta y rechazar el exceso con `409 limite-reservas-alcanzado`.
- Mantener el código QR del puesto como identificador inmutable del recurso físico: cancelar solo cambia el estado de la reserva a `Cancelada` y nunca regenera ni reasigna el QR.
- Crear la funcionalidad `features/solicitudes-estudiante` en Angular 20 (Signals + Standalone Components): selección de franjas del puesto, confirmación inmediata, listado de reservas propias y cancelación con validación de plazo; enlazarla desde el detalle del catálogo y la navegación de la SPA.
- Añadir la configuración `Reservas:*` (antelación mínima y máxima, duración mínima y máxima, granularidad de franja, plazo de cancelación de 30 minutos por defecto, cupo de reservas activas por usuario y zona horaria) y las pruebas unitarias, de integración concurrente y de componente de la etapa.

Fuera de alcance: reservas recurrentes semestrales y calendarios docentes, aprobaciones de docente o administrativo con carga de pre-informe PDF, franjas de mantenimiento y bloqueos programados, reprogramación o extensión de una reserva, *check-in*/*check-out* por QR, liberación automática por inasistencia, sanciones, notificaciones SMTP y listas de espera. La reserva de un área completa (laboratorio o auditorio) tampoco se incluye aquí.

## Capabilities

### New Capabilities

- `reservas/prestamo-directo`: consulta de franjas horarias libres de un puesto de cómputo y reserva autoconfirmada e inmediata sobre él, con prevención estricta de traslapes entre reservas vigentes y cancelación de la reserva propia dentro del plazo permitido.

### Modified Capabilities

- Ninguna. `espacios/gestion-espacios` describe el inventario y el préstamo directo consume ese inventario sin alterar sus requisitos.

## Impact

- **Código afectado (nuevo):** `src/Backend.Domain/Entities/Reserva.cs`, `ValueObjects/FranjaHoraria.cs`, `Enums/EstadoReserva.cs`, `Services/MotorDisponibilidad.cs` y `Exceptions/MensajesReserva.cs`; `src/Backend.Application/Modules/Reservas` (Dtos, handlers de disponibilidad, alta, consulta y cancelación), `Application/Interfaces/IReservasRepository.cs`, `IConsultaReservas.cs` y `OpcionesReservas` en `Common/OpcionesAplicacion.cs`; `src/Backend.Infrastructure/Persistence/Configurations/ReservaConfiguration.cs`, `Repositories/ReservasRepository.cs`, `Queries/ConsultaReservas.cs` y la migración `Reservas`; `src/Backend.Presentation.Api/Controllers/ReservasController.cs` y `PuestosController` (endpoint de disponibilidad) con sus DTOs en `Contracts`; `src/frontend/src/app/features/solicitudes-estudiante` (rutas, modelos, `reservas.api.ts`, store, páginas y componentes), `app.routes.ts`, `shared/shell` y el detalle del catálogo en `features/areas`.
- **APIs expuestas:** `GET /api/puestos/{id}/disponibilidad` (autenticada, nunca anónima), `POST /api/reservas`, `GET /api/reservas/me` y `DELETE /api/reservas/{id}`. Errores en `application/problem+json` con `type` estable (`franja-no-disponible`, `fuera-de-plazo`, `puesto-no-reservable`, `limite-reservas-alcanzado`).
- **Persistencia:** tabla `Reservas` con `Inicio`/`Fin` en `datetimeoffset`, `Estado` como `int`, `RowVersion` `rowversion`, índice único filtrado `IX_Reservas_PuestoId_Inicio_Activa` (`WHERE [Estado] = 1`) e índices de consulta por puesto y por usuario. La migración es aditiva sobre `Espacios` y `Puestos`.
- **Configuración:** sección `Reservas` con `AntelacionMinimaMinutos`, `AntelacionMaximaDias`, `DuracionMinimaMinutos`, `DuracionMaximaMinutos`, `IntervaloMinutos`, `AntelacionCancelacionMinutos` (`30`), `MaxReservasActivasPorUsuario` (`2`) y `ZonaHoraria`.
- **Seguridad:** todos los endpoints exigen sesión autenticada (JWT local o federado); la cancelación solo la ejerce el titular (`403` para terceros) y la reserva nunca expone datos de otros usuarios. El alta se apoya en transacción serializable, bloqueo de la fila del puesto y token de concurrencia optimista, no en confianza del cliente.
- **Riesgo principal:** dos solicitudes simultáneas sobre el mismo puesto y la misma franja. Se mitiga con bloqueo de fila dentro de transacción serializable, re-verificación obligatoria de solapamientos antes de insertar, índice único filtrado como segunda barrera, `RowVersion` para las transiciones de estado y conteo del cupo en la misma transacción; los escenarios concurrentes quedan cubiertos por prueba de integración.
