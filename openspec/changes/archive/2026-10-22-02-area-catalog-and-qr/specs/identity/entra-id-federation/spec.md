# Spec Delta

## MODIFIED Requirements

### Requirement: Vinculación del token federado con el perfil institucional local

El sistema SHALL resolver al usuario interno exclusivamente por el identificador federado (`oid`) previamente vinculado a una cuenta y SHALL aplicar el rol asignado en la plataforma, sin derivarlo de las notificaciones del token.

#### Scenario: Docente con perfil local existente

- **GIVEN** una cuenta interna con rol `Docente` vinculada al `oid` del token
- **WHEN** invoca un endpoint protegido para docentes
- **THEN** el sistema identifica la cuenta interna correspondiente
- **AND** autoriza la operación con el rol `Docente` asignado en la plataforma

#### Scenario: Primer acceso de un miembro de la comunidad institucional

- **GIVEN** un token válido del inquilino cuyo `oid` no está vinculado a ninguna cuenta
- **WHEN** invoca un endpoint protegido
- **THEN** responde `403 Forbidden` con un problema RFC 7807 que indica que la cuenta debe registrarse y vincularse desde la configuración del usuario
- **AND** el sistema no crea ni aprovisiona un perfil automáticamente

#### Scenario: Token federado de una identidad sin habilitación académica

- **GIVEN** un token válido del inquilino cuyas notificaciones de rol no corresponden a ninguna cuenta de la plataforma
- **WHEN** invoca un endpoint de la aplicación
- **THEN** responde `403 Forbidden` con un problema RFC 7807 que indica ausencia de habilitación
- **AND** el sistema no crea cuentas ni concede roles con base en las notificaciones del token

#### Scenario: Rol modificado por el personal administrativo

- **GIVEN** una cuenta vinculada cuyo rol fue cambiado de `Estudiante` a `Docente` en la plataforma
- **WHEN** presenta un nuevo token institucional
- **THEN** el sistema resuelve el perfil con el rol `Docente`
- **AND** las autorizaciones se evalúan con ese rol vigente

