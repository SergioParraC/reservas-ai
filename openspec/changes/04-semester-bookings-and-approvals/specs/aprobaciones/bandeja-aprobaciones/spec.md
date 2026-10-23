# Spec Delta

## Purpose

Ofrece a docentes y administrativos una bandeja única donde revisan las solicitudes que les competen, deciden su estado con trazabilidad del revisor y notifican al solicitante por correo electrónico.

## ADDED Requirements

### Requirement: Bandeja filtrada por competencia del revisor

El sistema SHALL devolver a `Docente` solo las solicitudes de áreas con aprobación `Docente` y a `Administrativo` las de cualquier tipo, permitiendo filtrar por estado, área y rango de fechas con paginación.

#### Scenario: Docente consulta su bandeja

- **GIVEN** un laboratorio con aprobación `Docente` y un auditorio con aprobación `Administrativo`
- **AND** una solicitud radicada en cada área
- **WHEN** un docente solicita `GET /api/bandeja-aprobaciones`
- **THEN** responde `200 OK` con la solicitud del laboratorio
- **AND** la solicitud del auditorio no aparece en el resultado

#### Scenario: Administrativo consulta la bandeja completa

- **GIVEN** las mismas dos solicitudes
- **WHEN** un administrativo solicita `GET /api/bandeja-aprobaciones`
- **THEN** responde `200 OK` con ambas solicitudes
- **AND** cada elemento incluye área, solicitante, franja, estado y fecha de radicación

#### Scenario: Filtro por estado y rango de fechas

- **GIVEN** solicitudes en estados `Radicado` y `Aprobado` en fechas distintas
- **WHEN** se solicita la bandeja con `estado=Radicado` y un rango que cubre solo una de ellas
- **THEN** responde `200 OK` solo con las solicitudes que cumplen ambos filtros
- **AND** el total de la página refleja el resultado filtrado

#### Scenario: Estudiante intenta abrir la bandeja

- **GIVEN** una sesión de rol `Estudiante`
- **WHEN** solicita `GET /api/bandeja-aprobaciones`
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** no devuelve ninguna solicitud

### Requirement: Transiciones de estado auditables de la solicitud

El sistema SHALL permitir únicamente `Radicado` a `EnRevision`, y de `EnRevision` a `Aprobado` o `Rechazado`, registrando revisor y momento de la decisión y exigiendo motivo en el rechazo.

#### Scenario: Revisor toma la solicitud en revisión

- **GIVEN** una solicitud `Radicado` de su competencia
- **WHEN** el revisor envía `PUT /api/solicitudes/{id}/estado` con `estado=EnRevision`
- **THEN** responde `200 OK` con el estado `EnRevision`
- **AND** la respuesta registra el revisor y el momento de la revisión

#### Scenario: Aprobación de la solicitud

- **GIVEN** una solicitud `EnRevision`
- **WHEN** el revisor competente envía `estado=Aprobado`
- **THEN** responde `200 OK` con el estado `Aprobado`
- **AND** la solicitud queda como estado terminal y no admite otra transición

#### Scenario: Rechazo sin motivo

- **GIVEN** una solicitud `EnRevision`
- **WHEN** el revisor envía `estado=Rechazado` sin `motivo`
- **THEN** responde `400 Bad Request` en formato RFC 7807 señalando el campo `motivo`
- **AND** la solicitud permanece `EnRevision`

#### Scenario: Rechazo con motivo

- **GIVEN** una solicitud `EnRevision`
- **WHEN** el revisor envía `estado=Rechazado` con un motivo de al menos 10 caracteres
- **THEN** responde `200 OK` con el estado `Rechazado` y el motivo registrado
- **AND** la consulta del solicitante muestra el motivo

#### Scenario: Transición desde un estado terminal

- **GIVEN** una solicitud ya `Aprobado`
- **WHEN** se intenta cambiarla a `Rechazado`
- **THEN** responde `409 Conflict` con `type` `transicion-invalida`
- **AND** la solicitud conserva el estado `Aprobado` y su revisor original

#### Scenario: Salto de estado no permitido

- **GIVEN** una solicitud `Radicado`
- **WHEN** el revisor intenta enviarla directamente a `Aprobado`
- **THEN** responde `409 Conflict` con `type` `transicion-invalida`
- **AND** la solicitud permanece `Radicado`

#### Scenario: Revisor de otra competencia

- **GIVEN** una solicitud sobre un área con aprobación `Administrativo`
- **WHEN** un docente intenta cambiar su estado
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** la solicitud conserva su estado y su revisor

#### Scenario: El solicitante intenta decidir su propia solicitud

- **GIVEN** un docente que radicó una solicitud sobre un área con aprobación `Docente`
- **WHEN** intenta aprobarla
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** la solicitud permanece en su estado anterior

### Requirement: Notificación asíncrona al solicitante

El sistema SHALL encolar en la misma transacción de la decisión un correo HTML dirigido al solicitante y despacharlo fuera del ciclo de la petición HTTP, reintentando ante fallos del servidor SMTP sin revertir la decisión.

#### Scenario: La decisión responde sin esperar al servidor SMTP

- **GIVEN** un servidor SMTP no disponible
- **WHEN** el revisor aprueba una solicitud
- **THEN** responde `200 OK` con el estado `Aprobado` dentro del tiempo normal de la petición
- **AND** queda un correo en estado pendiente asociado a la solicitud

#### Scenario: Despacho exitoso del correo

- **GIVEN** un correo pendiente y un servidor SMTP disponible
- **WHEN** el despachador procesa la cola
- **THEN** el correo se envía al buzón del solicitante con plantilla HTML que indica el nuevo estado
- **AND** el correo rechazado incluye el motivo del rechazo
- **AND** el correo queda marcado como enviado con su fecha de envío

#### Scenario: Fallo de envío con reintento

- **GIVEN** un correo pendiente y un servidor SMTP que rechaza la conexión
- **WHEN** el despachador intenta enviarlo
- **THEN** incrementa el contador de intentos y programa el siguiente intento con retroceso exponencial
- **AND** la solicitud conserva el estado decidido por el revisor
- **AND** el correo no se elimina de la cola

#### Scenario: El correo no expone rutas internas

- **GIVEN** una notificación de aprobación
- **WHEN** se compone el cuerpo HTML
- **THEN** el mensaje identifica área, franja, estado y motivo cuando aplica
- **AND** no incluye la ruta física ni la ruta relativa del pre-informe
- **AND** escapa el contenido ingresado por el usuario antes de insertarlo en el HTML
