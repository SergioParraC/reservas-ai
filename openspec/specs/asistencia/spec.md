# asistencia Specification

## Purpose
Valida la presencia física del titular escaneando el QR del puesto reservado, libera automáticamente las franjas que nadie ocupa y publica la ocupación real de las áreas en el instante consultado.

## Requirements

### Requirement: Check-in con el QR del puesto dentro de la ventana de tolerancia

El sistema SHALL registrar el ingreso cuando el código escaneado coincide con el QR del puesto reservado y el momento del escaneo cae dentro de la ventana de tolerancia configurada en `Reservas:VentanaToleranciaCheckInMinutos`, aplicada por igual antes y después del inicio de la franja.

#### Scenario: Ingreso en el inicio exacto de la franja

- **GIVEN** una sesión de rol `Estudiante`
- **AND** una reserva confirmada del puesto `PC-07` para el `2026-11-03` de `10:00` a `11:00`
- **AND** `Reservas:VentanaToleranciaCheckInMinutos` igual a `15` en `appsettings.json`
- **WHEN** el titular envía a `POST /api/reservas/{id}/check-in` el `codigoQr` impreso en `PC-07` a las `10:00`
- **THEN** responde `200 OK` con estado `EnUso`, `fechaCheckIn` igual a `10:00` y `fechaCheckOut` nulo
- **AND** `GET /api/reservas/me` muestra la reserva en estado `EnUso`
- **AND** la franja sigue ocupada para cualquier otro usuario

#### Scenario: Ingreso anticipado dentro de la ventana

- **GIVEN** la misma reserva de `10:00` a `11:00` y `Reservas:VentanaToleranciaCheckInMinutos` igual a `15`
- **WHEN** el titular escanea el QR a las `09:47`
- **THEN** responde `200 OK` con estado `EnUso` y `fechaCheckIn` igual a `09:47`

#### Scenario: Ingreso tardío dentro de la ventana

- **GIVEN** la misma reserva y `Reservas:VentanaToleranciaCheckInMinutos` igual a `15`
- **WHEN** el titular escanea el QR a las `10:14`
- **THEN** responde `200 OK` con estado `EnUso`

#### Scenario: Ventana tomada de la configuración vigente

- **GIVEN** una reserva confirmada de `10:00` a `11:00`
- **AND** `Reservas:VentanaToleranciaCheckInMinutos` igual a `10` en `appsettings.json`
- **WHEN** la API arranca y el titular escanea a las `09:52`
- **THEN** responde `200 OK` con estado `EnUso`
- **AND** al cambiar la clave a `20` y reiniciar, un escaneo a las `09:45` de la misma franja responde `200 OK`
- **AND** ningún valor de la reserva ni del código QR altera la ventana aplicada

#### Scenario: Tolerancia inválida en la configuración

- **GIVEN** `Reservas:VentanaToleranciaCheckInMinutos` igual a `0`
- **WHEN** se inicia la API
- **THEN** el arranque falla con un mensaje que indica el rango admitido de 1 a 720 minutos
- **AND** el servicio de check-in no queda disponible con una ventana indefinida

### Requirement: Rechazo del check-in fuera de ventana, ajeno o inválido

El sistema SHALL rechazar el check-in sin modificar la reserva cuando el escaneo ocurre fuera de la ventana configurada en `Reservas:VentanaToleranciaCheckInMinutos`, el código no pertenece al puesto reservado, la reserva es de otro usuario o su estado no admite el ingreso.

#### Scenario: Escaneo antes de abrir la ventana

- **GIVEN** una reserva confirmada de `10:00` a `11:00` y `Reservas:VentanaToleranciaCheckInMinutos` igual a `15`
- **WHEN** el titular envía el check-in a las `09:44`
- **THEN** responde `409 Conflict` con `type` `check-in-fuera-de-ventana`
- **AND** el problema indica la ventana admitida de `09:45` a `10:15`
- **AND** la reserva conserva el estado `Confirmada` y `fechaCheckIn` nulo

#### Scenario: Ventana corta configurada rechaza un escaneo antes válido

