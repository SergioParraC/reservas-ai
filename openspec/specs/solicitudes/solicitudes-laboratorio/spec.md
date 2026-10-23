# solicitudes/solicitudes-laboratorio Specification

## Purpose
Permite a un estudiante o docente radicar formalmente una solicitud de laboratorio o auditorio con su pre-informe PDF, dejando la petición en estado `Radicado` y garantizando que solo su autor y los revisores competentes accedan al documento.

## Requirements

### Requirement: Radicación de solicitud con pre-informe PDF

El sistema SHALL registrar una solicitud en estado `Radicado` cuando reciba el área, la franja, la materia, el docente responsable, la justificación y un archivo PDF válido, guardando el archivo en el almacén de documentos y solo su ruta relativa en la base de datos.

#### Scenario: Radicación válida de un laboratorio

- **GIVEN** una sesión de rol `Estudiante`
- **AND** un laboratorio activo con modalidad `AreaCompleta`, elemento `LaboratorioEspecializado` y aprobación `Docente`
- **WHEN** envía a `POST /api/solicitudes` el área, la franja `2026-11-05T14:00:00-05:00` a `2026-11-05T16:00:00-05:00`, la materia, el docente responsable, la justificación y un PDF de 800 KB
- **THEN** responde `201 Created` con el identificador, el estado `Radicado` y el nombre y tamaño del archivo
- **AND** el archivo queda en el volumen bajo `solicitudes/{id}/` con nombre generado por el servidor
- **AND** la respuesta no expone la ruta física del archivo

#### Scenario: Radicación sin archivo adjunto

- **GIVEN** una sesión autenticada y un laboratorio válido
- **WHEN** envía `POST /api/solicitudes` sin la parte del archivo
- **THEN** responde `400 Bad Request` en formato RFC 7807 señalando el campo `archivo`
- **AND** no se crea la solicitud ni se escribe en el almacén

#### Scenario: Archivo que supera el tamaño máximo

- **GIVEN** un límite configurado de 5 MB por pre-informe
- **WHEN** se envía un PDF de 6 MB
- **THEN** responde `413 Payload Too Large` con `type` `archivo-demasiado-grande`
- **AND** no se crea la solicitud ni se escribe en el almacén

#### Scenario: Archivo cuya firma no corresponde a un PDF

- **GIVEN** un archivo declarado como `application/pdf` cuyo contenido no empieza por `%PDF-`
- **WHEN** se envía la solicitud
- **THEN** responde `415 Unsupported Media Type` con `type` `archivo-no-valido`
- **AND** no se crea la solicitud ni se escribe en el almacén

#### Scenario: Área que no admite solicitud

- **GIVEN** un área con modalidad `PuestoIndividual` y otra con aprobación automática
- **WHEN** se radica una solicitud sobre cada una
- **THEN** la primera responde `409 Conflict` con `type` `modalidad-incompatible`
- **AND** la segunda responde `409 Conflict` con `type` `espacio-no-solicitable`
- **AND** no se persiste la solicitud ni el archivo

### Requirement: Validación de la franja solicitada

El sistema SHALL validar la franja solicitada contra la jornada institucional, la duración admitida y la antelación mínima antes de persistir la solicitud.

#### Scenario: Franja fuera de la jornada institucional

- **GIVEN** una jornada configurada de `07:00` a `21:00`
- **WHEN** se radica una solicitud de `22:00` a `23:00`
- **THEN** responde `400 Bad Request` en formato RFC 7807 indicando la jornada admitida
- **AND** no se crea la solicitud

#### Scenario: Franja con menos antelación que la configurada

- **GIVEN** una antelación mínima de 30 minutos
- **WHEN** se radica una solicitud que inicia en 10 minutos
- **THEN** responde `400 Bad Request` en formato RFC 7807 indicando la antelación mínima
- **AND** no se crea la solicitud

#### Scenario: Franja con fin anterior al inicio

