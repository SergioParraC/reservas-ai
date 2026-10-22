# Spec Delta

## Purpose

Otorga al personal administrativo el gobierno de las cuentas de la plataforma: consultar quién está registrado y asignar el rol que determina todos sus permisos, con independencia de los roles declarados por Entra ID.

## ADDED Requirements

### Requirement: Consulta administrativa de cuentas registradas

El sistema SHALL exponer al rol `Administrativo` un listado paginado y filtrable de las cuentas registradas, con su rol vigente y su estado de vinculación federada.

#### Scenario: Listado por omisión

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** invoca `GET /api/usuarios` sin filtros
- **THEN** responde `200 OK` con una página de cuentas, el total de registros y los datos de paginación
- **AND** cada elemento incluye nombre, correo, rol vigente, tipo de actor, estado activo, indicador de vínculo federado y fecha de creación
- **AND** ninguna respuesta contiene contraseñas, hashes ni sellos de seguridad

#### Scenario: Filtros de la consola de usuarios

- **GIVEN** cuentas con roles y estados de vinculación distintos
- **WHEN** se filtra por rol `Estudiante` y por cuentas sin vínculo federado
- **THEN** el resultado contiene únicamente las cuentas que cumplen ambas condiciones
- **AND** el total corresponde al conjunto filtrado

#### Scenario: Búsqueda por nombre o correo

- **GIVEN** cuentas registradas con nombres y correos distintos
- **WHEN** se filtra por un fragmento de nombre o de correo
- **THEN** el resultado incluye las coincidencias sin distinguir mayúsculas ni acentos

### Requirement: Asignación del rol como fuente de verdad

El sistema SHALL aplicar el rol que el personal administrativo asigna en la plataforma como única fuente de autorización, sin derivarlo de los app roles del token de Entra ID.

#### Scenario: Cambio de rol de una cuenta

- **GIVEN** una cuenta con rol `Estudiante`
- **WHEN** el personal administrativo invoca `PUT /api/usuarios/{id}/rol` con el rol `Docente`
- **THEN** responde `200 OK` con el rol actualizado
- **AND** el almacén de identidad conserva exactamente un rol vigente para esa cuenta
- **AND** las autorizaciones posteriores de esa cuenta usan `Docente`

#### Scenario: El rol del token no eleva el rol local

- **GIVEN** una cuenta con rol local `Estudiante`
- **WHEN** el token de Entra ID declara un app role administrativo para esa identidad
- **THEN** la sesión conserva el rol `Estudiante` y no accede a recursos administrativos
- **AND** el sistema no modifica el rol asignado en la plataforma

#### Scenario: Rol inexistente o no soportado

- **GIVEN** una solicitud con un rol distinto de `Administrativo`, `Docente` o `Estudiante`
- **WHEN** se intenta asignar
- **THEN** responde `400 Bad Request` en formato RFC 7807
- **AND** la cuenta conserva su rol anterior

### Requirement: Protección de la administración del sistema

El sistema SHALL impedir que un cambio de rol deje el sistema sin ningún administrador activo o que un administrador se degrade a sí mismo.

#### Scenario: Degradación del último administrador

- **GIVEN** un único administrador activo en la plataforma
- **WHEN** se intenta cambiar su rol a `Docente` o `Estudiante`
- **THEN** responde `409 Conflict` con un problema RFC 7807 que indica la necesidad de conservar un administrador
- **AND** el rol del administrador no cambia

#### Scenario: Auto-degradación con otro administrador disponible

- **GIVEN** un administrador autenticado y al menos otro administrador activo
- **WHEN** intenta cambiar su propio rol
- **THEN** responde `409 Conflict` en formato RFC 7807
- **AND** el rol permanece sin cambios

#### Scenario: Degradación de un administrador con reemplazo disponible

- **GIVEN** dos administradores activos
- **WHEN** se cambia el rol de uno de ellos a `Docente`
- **THEN** responde `200 OK` y el sistema conserva al menos un administrador activo

### Requirement: Efectividad del cambio de rol en las sesiones

El sistema SHALL aplicar el rol vigente en la siguiente validación de credenciales y SHALL indicar al cliente que renueve la sesión afectada.

#### Scenario: Sesión local con rol modificado

- **GIVEN** una sesión local emitida con rol `Estudiante` que luego fue cambiado a `Docente`
- **WHEN** el portador renueva su token mediante `POST /api/auth/refresh`
- **THEN** el nuevo token contiene el rol `Docente`
- **AND** las autorizaciones posteriores usan el rol renovado

#### Scenario: Sesión federada con rol modificado

- **GIVEN** una sesión federada de una cuenta cuyo rol administrativo cambió
- **WHEN** la cuenta invoca un endpoint protegido en la siguiente solicitud
- **THEN** el sistema resuelve el perfil con el rol vigente en la plataforma
- **AND** el acceso se concede o deniega según ese rol

### Requirement: Autorización de la consola de usuarios

El sistema SHALL restringir el listado y el cambio de rol al rol `Administrativo`, y SHALL mantener esos datos fuera del alcance de cualquier otra identidad.

#### Scenario: Acceso con rol académico

- **GIVEN** una sesión autenticada con rol `Docente` o `Estudiante`
- **WHEN** invoca el listado de usuarios o el cambio de rol
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** no se exponen datos de otras cuentas

#### Scenario: Acceso sin credenciales

- **GIVEN** una solicitud sin encabezado `Authorization`
- **WHEN** invoca la consola de usuarios
- **THEN** responde `401 Unauthorized` en formato RFC 7807

#### Scenario: Cuenta inexistente

- **GIVEN** un identificador que no corresponde a ninguna cuenta
- **WHEN** se intenta cambiar su rol
- **THEN** responde `404 Not Found` en formato RFC 7807
- **AND** no se modifica ningún registro
