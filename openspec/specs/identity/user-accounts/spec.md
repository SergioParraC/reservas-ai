# identity/user-accounts Specification

## Purpose
Permite que cualquier miembro de la comunidad institucional cree su cuenta en la plataforma, habilitando el primer administrador del sistema y el doble método de acceso mediante la vinculación posterior con Microsoft Entra ID.

## Requirements

### Requirement: Autorregistro de cuentas con rol inicial

El sistema SHALL crear la cuenta desde un formulario público y SHALL asignarle el rol `Administrativo` cuando sea la primera cuenta del sistema, o `Estudiante` en cualquier otro caso.

#### Scenario: Primer registro del sistema

- **GIVEN** un sistema sin ninguna cuenta registrada
- **WHEN** se envía a `POST /api/usuarios/registro` un nombre completo, un correo institucional y una contraseña válidos
- **THEN** responde `201 Created` con el identificador, el correo y el rol `Administrativo`
- **AND** la cuenta queda activa y puede iniciar sesión con la contraseña registrada
- **AND** la respuesta no emite token de acceso

#### Scenario: Registro posterior al primero

- **GIVEN** un sistema con al menos una cuenta registrada
- **WHEN** se registra una cuenta nueva con datos válidos
- **THEN** responde `201 Created` con el rol `Estudiante`
- **AND** el rol asignado no depende de ninguna notificación de Entra ID

#### Scenario: Registros simultáneos sobre un sistema vacío

- **GIVEN** un sistema sin cuentas y dos solicitudes de registro concurrentes
- **WHEN** ambas se procesan
- **THEN** exactamente una cuenta recibe el rol `Administrativo`
- **AND** la otra recibe el rol `Estudiante` o es rechazada por conflicto, sin dejar el sistema con dos administradores iniciales

### Requirement: Restricciones del autorregistro

El sistema SHALL rechazar un registro cuyo correo no pertenezca al dominio institucional configurado, que ya exista o cuya contraseña no cumpla la política vigente.

#### Scenario: Correo de un dominio no institucional

- **GIVEN** un dominio institucional configurado
- **WHEN** se registra una cuenta con un correo de otro dominio
- **THEN** responde `400 Bad Request` en formato RFC 7807 señalando el campo de correo
- **AND** no se crea la cuenta

#### Scenario: Correo ya registrado

- **GIVEN** una cuenta existente con el correo `ana@universidad.edu`
- **WHEN** se intenta registrar otra cuenta con el mismo correo
- **THEN** responde `409 Conflict` en formato RFC 7807
- **AND** la cuenta existente conserva su rol y su contraseña

#### Scenario: Contraseña que no cumple la política

- **GIVEN** una contraseña más corta que el mínimo configurado o sin la composición exigida
- **WHEN** se envía el formulario de registro
- **THEN** responde `400 Bad Request` con el detalle de la regla incumplida
- **AND** no se crea la cuenta

#### Scenario: Autorregistro deshabilitado por configuración

- **GIVEN** la clave `Identidad:PermitirAutorregistro` en `false`
- **WHEN** se intenta registrar una cuenta
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** no se crea la cuenta

### Requirement: Vinculación de la identidad institucional

El sistema SHALL asociar el identificador federado inmutable (`oid`) de un token de Entra ID válido a la cuenta autenticada localmente, previa coincidencia del correo institucional.

#### Scenario: Vinculación exitosa

- **GIVEN** una sesión local activa de una cuenta sin vínculo federado
- **WHEN** envía a `POST /api/usuarios/me/vinculacion-entra` un token de Entra ID válido cuyo correo coincide con el de la cuenta
- **THEN** responde `200 OK` con el estado de vinculación y el correo institucional confirmado
- **AND** el `oid` del token queda almacenado como identificador federado de esa cuenta
- **AND** la cuenta puede iniciar sesión por el proveedor institucional en el siguiente intento

#### Scenario: Token con un correo distinto al de la cuenta

- **GIVEN** una sesión local activa de una cuenta con correo `ana@universidad.edu`
- **WHEN** intenta vincular un token de Entra ID cuyo correo es `luis@universidad.edu`
- **THEN** responde `409 Conflict` con un problema RFC 7807 que indica la discrepancia de correo
- **AND** la cuenta conserva su estado sin vínculo

#### Scenario: Identidad ya vinculada a otra cuenta

- **GIVEN** un `oid` ya asociado a otra cuenta de la plataforma
- **WHEN** se intenta vincular ese mismo `oid` a una cuenta distinta
- **THEN** responde `409 Conflict` en formato RFC 7807
- **AND** ninguna de las dos cuentas cambia su vínculo

#### Scenario: Vinculación repetida de la misma cuenta

- **GIVEN** una cuenta ya vinculada a un `oid`
- **WHEN** repite la vinculación con un token del mismo `oid`
- **THEN** responde `200 OK` sin duplicar el vínculo ni alterar el identificador
- **AND** la operación es idempotente

#### Scenario: Token federado inválido

- **GIVEN** un token expirado, con firma inválida o emitido por otro inquilino
- **WHEN** se intenta vincular la identidad
- **THEN** responde `401 Unauthorized` o `400 Bad Request` en formato RFC 7807
- **AND** la cuenta permanece sin vínculo federado

### Requirement: Desvinculación de la identidad institucional

El sistema SHALL permitir a la cuenta autenticada retirar su vínculo federado sin eliminar la cuenta ni su contraseña local.

#### Scenario: Desvinculación exitosa

- **GIVEN** una cuenta vinculada a un `oid`
- **WHEN** invoca `DELETE /api/usuarios/me/vinculacion-entra`
- **THEN** responde `204 No Content`
- **AND** el identificador federado queda vacío y puede volver a vincularse
- **AND** el inicio de sesión con contraseña local sigue funcionando

#### Scenario: Desvinculación sin vínculo previo

- **GIVEN** una cuenta sin identidad federada asociada
- **WHEN** invoca la desvinculación
- **THEN** responde `204 No Content` sin modificar la cuenta

### Requirement: Doble método de acceso para la misma cuenta

El sistema SHALL permitir que una cuenta vinculada acceda con su contraseña local o con su identidad institucional, resolviendo en ambos casos el mismo usuario interno y el mismo rol.

#### Scenario: Acceso con contraseña local

- **GIVEN** una cuenta vinculada y activa
- **WHEN** inicia sesión con su correo y contraseña locales
- **THEN** responde `200 OK` con un token local que contiene el rol asignado en la plataforma

#### Scenario: Acceso con el correo de la universidad

- **GIVEN** una cuenta vinculada y activa
- **WHEN** inicia sesión por Microsoft Entra ID con la misma identidad institucional
- **THEN** el sistema resuelve la misma cuenta interna por el `oid` vinculado
- **AND** el rol aplicado es el asignado en la plataforma, idéntico al del acceso local

### Requirement: Alcance del autoservicio de vinculación

El sistema SHALL limitar la vinculación y la desvinculación a la cuenta del portador del token y SHALL rechazar cualquier intento de operar sobre otra cuenta.

#### Scenario: Vinculación sin sesión

- **GIVEN** una solicitud sin token local ni federado
- **WHEN** invoca la vinculación de identidad
- **THEN** responde `401 Unauthorized` en formato RFC 7807
- **AND** no se asocia ningún `oid`

#### Scenario: Intento de vincular la cuenta de otro usuario

- **GIVEN** una sesión activa de una cuenta
- **WHEN** la solicitud incluye el identificador de otra cuenta
- **THEN** responde `403 Forbidden` o `404 Not Found` en formato RFC 7807
- **AND** la cuenta ajena permanece sin cambios
