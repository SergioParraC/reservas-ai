# Spec Delta

## Purpose

Permite a un miembro de la comunidad consultar las franjas libres de un puesto de cómputo y apartarlo de inmediato sin aprobación, garantizando que dos personas nunca obtengan la misma franja y que el titular pueda liberarla con antelación.

## ADDED Requirements

### Requirement: Consulta de franjas horarias disponibles sin colisiones

El sistema SHALL devolver, para un puesto activo y una fecha solicitada, la lista de franjas horarias de la jornada con la marca de disponibilidad calculada contra las reservas vigentes, de modo que ninguna franja marcada como disponible se solape con una reserva vigente del mismo puesto.

#### Scenario: Franja libre entre dos reservas vigentes

- **GIVEN** un puesto activo con reservas vigentes de `08:00` a `10:00` y de `12:00` a `14:00` en la fecha consultada
- **WHEN** un usuario autenticado solicita `GET /api/puestos/{id}/disponibilidad` para esa fecha con franjas de una hora
- **THEN** responde `200 OK` con las franjas `10:00-11:00` y `11:00-12:00` marcadas como disponibles
- **AND** las franjas `08:00-10:00` y `12:00-14:00` se devuelven marcadas como no disponibles
- **AND** cada franja incluye su inicio y fin con desplazamiento horario explícito

#### Scenario: Solapamiento parcial con una reserva vigente

- **GIVEN** un puesto con una reserva vigente de `09:00` a `10:30`
- **WHEN** se consulta la disponibilidad con franjas de una hora
- **THEN** las franjas `09:00-10:00` y `10:00-11:00` se devuelven como no disponibles
- **AND** la franja `08:00-09:00` se devuelve como disponible

#### Scenario: Reserva cancelada no ocupa la franja

- **GIVEN** un puesto cuya única reserva de la mañana fue cancelada por su titular
- **WHEN** se consulta la disponibilidad de esa mañana
- **THEN** todas las franjas de la jornada se devuelven como disponibles
- **AND** la reserva cancelada no aparece en la respuesta

#### Scenario: Consulta fuera del horizonte permitido

- **GIVEN** un horizonte máximo de reserva configurado en 14 días
- **WHEN** se solicita la disponibilidad de una fecha posterior a ese horizonte
- **THEN** responde `400 Bad Request` en formato RFC 7807 indicando el rango de fechas admitido
- **AND** no se ejecuta ninguna lectura de agenda

#### Scenario: Consulta sin sesión

- **GIVEN** una solicitud a `GET /api/puestos/{id}/disponibilidad` sin encabezado `Authorization`
- **WHEN** el sistema evalúa la autorización
- **THEN** responde `401 Unauthorized` en formato RFC 7807
- **AND** no devuelve ninguna franja ni dato de ocupación del puesto

### Requirement: Reserva autoconfirmada inmediata de puestos de cómputo

El sistema SHALL crear la reserva en estado confirmado y sin intervención humana cuando el puesto y su área estén activos, el área tenga aprobación automática, la franja esté libre y el titular no haya agotado su cupo de reservas activas; en cualquier otro caso SHALL rechazar la solicitud sin persistir la reserva.

#### Scenario: Reserva exitosa de un puesto de cómputo

- **GIVEN** una sesión autenticada de rol `Estudiante`
- **AND** un puesto activo `PC-07` de un área activa con aprobación automática
- **AND** la franja `2026-11-03T08:00:00-05:00` a `2026-11-03T09:00:00-05:00` sin reservas vigentes
- **WHEN** envía a `POST /api/reservas` el puesto y esa franja
- **THEN** responde `201 Created` con el identificador de la reserva, el estado confirmado y el encabezado `Location`
- **AND** la reserva aparece de inmediato en `GET /api/reservas/me`
- **AND** la franja deja de figurar como disponible para cualquier otro usuario

#### Scenario: Puesto o área no reservable

- **GIVEN** un puesto inactivo o un área con aprobación docente o administrativa
- **WHEN** se intenta reservar una franja libre de ese puesto
- **THEN** responde `409 Conflict` con un problema RFC 7807 que indica que el puesto no admite préstamo directo
- **AND** no se crea ninguna reserva

#### Scenario: Franja con duración fuera del rango admitido

- **GIVEN** una duración mínima configurada de 30 minutos y máxima de 4 horas
- **WHEN** se solicita una reserva de 15 minutos sobre un puesto reservable
- **THEN** responde `400 Bad Request` con el campo de la franja señalado en el problema
- **AND** no se persiste la reserva

#### Scenario: Solicitud sobre un puesto inexistente

