# Spec Delta

## Purpose

Permite que docentes y estudiantes accedan con su identidad institucional de Microsoft Entra ID mediante tokens Bearer, resolviendo su perfil y roles dentro del sistema sin gestionar contraseñas locales para esa comunidad.

## ADDED Requirements

### Requirement: Validación de tokens Bearer emitidos por Entra ID

El sistema SHALL aceptar como identidad federada únicamente tokens Bearer cuya firma se verifique contra las claves publicadas por el inquilino institucional, con emisor, audiencia y vigencia esperados.

#### Scenario: Token institucional válido

- **GIVEN** un docente autenticado en el inquilino institucional de Entra ID
- **WHEN** invoca un endpoint protegido presentando su token Bearer
- **THEN** el sistema valida la firma con las claves publicadas por el inquilino, el emisor esperado, la audiencia configurada y la fecha de expiración
- **AND** responde `200 OK` con el recurso solicitado

#### Scenario: Token institucional expirado

- **GIVEN** un token de Entra ID cuya vigencia finalizó
- **WHEN** se presenta en un endpoint protegido
- **THEN** responde `401 Unauthorized` con un problema RFC 7807
- **AND** no se resuelve ningún perfil institucional a partir de ese token

#### Scenario: Token de un inquilino o audiencia ajenos

- **GIVEN** un token válido emitido por un inquilino distinto al institucional, o con una audiencia no registrada para esta aplicación
- **WHEN** se presenta en un endpoint protegido
- **THEN** responde `401 Unauthorized` con un problema RFC 7807
- **AND** el sistema no concede roles con base en sus notificaciones

### Requirement: Vinculación del token federado con el perfil institucional local

El sistema SHALL resolver el usuario interno a partir del identificador federado inmutable (`oid`) del token y SHALL asociar el perfil con su tipo de actor y rol correspondiente.

#### Scenario: Docente con perfil local existente

- **GIVEN** un usuario interno con rol `Docente` cuyo identificador federado coincide con el `oid` del token
- **WHEN** invoca un endpoint protegido para docentes
- **THEN** el sistema identifica al usuario interno correspondiente
- **AND** autoriza la operación con el rol `Docente`

#### Scenario: Primer acceso de un miembro de la comunidad institucional

- **GIVEN** un token válido del inquilino cuyo `oid` no tiene perfil interno asociado
- **WHEN** invoca un endpoint protegido
- **THEN** el sistema crea o vincula el perfil institucional con los datos de nombre, correo y rol informados por la institución
- **AND** el acceso se autoriza con un rol distinto de `Administrativo`

#### Scenario: Token federado de una identidad sin habilitación académica

- **GIVEN** un token válido del inquilino cuyo rol institucional no corresponde a docente ni a estudiante
- **WHEN** invoca un endpoint de la aplicación
- **THEN** responde `403 Forbidden` con un problema RFC 7807 que indica ausencia de habilitación
- **AND** no se crea ni se modifican roles administrativos para esa identidad

### Requirement: Resolución del esquema de autenticación híbrido

El sistema SHALL determinar el esquema aplicable a partir del emisor y la firma del token, sin aceptar credenciales de un esquema bajo las reglas de validación del otro.

#### Scenario: Convivencia de ambos esquemas en el mismo endpoint

- **GIVEN** un endpoint protegido que admite identidades locales y federadas
- **WHEN** dos solicitudes distintas se presentan, una con token local vigente y otra con token de Entra ID válido
- **THEN** ambas respuestas son `200 OK`
- **AND** cada identidad conserva los roles que le corresponden según su origen

#### Scenario: Token local presentado donde solo se admite federación

- **GIVEN** un endpoint que exige identidad federada de la comunidad institucional
- **WHEN** se presenta un token local emitido por el sistema
- **THEN** responde `401 Unauthorized` con un problema RFC 7807
- **AND** el sistema no valida ese token contra las claves del inquilino

#### Scenario: Token federado usado como credencial local

- **GIVEN** el endpoint de inicio de sesión local con credenciales
- **WHEN** se presenta únicamente un token Bearer de Entra ID en lugar de correo y contraseña
- **THEN** responde `400 Bad Request` o `401 Unauthorized` con un problema RFC 7807
- **AND** no se emite un token local

### Requirement: Continuidad operativa ante indisponibilidad del proveedor federado

El sistema SHALL mantener operativo el acceso local cuando no pueda obtener los metadatos o claves de Entra ID, y SHALL rechazar con un error explícito las solicitudes que dependan de la validación federada.

#### Scenario: Login local durante una caída del proveedor federado

- **GIVEN** que el sistema no logra obtener las claves públicas del inquilino
- **WHEN** un administrador inicia sesión con credenciales locales válidas
- **THEN** responde `200 OK` con un token local vigente
- **AND** la respuesta no queda bloqueada por el fallo del proveedor externo

#### Scenario: Solicitud federada durante una caída del proveedor

- **GIVEN** que el sistema no puede validar tokens porque no alcanza las claves del inquilino
- **WHEN** un docente presenta su token Bearer en un endpoint protegido
- **THEN** responde `503 Service Unavailable` con un problema RFC 7807 que indica indisponibilidad temporal del proveedor de identidad
- **AND** la respuesta no se reporta como credencial inválida del usuario

### Requirement: Registro de auditoría de la autenticación federada

El sistema SHALL registrar cada intento de autenticación federada con su resultado, identificando al portador cuando la validación sea exitosa.

#### Scenario: Acceso federado exitoso registrado

- **GIVEN** un token institucional válido que resuelve un perfil interno
- **WHEN** la solicitud se completa
- **THEN** se registra el identificador del usuario interno, el rol aplicado y la marca de tiempo
- **AND** el registro no incluye el token completo ni datos sensibles del portador

#### Scenario: Intento federado rechazado registrado

- **GIVEN** un token con firma inválida o vigencia expirada
- **WHEN** el sistema rechaza la solicitud
- **THEN** se registra el motivo del rechazo y la marca de tiempo
- **AND** el motivo registrado es útil para diagnóstico sin exponer el contenido del token
