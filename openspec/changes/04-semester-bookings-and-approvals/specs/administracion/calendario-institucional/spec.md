# Spec Delta

## Purpose

Permite administrar el periodo académico vigente y las franjas en que la institución no opera, y aplica ambas restricciones a la disponibilidad y a cualquier solicitud de reserva de un espacio.

## ADDED Requirements

### Requirement: Registro y activación de periodos académicos

El sistema SHALL permitir al rol `Administrativo` registrar periodos con fecha de inicio, fecha de fin y estado, rechazando rangos invertidos o solapados, y SHALL mantener como máximo un periodo activo a la vez.

#### Scenario: Alta de un periodo inactivo

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** envía a `POST /api/periodos-academicos` el nombre `2026-2`, la fecha de inicio `2026-08-01` y la fecha de fin `2026-12-15`
- **THEN** responde `201 Created` con el identificador, el rango y el estado inactivo
- **AND** el periodo aparece en el listado administrativo
- **AND** no habilita todavía ninguna reserva

#### Scenario: Periodo con fechas invertidas

- **GIVEN** una solicitud de alta con fecha de fin anterior a la fecha de inicio
- **WHEN** el sistema valida el cuerpo
- **THEN** responde `400 Bad Request` en formato RFC 7807 señalando el campo de fecha
- **AND** no se registra el periodo

#### Scenario: Periodo solapado con otro registrado

- **GIVEN** un periodo registrado del `2026-02-01` al `2026-06-15`
- **WHEN** se intenta registrar otro periodo del `2026-05-01` al `2026-09-30`
- **THEN** responde `409 Conflict` con `type` `periodo-solapado`
- **AND** no se registra el segundo periodo

#### Scenario: Activación que reemplaza al periodo vigente

- **GIVEN** el periodo `2026-1` activo y el periodo `2026-2` inactivo
- **WHEN** el administrativo envía `PUT /api/periodos-academicos/{id}/activar` sobre `2026-2`
- **THEN** responde `200 OK` con `2026-2` activo
- **AND** `2026-1` queda inactivo en la misma operación
- **AND** `GET /api/calendario/vigente` devuelve únicamente `2026-2` como periodo activo

#### Scenario: Docente intenta administrar periodos

- **GIVEN** una sesión con rol `Docente`
- **WHEN** envía `POST /api/periodos-academicos`
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** no se registra ningún periodo

### Requirement: Reservas únicamente dentro del periodo académico activo

El sistema SHALL rechazar toda reserva, serie o solicitud cuya franja no quede completamente dentro del periodo académico activo, y SHALL rechazar cualquier solicitud cuando no exista periodo activo.

#### Scenario: Reserva puntual dentro del periodo activo

- **GIVEN** el periodo `2026-2` activo del `2026-08-01` al `2026-12-15`
- **AND** un puesto libre el `2026-11-03` de `08:00` a `09:00`
- **WHEN** se envía `POST /api/reservas` con esa franja
- **THEN** responde `201 Created`
- **AND** la reserva queda confirmada

#### Scenario: Reserva puntual fuera del periodo activo

- **GIVEN** el periodo `2026-2` activo que termina el `2026-12-15`
- **WHEN** se intenta reservar el `2026-12-20` de `08:00` a `09:00`
- **THEN** responde `409 Conflict` con `type` `fuera-de-periodo-academico`
- **AND** no se crea ninguna reserva ni se modifica la agenda

#### Scenario: Sin periodo académico activo

- **GIVEN** una institución sin ningún periodo activo
- **WHEN** se intenta cualquier reserva, serie o solicitud
- **THEN** responde `409 Conflict` con `type` `sin-periodo-activo`
- **AND** no se persiste ninguna reserva

#### Scenario: Consulta de disponibilidad fuera del periodo activo

- **GIVEN** una fecha posterior al fin del periodo activo
- **WHEN** se solicita `GET /api/puestos/{id}/disponibilidad` para esa fecha
- **THEN** responde `200 OK` con todas las franjas marcadas como no disponibles y motivo `fuera-de-periodo-academico`

#### Scenario: Serie semestral que excede el periodo activo

- **GIVEN** el periodo activo del `2026-08-01` al `2026-12-15`
- **WHEN** un docente radica una serie con periodo del `2026-08-04` al `2027-01-30`
- **THEN** responde `409 Conflict` con `type` `fuera-de-periodo-academico`
- **AND** no se genera ninguna ocurrencia ni se escribe en la agenda

#### Scenario: Aprobación posterior al cierre del periodo

- **GIVEN** una solicitud `EnRevision` cuya franja quedó fuera del periodo activo tras un cambio de calendario
- **WHEN** el revisor intenta aprobarla
- **THEN** responde `409 Conflict` con `type` `fuera-de-periodo-academico`
- **AND** la solicitud conserva el estado `EnRevision`
- **AND** no se crea ocupación en la agenda

### Requirement: Configuración del calendario de no operación

El sistema SHALL permitir al rol `Administrativo` registrar franjas no operativas globales por fecha específica o por día de la semana, con hora de inicio, hora de fin, marca de feriado y motivo, rechazando registros ambiguos o duplicados.

