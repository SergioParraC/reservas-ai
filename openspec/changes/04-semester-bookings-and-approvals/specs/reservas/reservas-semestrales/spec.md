# Spec Delta

## Purpose

Permite a un docente apartar un área completa para todo el semestre según un patrón periódico, garantizando que la serie solo se confirme cuando ninguna de sus ocurrencias se cruza con la agenda vigente del área.

## ADDED Requirements

### Requirement: Generación de ocurrencias del semestre según el patrón

El sistema SHALL expandir el patrón solicitado (semanal, bisemanal o fechas fijas) en una ocurrencia por fecha dentro del periodo semestral, omitiendo los días no operativos del calendario institucional y rechazando fechas fuera del periodo.

#### Scenario: Serie semanal con un día no lectivo

- **GIVEN** una sesión de rol `Docente`
- **AND** un área activa con modalidad `AreaCompleta` y aprobación `Docente`
- **AND** un periodo académico activo del `2026-08-04` al `2026-11-24`
- **AND** el feriado `2026-10-13` registrado en el calendario institucional
- **WHEN** envía a `POST /api/reservas/recurrentes` un patrón semanal de los martes de `14:00` a `16:00`
- **THEN** responde `201 Created` con 16 ocurrencias, una por martes lectivo del periodo
- **AND** la ocurrencia del `2026-10-13` no se genera
- **AND** cada ocurrencia conserva la hora local y el desplazamiento horario institucional

#### Scenario: Serie bisemanal

- **GIVEN** el mismo periodo y un patrón bisemanal que inicia el `2026-08-04`
- **WHEN** se radica la serie
- **THEN** responde `201 Created` con 8 ocurrencias separadas 14 días
- **AND** la ocurrencia del feriado `2026-10-13` registrado en el calendario no se genera

#### Scenario: Serie por fechas fijas

- **GIVEN** un patrón de fechas fijas `2026-09-07`, `2026-10-19` y `2026-11-16` con la misma franja
- **WHEN** se radica la serie
- **THEN** responde `201 Created` con exactamente 3 ocurrencias
- **AND** no se generan ocurrencias en fechas distintas a las indicadas

#### Scenario: Fecha fija fuera del periodo semestral

- **GIVEN** un patrón de fechas fijas que incluye el `2027-02-15` y un periodo que termina el `2026-11-24`
- **WHEN** se radica la serie
- **THEN** responde `400 Bad Request` en formato RFC 7807 indicando la fecha fuera de periodo
- **AND** no se genera ninguna ocurrencia ni se escribe en la agenda

#### Scenario: Periodo que excede el máximo de semanas

- **GIVEN** un máximo configurado de 20 semanas por serie
- **WHEN** se solicita un periodo de 26 semanas
- **THEN** responde `400 Bad Request` en formato RFC 7807 indicando el máximo admitido
- **AND** no se ejecuta ninguna lectura de agenda

### Requirement: Rechazo íntegro de la serie cuando una ocurrencia colisiona

El sistema SHALL rechazar la serie completa sin persistir ninguna ocurrencia cuando al menos una de sus franjas se solape con la ocupación vigente del área, informando las fechas en conflicto.

#### Scenario: Colisión con otra serie docente

- **GIVEN** una serie activa del área los martes de `14:00` a `16:00`
- **WHEN** otro docente radica una serie del mismo día y horario
- **THEN** responde `409 Conflict` con `type` `colision-serie`
- **AND** el problema enumera las fechas en conflicto con su inicio y fin
- **AND** `GET /api/reservas/recurrentes/me` del segundo docente sigue sin series
- **AND** la serie original permanece intacta

#### Scenario: Colisión con una reserva puntual de un puesto del área

- **GIVEN** un laboratorio con una reserva confirmada de un puesto el `2026-09-15` de `14:00` a `15:00`
- **WHEN** se radica una serie semestral que incluye esa fecha de `14:00` a `16:00`
- **THEN** responde `409 Conflict` con `type` `colision-serie`
- **AND** el problema identifica el `2026-09-15` como fecha en conflicto
- **AND** no se crea ninguna ocurrencia de la serie

#### Scenario: Serie sin colisiones bloquea la agenda del área