- **GIVEN** una reserva confirmada de `10:00` a `11:00`
- **AND** `Reservas:VentanaToleranciaCheckInMinutos` igual a `10`
- **WHEN** el titular envía el check-in a las `09:48`
- **THEN** responde `409 Conflict` con `type` `check-in-fuera-de-ventana`
- **AND** el problema indica la ventana admitida de `09:50` a `10:10`
- **AND** la reserva permanece `Confirmada`

#### Scenario: Escaneo después de cerrar la ventana

- **GIVEN** una reserva confirmada de `10:00` a `11:00` y `Reservas:VentanaToleranciaCheckInMinutos` igual a `15`
- **AND** el barrido todavía no ha liberado la reserva
- **WHEN** el titular envía el check-in a las `10:16`
- **THEN** responde `409 Conflict` con `type` `check-in-fuera-de-ventana`
- **AND** no se registra `fechaCheckIn`

#### Scenario: Código QR de otro puesto

- **GIVEN** una reserva confirmada del puesto `PC-07`
- **WHEN** el titular envía el `codigoQr` de la placa del puesto `PC-08` dentro de la ventana
- **THEN** responde `409 Conflict` con `type` `check-in-codigo-invalido`
- **AND** la reserva permanece `Confirmada`

#### Scenario: Reserva de otro titular

- **GIVEN** una reserva confirmada de otro usuario dentro de la ventana
- **WHEN** un usuario autenticado distinto intenta el check-in
- **THEN** responde `403 Forbidden` con `type` `reserva-ajena`
- **AND** la reserva conserva su estado, su titular y su `fechaCheckIn`

#### Scenario: Check-in repetido

- **GIVEN** una reserva ya registrada en `EnUso` con `fechaCheckIn` de las `10:02`
- **WHEN** el titular repite `POST /api/reservas/{id}/check-in`
- **THEN** responde `409 Conflict` con `type` `transicion-invalida`
- **AND** `fechaCheckIn` conserva el valor `10:02`

#### Scenario: Reserva liberada por inasistencia

- **GIVEN** una reserva en estado `NoShow`
- **WHEN** el titular intenta el check-in
- **THEN** responde `409 Conflict` con `type` `transicion-invalida`
- **AND** la reserva no vuelve a `EnUso`

#### Scenario: Reserva inexistente, sin sesión o sin código

- **GIVEN** un identificador de reserva que no existe, una solicitud sin encabezado `Authorization` y otra sin el campo `codigoQr`
- **WHEN** el sistema evalúa cada solicitud
- **THEN** la inexistente responde `404 Not Found`
- **AND** la anónima responde `401 Unauthorized`
- **AND** la incompleta responde `400 Bad Request` con el campo `codigoQr` señalado
- **AND** ninguna modifica una reserva existente

### Requirement: Check-out en cualquier instante desde el estado EnUso

El sistema SHALL finalizar una reserva en estado `EnUso` en el momento en que su titular registra la salida, sin exigir ventana horaria, guardando la hora exacta y liberando de inmediato el remanente de la franja.

#### Scenario: Salida anticipada que libera el remanente

- **GIVEN** una reserva en estado `EnUso` de `10:00` a `11:00` con `finPlanificado` igual a `11:00`
- **WHEN** el titular envía `POST /api/reservas/{id}/check-out` a las `10:45`
- **THEN** responde `200 OK` con estado `Finalizada`, `fechaCheckOut` igual a `10:45`, `fin` igual a `10:45` y `finPlanificado` igual a `11:00`
- **AND** `GET /api/puestos/{id}/disponibilidad` marca la franja `10:45-11:00` como disponible
- **AND** otro usuario que solicita `10:45-11:00` a `POST /api/reservas` recibe `201 Created`
- **AND** la reserva deja de contarse como ocupada en `GET /api/monitoreo/ocupacion-actual`

#### Scenario: Salida después del fin planificado

