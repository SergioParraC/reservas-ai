# identity/local-authentication Specification

## Purpose
Permite que el personal administrativo acceda al sistema con credenciales locales gestionadas por la institución, obteniendo un token propio que autoriza sus operaciones con independencia de la disponibilidad del proveedor institucional.

## Requirements

### Requirement: Inicio de sesión con credenciales locales

El sistema SHALL emitir un token de acceso propio cuando un usuario administrativo acreditado presenta credenciales locales válidas.

#### Scenario: Credenciales administrativas válidas

- **GIVEN** un usuario administrativo activo con rol `Administrativo` registrado en el almacén local de identidad
- **WHEN** envía su correo institucional y contraseña al endpoint `POST /api/auth/login`
- **THEN** responde `200 OK` con un token de acceso, su tipo, su fecha de expiración y los roles concedidos
- **AND** el token identifica al usuario de forma unívoca y contiene su rol `Administrativo`
- **AND** la respuesta no incluye contraseña, hash ni el identificador interno de seguridad del almacén

#### Scenario: Credenciales de un segundo perfil administrativo

- **GIVEN** un usuario activo con rol `Administrativo`
- **WHEN** inicia sesión con credenciales locales válidas
- **THEN** responde `200 OK` con un token cuyo rol concedido es `Administrativo`
- **AND** el token queda registrado con la marca de tiempo de emisión

### Requirement: Rechazo de credenciales locales inválidas

El sistema SHALL rechazar el inicio de sesión con contraseña incorrecta o cuenta inexistente mediante un único mensaje genérico, sin revelar cuál de las dos condiciones se produjo.

#### Scenario: Contraseña incorrecta

- **GIVEN** un usuario administrativo existente y activo
- **WHEN** intenta iniciar sesión con una contraseña incorrecta
- **THEN** responde `401 Unauthorized` con un problema RFC 7807 de título genérico de credenciales inválidas
- **AND** no emite token de acceso

#### Scenario: Correo no registrado

- **GIVEN** un correo que no corresponde a ningún usuario del sistema
- **WHEN** se intenta iniciar sesión con ese correo y cualquier contraseña
- **THEN** responde `401 Unauthorized` con el mismo título y estructura que el caso de contraseña incorrecta
- **AND** el tiempo de respuesta no permite distinguir entre usuario inexistente y contraseña errónea de forma evidente

#### Scenario: Cuenta deshabilitada

- **GIVEN** un usuario administrativo marcado como inactivo
- **WHEN** presenta credenciales correctas
- **THEN** responde `401 Unauthorized` con un problema RFC 7807
- **AND** no se emite token de acceso para esa cuenta

### Requirement: Protección contra intentos de acceso repetidos

El sistema SHALL bloquear temporalmente el inicio de sesión local de una cuenta tras superar el número configurado de intentos fallidos consecutivos.

#### Scenario: Bloqueo por intentos fallidos consecutivos

- **GIVEN** un usuario administrativo que alcanzó el umbral configurado de intentos fallidos
- **WHEN** intenta iniciar sesión, incluso con la contraseña correcta, antes de que expire el bloqueo
- **THEN** responde `423 Locked` con un problema RFC 7807 que indica el bloqueo temporal
- **AND** no se emite token de acceso

#### Scenario: Restablecimiento del contador tras un acceso exitoso

- **GIVEN** un usuario con intentos fallidos previos que aún no alcanzó el umbral
- **WHEN** inicia sesión correctamente
- **THEN** el contador de intentos fallidos vuelve a cero
- **AND** el siguiente inicio de sesión con credenciales válidas también es exitoso

### Requirement: Autorización por rol de los endpoints administrativos

El sistema SHALL restringir los recursos administrativos a los roles expresamente autorizados y rechazar a cualquier otro portador autenticado.

#### Scenario: Acceso con rol autorizado

- **GIVEN** una sesión con token local que contiene el rol `Administrativo`
- **WHEN** invoca un endpoint administrativo protegido
- **THEN** responde `200 OK` con el recurso solicitado

#### Scenario: Acceso con rol insuficiente

- **GIVEN** una sesión con token local válido cuyo único rol es `Estudiante`
- **WHEN** invoca un endpoint administrativo protegido
- **THEN** responde `403 Forbidden` con un problema RFC 7807
- **AND** no se expone el contenido del recurso

#### Scenario: Acceso sin credenciales

- **GIVEN** una solicitud sin encabezado `Authorization`
- **WHEN** invoca un endpoint administrativo protegido
- **THEN** responde `401 Unauthorized` con un problema RFC 7807
- **AND** la respuesta incluye el encabezado `WWW-Authenticate` correspondiente al esquema local

### Requirement: Vigencia y verificación del token local

El sistema SHALL aceptar únicamente tokens propios con firma válida, emisor y audiencia esperados y fecha de expiración vigente.

#### Scenario: Token expirado

- **GIVEN** un token local emitido cuya fecha de expiración ya transcurrió
- **WHEN** se usa para invocar un endpoint protegido
- **THEN** responde `401 Unauthorized` con un problema RFC 7807 que indica sesión expirada
- **AND** el acceso se deniega

#### Scenario: Token alterado o de otro emisor

- **GIVEN** un token con la firma modificada o emitido por un emisor distinto al configurado
- **WHEN** se usa para invocar un endpoint protegido
- **THEN** responde `401 Unauthorized` con un problema RFC 7807
- **AND** el sistema no concede ningún rol del contenido del token

### Requirement: Consulta del perfil del portador autenticado

El sistema SHALL exponer el perfil del usuario autenticado resuelto desde el token vigente, identificando su tipo de actor y sus roles.

#### Scenario: Perfil administrativo consulta su perfil

- **GIVEN** una sesión activa con token local válido
- **WHEN** invoca `GET /api/auth/me`
- **THEN** responde `200 OK` con el nombre, correo institucional, tipo de actor y roles del usuario
- **AND** la respuesta no incluye datos de seguridad del almacén de identidad

### Requirement: Persistencia de la sesión en el cliente web

La SPA SHALL adjuntar el token local en las solicitudes a endpoints protegidos y descartarlo al cerrar sesión.

#### Scenario: Solicitud autenticada desde la SPA

- **GIVEN** un usuario administrativo autenticado en la aplicación Angular
- **WHEN** la SPA invoca un endpoint protegido
- **THEN** la solicitud incluye el encabezado `Authorization` con el token vigente
- **AND** la sesión no se pierde al recargar la página mientras el token siga vigente

#### Scenario: Cierre de sesión en la SPA

- **GIVEN** una sesión activa en la aplicación Angular
- **WHEN** el usuario cierra sesión
- **THEN** el cliente descarta el token almacenado y redirige a la pantalla de acceso
- **AND** una nueva invocación a un endpoint protegido responde `401 Unauthorized`

#### Scenario: Navegación a una ruta protegida sin sesión

- **GIVEN** un usuario sin token vigente
- **WHEN** intenta abrir una ruta protegida del cliente
- **THEN** la aplicación lo redirige a la pantalla de acceso
- **AND** no se renderiza el contenido protegido