- **GIVEN** un área sin ocupación en el periodo solicitado
- **WHEN** se radica y confirma la serie
- **THEN** responde `201 Created` con el identificador de la serie y el total de ocurrencias
- **AND** cada ocurrencia aparece como ocupación vigente del área en el rango del semestre
- **AND** una nueva serie o una reserva puntual sobre cualquiera de esas franjas responde `409 Conflict`

#### Scenario: Franjas contiguas no son colisión

- **GIVEN** una serie activa los martes de `14:00` a `16:00`
- **WHEN** se radica otra serie los martes de `16:00` a `18:00`
- **THEN** responde `201 Created`
- **AND** ambas series permanecen vigentes y contiguas

### Requirement: Confirmación atómica de la serie bajo concurrencia

El sistema SHALL garantizar que, ante dos series concurrentes que se solapan en el mismo semestre, exactamente una quede confirmada y la otra sea rechazada, sin ocurrencias parciales ni duplicadas.

#### Scenario: Dos docentes radican la misma franja a la vez

- **GIVEN** dos docentes con sesión válida y el mismo periodo, área, día y horario
- **WHEN** ambas solicitudes se procesan en paralelo
- **THEN** una responde `201 Created` y la otra `409 Conflict` con `type` `colision-serie`
- **AND** la agenda del área contiene exactamente una ocurrencia por fecha
- **AND** ninguna de las dos series queda registrada a medias

#### Scenario: Conflicto de concurrencia al confirmar

- **GIVEN** una serie cuya agenda cambió entre la validación y la escritura
- **WHEN** el sistema intenta confirmar con un token de concurrencia desactualizado
- **THEN** responde `409 Conflict` en formato RFC 7807
- **AND** la agenda conserva el estado previo y no se registra la serie

### Requirement: Autorización y ámbito de la serie semestral

El sistema SHALL permitir series únicamente a docentes sobre áreas activas con modalidad `AreaCompleta`, dentro del periodo académico activo, del periodo admitido y con la antelación mínima configurada; cualquier otro caso SHALL rechazarse sin persistir.

#### Scenario: Estudiante intenta crear una serie

- **GIVEN** una sesión de rol `Estudiante`
- **WHEN** envía `POST /api/reservas/recurrentes`
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** no se lee ni se modifica la agenda del área

#### Scenario: Área inactiva o inexistente

- **GIVEN** un área dada de baja y otra que no existe
- **WHEN** un docente radica una serie sobre cada una
- **THEN** el área inexistente responde `404 Not Found`
- **AND** el área inactiva responde `409 Conflict` con `type` `espacio-no-disponible`
- **AND** ninguna de las dos crea ocurrencias

#### Scenario: Serie que inicia antes de la antelación mínima

- **GIVEN** una antelación mínima configurada de 3 días
- **WHEN** se radica una serie cuya primera ocurrencia es en 1 día
- **THEN** responde `400 Bad Request` en formato RFC 7807 indicando la antelación mínima
- **AND** no se escribe en la agenda

#### Scenario: Solicitud sin sesión

- **GIVEN** una solicitud a `POST /api/reservas/recurrentes` sin encabezado `Authorization`
- **WHEN** el sistema evalúa la autorización
- **THEN** responde `401 Unauthorized` en formato RFC 7807

### Requirement: Consulta de las series propias

El sistema SHALL devolver al docente únicamente sus series registradas, con el patrón, el periodo, el total de ocurrencias y el estado de cada una, en orden descendente por fecha de registro.

#### Scenario: Docente consulta sus series

- **GIVEN** un docente con dos series vigentes radicadas en fechas distintas
- **WHEN** solicita `GET /api/reservas/recurrentes/me`
- **THEN** responde `200 OK` con las dos series ordenadas de la más reciente a la más antigua
- **AND** cada serie incluye área, patrón, periodo y total de ocurrencias
- **AND** no se incluyen series de otros docentes

#### Scenario: Docente sin series registradas

- **GIVEN** un docente sin series
- **WHEN** solicita `GET /api/reservas/recurrentes/me`
- **THEN** responde `200 OK` con una página vacía y el total en cero