- **GIVEN** una reserva en estado `EnUso` de `10:00` a `11:00`
- **WHEN** el titular envía el check-out a las `11:30`
- **THEN** responde `200 OK` con estado `Finalizada` y `fechaCheckOut` igual a `11:30`
- **AND** `fin` conserva el valor `11:00`, sin extender la franja
- **AND** la franja `11:00-12:00` sigue disponible para otro usuario

#### Scenario: Salida inmediata tras un ingreso anticipado

- **GIVEN** una reserva con check-in a las `09:50` para una franja de `10:00` a `11:00`
- **WHEN** el titular registra la salida a las `09:52`
- **THEN** responde `200 OK` con estado `Finalizada`, `fechaCheckOut` igual a `09:52` y `fin` igual a `10:00`
- **AND** la franja completa `10:00-11:00` vuelve a figurar como disponible

### Requirement: Rechazo del check-out sin check-in previo

El sistema SHALL rechazar el check-out sin modificar la reserva cuando su estado no sea `EnUso`, aunque el instante de la solicitud caiga dentro de la franja planificada.

#### Scenario: Reserva confirmada que aún no registra ingreso

- **GIVEN** una reserva en estado `Confirmada` cuyo inicio ya pasó y cuya ventana de check-in sigue abierta
- **WHEN** el titular envía `POST /api/reservas/{id}/check-out`
- **THEN** responde `409 Conflict` con `type` `transicion-invalida`
- **AND** la reserva conserva el estado `Confirmada` y `fechaCheckOut` nulo

#### Scenario: Reserva liberada por inasistencia

- **GIVEN** una reserva en estado `NoShow`
- **WHEN** el titular envía el check-out
- **THEN** responde `409 Conflict` con `type` `transicion-invalida`
- **AND** la reserva permanece `NoShow` sin `fechaCheckOut`

#### Scenario: Reserva cancelada por el titular

- **GIVEN** una reserva cancelada antes de su inicio
- **WHEN** el titular envía el check-out
- **THEN** responde `409 Conflict` con `type` `transicion-invalida`
- **AND** la reserva permanece `Cancelada` sin `fechaCheckOut`
- **AND** la franja no recupera la ocupación que tenía antes de la cancelación

#### Scenario: Salida repetida o de una reserva ajena

- **GIVEN** una reserva ya finalizada y otra en uso de otro usuario
- **WHEN** se intenta el check-out sobre cada una
- **THEN** la finalizada responde `409 Conflict` con `type` `transicion-invalida`
- **AND** la ajena responde `403 Forbidden` con `type` `reserva-ajena`
- **AND** ninguna reescribe `fechaCheckOut` ni `fin`

### Requirement: Liberación automática de reservas sin check-in

El sistema SHALL liberar automáticamente toda reserva confirmada que continúe sin check-in al expirar la ventana configurada en `Reservas:VentanaToleranciaCheckInMinutos`, pasándola a `NoShow` y dejando su franja disponible para otros usuarios.

#### Scenario: Barrido que libera una reserva vencida

- **GIVEN** una reserva confirmada del `2026-11-03` de `10:00` a `11:00` sin `fechaCheckIn`
- **AND** `Reservas:VentanaToleranciaCheckInMinutos` igual a `15`
- **WHEN** el barrido en segundo plano se ejecuta a las `10:16`
- **THEN** la reserva pasa a estado `NoShow`
- **AND** `fechaCheckIn` y `fechaCheckOut` permanecen nulos
- **AND** otro usuario que solicita la misma franja a `POST /api/reservas` recibe `201 Created`

#### Scenario: Barrido antes de vencer la ventana

- **GIVEN** la misma reserva sin check-in y `Reservas:VentanaToleranciaCheckInMinutos` igual a `15`
- **WHEN** el barrido se ejecuta a las `10:10`
- **THEN** la reserva continúa en estado `Confirmada`
- **AND** la franja sigue ocupada: otro usuario recibe `409 Conflict`

#### Scenario: Reserva con ingreso registrado

- **GIVEN** una reserva en estado `EnUso` cuya franja está en curso
- **WHEN** el barrido se ejecuta después de la ventana
- **THEN** la reserva permanece en `EnUso`
- **AND** `fechaCheckIn` no cambia