- **GIVEN** un identificador de puesto que no corresponde a ningún registro
- **WHEN** se envía la solicitud de reserva
- **THEN** responde `404 Not Found` en formato RFC 7807
- **AND** la respuesta no revela datos de otros puestos ni de otras reservas

#### Scenario: Cupo de reservas activas alcanzado

- **GIVEN** un cupo configurado de 2 reservas activas por usuario
- **AND** una cuenta con 2 reservas en estado confirmado
- **WHEN** el titular intenta reservar una tercera franja libre en otro puesto
- **THEN** responde `409 Conflict` con un problema RFC 7807 que identifica el cupo agotado
- **AND** la tercera reserva no se persiste y las dos existentes no se modifican

#### Scenario: Cancelar libera cupo de inmediato

- **GIVEN** una cuenta con 2 reservas confirmadas y el cupo agotado
- **WHEN** el titular cancela una de ellas dentro del plazo permitido
- **THEN** responde `204 No Content`
- **AND** una nueva reserva del mismo titular sobre una franja libre responde `201 Created`

### Requirement: Prevención de doble reserva simultánea

El sistema SHALL garantizar que, ante solicitudes concurrentes sobre el mismo puesto y franjas que se solapen, exactamente una reserva quede confirmada y las demás sean rechazadas con conflicto, sin pérdida de integridad ni reservas duplicadas.

#### Scenario: Dos solicitudes simultáneas sobre la misma franja

- **GIVEN** un puesto libre y dos usuarios autenticados que solicitan la misma franja a la vez
- **WHEN** ambas solicitudes se procesan en paralelo
- **THEN** una responde `201 Created` y la otra `409 Conflict`
- **AND** la base de datos contiene exactamente una reserva vigente para esa franja
- **AND** la respuesta del conflicto identifica la franja como ya ocupada

#### Scenario: Franjas contiguas sin solapamiento

- **GIVEN** un puesto con una reserva vigente de `08:00` a `09:00`
- **WHEN** otro usuario solicita `09:00` a `10:00` del mismo puesto
- **THEN** responde `201 Created`
- **AND** ambas reservas permanecen vigentes y contiguas

#### Scenario: Solapamiento parcial o contenido

- **GIVEN** una reserva vigente de `10:00` a `12:00`
- **WHEN** se solicita `11:00` a `13:00` o una franja de `10:30` a `11:30`
- **THEN** cada intento responde `409 Conflict`
- **AND** no se crea ninguna reserva nueva ni se modifica la existente

#### Scenario: Conflicto de concurrencia al confirmar

- **GIVEN** una reserva que fue modificada o cancelada entre la validación y la escritura
- **WHEN** el sistema intenta confirmar el cambio con su token de concurrencia desactualizado
- **THEN** responde `409 Conflict` en formato RFC 7807
- **AND** la reserva conserva el estado que la base de datos tenía antes del intento

### Requirement: Cancelación de la reserva propia dentro del margen permitido

El sistema SHALL permitir que el titular cancele su reserva confirmada mientras falte al menos el margen configurado para el inicio, liberando la franja; fuera de ese margen o sobre una reserva ajena SHALL rechazar la operación sin alterar el estado.

#### Scenario: Cancelación dentro del plazo

- **GIVEN** una reserva confirmada del usuario con inicio dentro de 24 horas
- **AND** un margen de cancelación configurado de 30 minutos
- **WHEN** el titular envía `DELETE /api/reservas/{id}`
- **THEN** responde `204 No Content`
- **AND** la reserva pasa a estado cancelado y deja de ocupar la franja
- **AND** la franja vuelve a figurar como disponible para otros usuarios
- **AND** el código QR del puesto conserva exactamente el valor que tenía antes de la reserva

#### Scenario: Cancelación fuera del plazo

- **GIVEN** una reserva confirmada cuyo inicio ocurre en 20 minutos y un margen de 30 minutos
- **WHEN** el titular intenta cancelarla
- **THEN** responde `409 Conflict` con un problema RFC 7807 que indica el plazo mínimo de cancelación
- **AND** la reserva permanece confirmada y la franja sigue ocupada

#### Scenario: Cancelación de una reserva ajena

- **GIVEN** una reserva confirmada de otro usuario
- **WHEN** un usuario autenticado distinto intenta cancelarla
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** la reserva conserva su estado y su titularidad

#### Scenario: Cancelación repetida

- **GIVEN** una reserva ya cancelada por su titular
- **WHEN** se repite `DELETE /api/reservas/{id}`
- **THEN** responde `409 Conflict` con un problema RFC 7807 que indica el estado actual
- **AND** no se registra una segunda cancelación