- **GIVEN** una solicitud cuyo fin es anterior al inicio
- **WHEN** el sistema valida el cuerpo
- **THEN** responde `400 Bad Request` en formato RFC 7807 señalando el campo de la franja
- **AND** no se ejecuta ninguna escritura

### Requirement: Consulta de las solicitudes propias

El sistema SHALL devolver al solicitante únicamente sus solicitudes, con área, franja, estado, motivo de rechazo cuando exista y fecha de radicación, de la más reciente a la más antigua.

#### Scenario: Solicitante consulta su historial

- **GIVEN** un estudiante con una solicitud `Radicado` y otra `Rechazado` con motivo
- **WHEN** solicita `GET /api/solicitudes/me`
- **THEN** responde `200 OK` con las dos solicitudes ordenadas por fecha de radicación descendente
- **AND** la solicitud rechazada incluye su motivo
- **AND** no aparecen solicitudes de otros usuarios

#### Scenario: Consulta sin sesión

- **GIVEN** una solicitud a `GET /api/solicitudes/me` sin encabezado `Authorization`
- **WHEN** el sistema evalúa la autorización
- **THEN** responde `401 Unauthorized` en formato RFC 7807
- **AND** no devuelve ninguna solicitud

### Requirement: Descarga restringida del pre-informe

El sistema SHALL entregar el pre-informe PDF únicamente al solicitante y a los revisores competentes del área, sin publicarlo en el almacén estático y sin caché.

#### Scenario: El autor descarga su pre-informe

- **GIVEN** una solicitud del usuario autenticado
- **WHEN** solicita `GET /api/solicitudes/{id}/preinforme`
- **THEN** responde `200 OK` con `Content-Type: application/pdf`
- **AND** la respuesta incluye `Content-Disposition: attachment` con el nombre original del archivo
- **AND** la cabecera de caché impide almacenar la respuesta

#### Scenario: Un docente competente descarga el pre-informe

- **GIVEN** una solicitud sobre un área con aprobación `Docente`
- **AND** un docente autenticado
- **WHEN** solicita el pre-informe de esa solicitud
- **THEN** responde `200 OK` con el archivo PDF

#### Scenario: Un tercero intenta descargar el pre-informe

- **GIVEN** una solicitud de otro estudiante
- **WHEN** un usuario sin competencia sobre el área solicita el pre-informe
- **THEN** responde `403 Forbidden` en formato RFC 7807 con `type` `solicitud-ajena`
- **AND** no se entrega ningún fragmento del archivo

#### Scenario: Pre-informe de una solicitud inexistente

- **GIVEN** un identificador que no corresponde a ninguna solicitud
- **WHEN** se solicita su pre-informe
- **THEN** responde `404 Not Found` en formato RFC 7807
- **AND** no se ejecuta ninguna lectura del almacén

### Requirement: Ocupación del área al aprobar la solicitud

El sistema SHALL registrar la ocupación del área en la misma transacción que aprueba la solicitud, revalidando la colisión contra la agenda vigente y conservando la solicitud en su estado anterior cuando la franja ya esté ocupada.

#### Scenario: Aprobación que bloquea la franja

- **GIVEN** una solicitud `EnRevision` sobre una franja libre del área
- **WHEN** el revisor competente la aprueba
- **THEN** responde `200 OK` con el estado `Aprobado`
- **AND** la franja queda ocupada en la agenda del área
- **AND** una serie semestral o una reserva puntual posterior sobre esa franja responde `409 Conflict`

#### Scenario: Aprobación sobre una franja ya ocupada

- **GIVEN** una solicitud `EnRevision` cuya franja fue ocupada por una serie semestral aprobada después de la radicación
- **WHEN** el revisor intenta aprobarla
- **THEN** responde `409 Conflict` con `type` `franja-no-disponible`
- **AND** la solicitud permanece `EnRevision`
- **AND** no se crea ninguna ocupación en la agenda
