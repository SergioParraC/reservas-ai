# Spec Delta

## ADDED Requirements

### Requirement: Parametrización de modalidad y elemento de préstamo del área

El sistema SHALL exigir, al registrar o editar un área, una modalidad de préstamo (`AreaCompleta`, `PuestoIndividual`, `SubZonaCompartida`) y un elemento de préstamo (`EquipoComputo`, `LaboratorioEspecializado`, `ZonaDescansoJuegos`, `Auditorio`) coherentes entre sí.

#### Scenario: Alta de un auditorio como área completa

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** envía a `POST /api/espacios` un área con modalidad `AreaCompleta` y elemento `Auditorio`
- **THEN** responde `201 Created` con ambos valores en la respuesta
- **AND** el área aparece con esa parametrización en el listado administrativo y en el catálogo

#### Scenario: Alta de una sala de cómputo por puesto individual

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** registra un área con modalidad `PuestoIndividual` y elemento `EquipoComputo`
- **THEN** responde `201 Created`
- **AND** el área admite préstamo directo de sus puestos

#### Scenario: Combinación incoherente de modalidad y elemento

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** intenta registrar un área con modalidad `PuestoIndividual` y elemento `Auditorio`
- **THEN** responde `400 Bad Request` en formato RFC 7807 señalando el campo `tipoElementoPrestamo`
- **AND** no se crea el área

#### Scenario: Alta sin modalidad o sin elemento

- **GIVEN** una solicitud de alta que omite `tipoModalidadPrestamo` o `tipoElementoPrestamo`
- **WHEN** el sistema valida el cuerpo
- **THEN** responde `400 Bad Request` en formato RFC 7807 con el detalle del campo faltante
- **AND** no se crea el área ni se consume un código QR

#### Scenario: Cambio de modalidad de un área sin ocupación futura

- **GIVEN** un área con modalidad `PuestoIndividual` y sin reservas futuras
- **WHEN** el administrativo envía `PUT /api/espacios/{id}` con modalidad `SubZonaCompartida` y elemento `ZonaDescansoJuegos`
- **THEN** responde `200 OK` con la nueva parametrización
- **AND** el código QR del área y de sus puestos no cambia

#### Scenario: Cambio de modalidad con ocupación futura

- **GIVEN** un área con series semestrales o reservas vigentes a futuro
- **WHEN** se intenta cambiar su modalidad de préstamo
- **THEN** responde `409 Conflict` con `type` `modalidad-con-ocupacion`
- **AND** el área conserva su modalidad y su agenda

#### Scenario: Docente intenta parametrizar un área

- **GIVEN** una sesión con rol `Docente`
- **WHEN** envía `POST /api/espacios` o `PUT /api/espacios/{id}`
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** no se modifica la parametrización

### Requirement: Préstamo admitido según la modalidad del área

El sistema SHALL aceptar únicamente el tipo de reserva que corresponde a la modalidad del área: préstamo de unidades para `PuestoIndividual` y `SubZonaCompartida`, y series o solicitudes de área completa para `AreaCompleta`.

#### Scenario: Reserva de puesto en un área por puesto individual

- **GIVEN** un área con modalidad `PuestoIndividual` y un puesto libre
- **WHEN** un usuario envía `POST /api/reservas` sobre ese puesto
- **THEN** responde `201 Created`
- **AND** la reserva queda confirmada

#### Scenario: Serie semestral en un área por puesto individual

- **GIVEN** un área con modalidad `PuestoIndividual`
- **WHEN** un docente radica una serie semestral sobre el área
- **THEN** responde `409 Conflict` con `type` `modalidad-incompatible`
- **AND** no se genera ninguna ocurrencia

#### Scenario: Reserva de puesto en un área completa

- **GIVEN** un área con modalidad `AreaCompleta`
- **WHEN** un usuario intenta reservar uno de sus puestos con `POST /api/reservas`
- **THEN** responde `409 Conflict` con `type` `modalidad-incompatible`
- **AND** no se crea ninguna reserva

#### Scenario: Serie y solicitud sobre un área completa

- **GIVEN** un área con modalidad `AreaCompleta` y elemento `LaboratorioEspecializado`
- **WHEN** un docente radica una serie semestral y un estudiante radica una solicitud con PDF sobre ella
- **THEN** ambas operaciones responden `201 Created`
- **AND** la agenda del área refleja la serie y la solicitud aprobada

#### Scenario: Reserva de sub-zona compartida

- **GIVEN** un área con modalidad `SubZonaCompartida` y elemento `ZonaDescansoJuegos`
- **WHEN** un usuario reserva una de sus unidades con `POST /api/reservas`
- **THEN** responde `201 Created`
- **AND** una serie semestral sobre esa área responde `409 Conflict` con `type` `modalidad-incompatible`