#### Scenario: Domingo completo no operativo

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** envía a `POST /api/franjas-no-operativas` el día `Domingo`, hora inicio `00:00`, hora fin `23:59`, `esFeriado=false` y motivo `Domingo no operativo`
- **THEN** responde `201 Created` con la franja registrada
- **AND** la franja aplica a todas las áreas del campus

#### Scenario: Sábado por la tarde no operativo

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** registra el día `Sabado` de `13:00` a `21:00` con motivo `Cierre administrativo`
- **THEN** responde `201 Created`
- **AND** la mañana del sábado permanece operable

#### Scenario: Feriado en fecha específica

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** registra la fecha `2026-10-12` de `00:00` a `23:59` con `esFeriado=true` y motivo `Día de la Raza`
- **THEN** responde `201 Created`
- **AND** el feriado se identifica como tal en `GET /api/calendario/vigente`

#### Scenario: Franja con fecha y día de la semana a la vez

- **GIVEN** una solicitud que envía `fechaEspecifica` y `diaSemana` en el mismo registro
- **WHEN** el sistema valida el cuerpo
- **THEN** responde `400 Bad Request` en formato RFC 7807 indicando que los criterios son excluyentes
- **AND** no se registra la franja

#### Scenario: Franja no operativa duplicada

- **GIVEN** una franja registrada para el día `Domingo` de `00:00` a `23:59`
- **WHEN** se intenta registrar otra idéntica
- **THEN** responde `409 Conflict` con `type` `franja-duplicada`
- **AND** se conserva una sola franja

#### Scenario: Eliminación de una franja no operativa

- **GIVEN** una franja no operativa registrada
- **WHEN** el administrativo envía `DELETE /api/franjas-no-operativas/{id}`
- **THEN** responde `204 No Content`
- **AND** la franja deja de aplicar a las reservas siguientes

### Requirement: Bloqueo automático de la operación en franjas no operativas

El sistema SHALL evaluar el calendario global antes de habilitar cualquier franja horaria, de modo que ninguna reserva, serie o solicitud se confirme total o parcialmente sobre una franja no operativa.

#### Scenario: Consulta de disponibilidad en un domingo

- **GIVEN** el domingo completo registrado como no operativo
- **WHEN** se consulta la disponibilidad de un puesto para un domingo
- **THEN** responde `200 OK` con todas las franjas marcadas como no disponibles
- **AND** cada franja informa el motivo `fuera-de-operacion`

#### Scenario: Reserva puntual en horario no operativo

- **GIVEN** el sábado de `13:00` a `21:00` registrado como no operativo
- **WHEN** se intenta reservar un puesto el sábado de `15:00` a `16:00`
- **THEN** responde `409 Conflict` con `type` `franja-no-operativa`
- **AND** no se crea la reserva

#### Scenario: Reserva puntual en horario operativo del mismo día

- **GIVEN** el sábado de `13:00` a `21:00` registrado como no operativo
- **WHEN** se reserva un puesto el sábado de `09:00` a `10:00`
- **THEN** responde `201 Created`
- **AND** la reserva queda confirmada

#### Scenario: Serie semestral que omite los domingos

- **GIVEN** el domingo completo no operativo y un patrón semanal de domingos de `08:00` a `10:00`
- **WHEN** el docente radica la serie
- **THEN** responde `400 Bad Request` con `type` `serie-sin-ocurrencias`
- **AND** no se escribe ninguna ocurrencia

#### Scenario: Serie que omite días no operativos y conserva los operativos

- **GIVEN** el domingo no operativo y una serie que alterna sábados y domingos
- **WHEN** se radica la serie
- **THEN** responde `201 Created` solo con las ocurrencias de los sábados operativos
- **AND** las fechas no operativas no se escriben en la agenda

#### Scenario: Ocurrencia parcialmente cubierta por una franja no operativa

- **GIVEN** el sábado de `13:00` a `21:00` no operativo
- **WHEN** se radica una serie con una ocurrencia el sábado de `12:00` a `14:00`
- **THEN** responde `409 Conflict` con `type` `franja-no-operativa`
- **AND** el problema identifica la fecha y la franja en conflicto
- **AND** no se escribe ninguna ocurrencia de la serie

### Requirement: Consulta del calendario vigente

El sistema SHALL exponer a cualquier usuario autenticado el periodo académico activo y las franjas no operativas aplicables, para que la interfaz anticipe las restricciones antes de enviar una solicitud.

#### Scenario: Consulta del calendario vigente

- **GIVEN** un periodo activo y franjas no operativas registradas
- **WHEN** un usuario autenticado solicita `GET /api/calendario/vigente`
- **THEN** responde `200 OK` con el periodo activo, su rango y las franjas no operativas ordenadas por día y hora
- **AND** indica si existe o no un periodo activo

#### Scenario: Consulta sin sesión

- **GIVEN** una solicitud a `GET /api/calendario/vigente` sin encabezado `Authorization`
- **WHEN** el sistema evalúa la autorización
- **THEN** responde `401 Unauthorized` en formato RFC 7807
- **AND** no devuelve datos del calendario