#### Scenario: Idempotencia de barridos consecutivos

- **GIVEN** una reserva ya liberada por el barrido anterior
- **WHEN** el barrido se ejecuta de nuevo
- **THEN** la reserva continúa en `NoShow` con la misma marca de liberación
- **AND** no se registra una segunda liberación ni otro cambio de estado

#### Scenario: Recuperación tras una caída del proceso

- **GIVEN** el servicio detenido durante 40 minutos y tres reservas cuya ventana venció en ese lapso
- **WHEN** el servicio vuelve a ejecutarse
- **THEN** las tres reservas pasan a `NoShow` en el primer barrido
- **AND** sus franjas quedan disponibles

#### Scenario: Fallo al persistir un lote

- **GIVEN** un barrido cuyo guardado falla por un conflicto de base de datos
- **WHEN** el ciclo termina con error
- **THEN** las reservas del lote conservan su estado anterior y la franja sigue ocupada
- **AND** el ciclo siguiente las libera
- **AND** el servicio sigue ejecutándose y registra el fallo

### Requirement: Mapa de ocupación actual por área

El sistema SHALL devolver, para el instante de la consulta, los totales y el desglose por área de aforo, puestos en uso, pendientes de check-in y libres, con el momento de generación del dato.

#### Scenario: Consulta administrativa con áreas ocupadas

- **GIVEN** una sesión de rol `Administrativo`
- **AND** un laboratorio con 20 puestos activos, 12 reservas en `EnUso` y 3 reservas en `Confirmada` dentro de su ventana de check-in
- **WHEN** solicita `GET /api/monitoreo/ocupacion-actual`
- **THEN** responde `200 OK`
- **AND** el área informa `aforo` 20, `enUso` 12, `pendientesCheckIn` 3 y `libres` 5
- **AND** `porcentajeOcupacion` es `60`
- **AND** la respuesta incluye `generadoEn`, `toleranciaMinutos` igual a `15` (leída de `Reservas:VentanaToleranciaCheckInMinutos`) y una entrada por cada reserva en uso con su puesto y titular

#### Scenario: El mapa refleja una liberación por inasistencia

- **GIVEN** un área con 20 puestos, 12 en uso y 3 pendientes de check-in
- **WHEN** el barrido libera 2 de las 3 reservas pendientes y el administrativo vuelve a consultar el mapa
- **THEN** el área informa `enUso` 12, `pendientesCheckIn` 1 y `libres` 7
- **AND** las reservas liberadas no aparecen en la lista de reservas en uso

#### Scenario: Sesión sin cerrar fuera de su franja

- **GIVEN** un área con 20 puestos activos y una reserva que sigue en `EnUso` con franja `10:00-11:00`
- **WHEN** el administrativo consulta el mapa a las `11:30`
- **THEN** el área informa `enUso` 0 y `sesionesSinCerrar` 1
- **AND** `libres` es 20
- **AND** la reserva se informa como sesión sin cerrar, sin contarse como ocupada

#### Scenario: Reservas futuras no cuentan como ocupación

- **GIVEN** un área de 20 puestos cuya única reserva confirmada es de `14:00` a `15:00`
- **WHEN** el mapa se consulta a las `10:00`
- **THEN** el área informa `enUso` 0, `pendientesCheckIn` 0 y `libres` 20
- **AND** `porcentajeOcupacion` es `0`

#### Scenario: Área sin puestos activos

- **GIVEN** un área sin puestos activos
- **WHEN** se consulta el mapa
- **THEN** el área aparece con `puestosActivos` 0, `libres` 0, `porcentajeOcupacion` 0 y lista de reservas en uso vacía

#### Scenario: Consulta sin autorización

- **GIVEN** una sesión de rol `Estudiante` y una solicitud sin encabezado `Authorization`
- **WHEN** cada una solicita `GET /api/monitoreo/ocupacion-actual`
- **THEN** la del estudiante responde `403 Forbidden` en formato RFC 7807
- **AND** la anónima responde `401 Unauthorized`
- **AND** ninguna devuelve datos de ocupación
