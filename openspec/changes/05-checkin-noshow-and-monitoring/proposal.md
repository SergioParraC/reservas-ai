# Proposal

## Why

El sistema ya aparta puestos y áreas, pero nada confirma que el titular se presente: una franja reservada puede quedar vacía mientras el catálogo la muestra ocupada y otro usuario busca dónde sentarse. Sin validación presencial ni liberación automática, la ocupación real del campus es invisible y la sobre-reserva se traslada al pasillo.

## What Changes

- Extender la entidad `Reserva` con `FechaCheckIn` y `FechaCheckOut` y ampliar `EstadoReserva` con `EnUso`, `Finalizada` y `NoShow` (renombra `CanceladaPorNoShow`, conservando el valor `3`); `EstaVigente` pasa a cubrir `Confirmada` y `EnUso`.
- Incorporar la máquina de estados de asistencia en el dominio: check-in solo desde `Confirmada` dentro de la ventana de tolerancia, check-out desde `EnUso` en cualquier instante y liberación por No-Show.
- Parametrizar la ventana de tolerancia exclusivamente desde `appsettings.json` con la clave `Reservas:VentanaToleranciaCheckInMinutos` (15 por omisión): `OpcionesReservas` la expone como `TimeSpan`, la valida al arrancar y la comparten el check-in HTTP y el barrido; no hay valor duplicado en el dominio ni en la base de datos.
- Permitir el check-out anticipado: al registrarlo la reserva pasa a `Finalizada`, guarda la hora exacta de salida y acorta su `Fin` al momento de salida (conservando `FinPlanificado`), de modo que el remanente de la franja vuelve a estar disponible de inmediato para otro usuario.
- Añadir `OpcionesCheckIn` (`MinutosIntervaloBarrido`, `MaximoReservasPorBarrido`, `Habilitado`) validada al arrancar, sin duplicar la tolerancia.
- Implementar `NoShowBackgroundService` (`BackgroundService` en `Backend.Infrastructure/BackgroundJobs`) que barre cada 5 minutos las reservas `Confirmada` cuya ventana de tolerancia venció sin check-in y las pasa a `NoShow`.
- Exponer `POST /api/reservas/{id}/check-in` (cuerpo `{ codigoQr }`), `POST /api/reservas/{id}/check-out` (sin cuerpo ni ventana temporal) y `GET /api/monitoreo/ocupacion-actual`, con problemas RFC 7807 y `type` estables (`check-in-fuera-de-ventana`, `check-in-codigo-invalido`, `transicion-invalida`, `reserva-ajena`).
- Ajustar persistencia y consultas para que `EnUso` siga bloqueando la franja: índice único filtrado `[Estado] IN (1, 4)`, revalidación de solapamiento y cupo con `(Confirmada, EnUso)`, e índice `IX_Reservas_Estado_Inicio` para el barrido.
- Crear `features/checkin` en Angular 20 (Standalone + Signals) con cámara HTML5 y `BarcodeDetector`, entrada manual del código como respaldo y botón de salida siempre habilitado mientras la reserva esté `EnUso`, informando el remanente liberado.
- Crear `features/administracion/monitoreo` con tablero de ocupación por área, sondeo cada 15 segundos y totales institucionales.
- Extender `ReservaDetalle`/`ReservaResumen` y los modelos de la SPA con `FechaCheckIn`, `FechaCheckOut` y los estados nuevos, incluida la tarjeta de "Mis reservas".

## Capabilities

### New Capabilities

- `asistencia`: validación de presencia física por escaneo del QR estático del puesto, liberación automática por inasistencia y publicación de la ocupación real de las áreas en el instante consultado.

### Modified Capabilities

<!-- Ninguna: la cancelación del titular y la consulta de disponibilidad conservan su comportamiento; los estados nuevos se estrenan con esta capacidad. -->

## Impact

