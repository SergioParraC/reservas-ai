# api-contracts/problem-details Specification

## Purpose
Establece un contrato único y legible por máquina para los fallos de autenticación y autorización de la API, de forma que el cliente web y cualquier consumidor puedan interpretar el error sin adivinar formatos ni recibir detalles internos.

## Requirements

### Requirement: Formato RFC 7807 para errores de autenticación y autorización

El sistema SHALL responder todo fallo de autenticación o autorización con el tipo de contenido `application/problem+json` y los miembros `type`, `title`, `status`, `detail` e `instance`.

#### Scenario: Error de credenciales inválidas

- **GIVEN** una solicitud de inicio de sesión con credenciales incorrectas
- **WHEN** el sistema rechaza la solicitud
- **THEN** responde con el tipo de contenido `application/problem+json`
- **AND** el cuerpo incluye `status` con el código HTTP, `title` legible, `detail` explicativo e `instance` con la ruta solicitada
- **AND** el `type` identifica la clase de problema mediante un identificador estable

#### Scenario: Error de autorización por rol insuficiente

- **GIVEN** una identidad autenticada sin el rol requerido
- **WHEN** el sistema deniega el acceso al recurso
- **THEN** responde `403 Forbidden` con el mismo contrato `application/problem+json`
- **AND** el `detail` describe la falta de permisos sin revelar la estructura de roles internos

#### Scenario: Error por ausencia de token

- **GIVEN** una solicitud a un recurso protegido sin encabezado `Authorization`
- **WHEN** el sistema deniega el acceso
- **THEN** responde `401 Unauthorized` con el contrato `application/problem+json`
- **AND** incluye el encabezado `WWW-Authenticate` con el esquema de autenticación aplicable

### Requirement: Correspondencia entre condición de fallo y código de estado

El sistema SHALL asignar de forma uniforme el código de estado HTTP a cada condición de autenticación, con independencia del esquema que originó el rechazo.

#### Scenario: Códigos de estado por condición

- **GIVEN** las condiciones de fallo de autenticación soportadas
- **WHEN** cada una se provoca sobre la API
- **THEN** credenciales inválidas responden `401 Unauthorized`
- **AND** identidad autenticada sin rol suficiente responde `403 Forbidden`
- **AND** cuenta bloqueada temporalmente responde `423 Locked`
- **AND** proveedor de identidad federado inaccesible responde `503 Service Unavailable`

#### Scenario: Misma condición con distinto origen

- **GIVEN** un token expirado emitido localmente y un token expirado emitido por el inquilino institucional
- **WHEN** cada uno se presenta en un endpoint protegido
- **THEN** ambas respuestas usan `401 Unauthorized` y el mismo contrato de error
- **AND** el `title` coincide entre ambas respuestas

### Requirement: Errores de validación de las solicitudes de autenticación

El sistema SHALL responder los cuerpos de solicitud inválidos en los endpoints de autenticación con `400 Bad Request` en formato RFC 7807 y el detalle por campo.

#### Scenario: Cuerpo de inicio de sesión incompleto

- **GIVEN** una solicitud de inicio de sesión sin el campo de contraseña
- **WHEN** se envía al endpoint de acceso local
- **THEN** responde `400 Bad Request` con `application/problem+json`
- **AND** el cuerpo incluye una extensión con los errores agrupados por nombre de campo
- **AND** no se ejecuta ninguna verificación de credenciales

#### Scenario: Formato de correo inválido

- **GIVEN** una solicitud de inicio de sesión cuyo correo no tiene formato válido
- **WHEN** se envía al endpoint de acceso local
- **THEN** responde `400 Bad Request` con el contrato de error uniforme
- **AND** el detalle identifica el campo afectado sin repetir la contraseña recibida

### Requirement: Ausencia de filtración de información interna en los errores

El sistema SHALL omitir trazas de pila, rutas de archivo, nombres de consultas y cualquier dato que permita enumerar usuarios o inferir la implementación.

#### Scenario: Fallo inesperado durante la autenticación

- **GIVEN** una excepción no controlada producida en el procesamiento de la autenticación
- **WHEN** el sistema compone la respuesta
- **THEN** responde `500 Internal Server Error` con un problema RFC 7807 de mensaje genérico
- **AND** el cuerpo no incluye trazas de pila, nombres de clases internas ni cadenas de conexión

#### Scenario: Consulta de un correo no registrado

- **GIVEN** un intento de inicio de sesión con un correo inexistente
- **WHEN** el sistema responde el fallo
- **THEN** el cuerpo no afirma ni niega si el correo existe en el sistema
- **AND** los campos del problema coinciden en estructura con los de una contraseña incorrecta

### Requirement: Correlación de errores para diagnóstico

El sistema SHALL incluir en cada respuesta de problema un identificador de correlación que permita localizar el evento en los registros del servidor.

#### Scenario: Identificador de correlación en la respuesta de error

- **GIVEN** una solicitud rechazada por autenticación
- **WHEN** el sistema genera la respuesta RFC 7807
- **THEN** el cuerpo incluye una extensión con el identificador de correlación de la solicitud
- **AND** el registro del servidor contiene el mismo identificador para ese evento

### Requirement: Interpretación uniforme del contrato de error en el cliente web

La SPA SHALL interpretar el contrato RFC 7807 en un único punto y presentar un mensaje comprensible, diferenciando la sesión expirada del resto de fallos.

#### Scenario: Sesión expirada detectada por el cliente

- **GIVEN** una SPA con un token vencido
- **WHEN** una solicitud recibe `401 Unauthorized` con el contrato de problema
- **THEN** el cliente descarta la sesión y redirige a la pantalla de acceso con un aviso de sesión expirada
- **AND** no muestra un mensaje de error genérico sin contexto

#### Scenario: Error de permisos presentado al usuario

- **GIVEN** una SPA con sesión vigente cuyo rol no autoriza la operación
- **WHEN** una solicitud recibe `403 Forbidden` con el contrato de problema
- **THEN** el cliente muestra el `detail` del problema de forma comprensible
- **AND** conserva la sesión activa sin redirigir al acceso

#### Scenario: Respuesta de error ajena al contrato

- **GIVEN** una respuesta de error que no cumple el formato RFC 7807, por ejemplo una página HTML de un proxy
- **WHEN** el cliente la recibe
- **THEN** se muestra un mensaje de error genérico controlado
- **AND** la aplicación no falla ni queda en un estado inconsistente