- **Dominio:** `src/Backend.Domain/Enums/EstadoReserva.cs`, `Entities/Reserva.cs` (`FinPlanificado`, `FechaCheckIn`, `FechaCheckOut`, `EstaEnVentanaDeCheckIn`, `RegistrarCheckIn`, `Finalizar`, `EstaVigente`), `Exceptions/MensajesReserva.cs`. El dominio recibe la tolerancia como `TimeSpan`; no guarda ni asume minutos.
- **Aplicación:** `Common/OpcionesAplicacion.cs` (`OpcionesReservas.VentanaToleranciaCheckInMinutos` + `ObtenerVentanaTolerancia()`), `Common/OpcionesCheckIn.cs` (cadencia del barrido, lote y bandera), `Modules/Asistencia/{RegistrarCheckInHandler,RegistrarCheckOutHandler,LiberarReservasVencidasHandler}.cs`, `Modules/Monitoreo/ConsultarOcupacionHandler.cs`, `Modules/Asistencia/Dtos/AsistenciaDtos.cs`, puertos `Interfaces/IConsultaCheckIn.cs` e `IConsultaOcupacionActual.cs`, y ampliación de `Interfaces/IReservasRepository.cs` con el listado de reservas vencidas.
- **Infraestructura:** `Persistence/Queries/{ConsultaCheckIn,ConsultaOcupacionActual}.cs`, `Persistence/Repositories/ReservasRepository.cs` (barrido y vigencia `(1, 4)`), `Persistence/Configurations/ReservaConfiguration.cs`, `BackgroundJobs/NoShowBackgroundService.cs`, `DependencyInjection.cs` y migración `CheckInYNoShow`.
- **API:** `Controllers/ReservasController.cs` (+`check-in`, `check-out`), `Controllers/MonitoreoController.cs`, `Contracts/AsistenciaContracts.cs`, `Problemas/ProblemasReserva.cs` (+3 slugs), `Program.cs` (handlers y hosted service).
- **Frontend:** `features/checkin/**`, `features/administracion/monitoreo/**`, `features/solicitudes-estudiante/{models/reserva.models.ts,mappers/reserva.mapper.ts,components/reserva-card.ts}`, `app.routes.ts` y `shared/shell/shell.ts`.
- **APIs expuestas:** `POST /api/reservas/{id}/check-in` (cualquier rol autenticado, solo su reserva), `POST /api/reservas/{id}/check-out`, `GET /api/monitoreo/ocupacion-actual` (rol `Administrativo`); errores en `application/problem+json`.
- **Persistencia:** columnas `Reservas.FinPlanificado` (`datetimeoffset NOT NULL`, rellena con `Fin`), `Reservas.FechaCheckIn` y `Reservas.FechaCheckOut` (`datetimeoffset` nulas); recreación del índice único `IX_Reservas_PuestoId_Inicio_Activa` con filtro `[Estado] IN (1, 4)` y nuevo índice `IX_Reservas_Estado_Inicio`; migración aditiva.
- **Disponibilidad:** `IConsultaReservas.ObtenerOcupadasAsync` y el alta de reservas siguen tomando solo `Confirmada`/`EnUso`; al pasar a `Finalizada` la reserva deja de ocupar y su `Fin` acortado libera el remanente sin tocar `MotorDisponibilidad`.
- **Configuración:** `Reservas:VentanaToleranciaCheckInMinutos` (15) es la única fuente de la tolerancia y se valida en `OpcionesReservas.Validar()`; sección `CheckIn` solo con `MinutosIntervaloBarrido` (5), `MaximoReservasPorBarrido` (200) y `Habilitado` (true).
- **Dependencias:** ninguna nueva; el escaneo usa `navigator.mediaDevices` y `BarcodeDetector` nativos (sin `@zxing`), con entrada manual cuando la API no existe.
- **Riesgo principal:** el barrido caído retiene franjas ajenas. Se mitiga con idempotencia por estado, recuperación al reiniciar (el barrido toma todo lo vencido, no solo lo reciente), tope por lote y registro en logs; cubierto por pruebas de worker, de migración y de integración.
