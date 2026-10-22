# Autenticación híbrida (JWT local + Entra ID)

Documento operativo de los esquemas de autenticación, los claims normalizados y las variables
de configuración. Complementa `docs/persistencia.md`.

## 1. Esquemas registrados

| Esquema | Uso | Validación |
| --- | --- | --- |
| `Hibrida` (`PolicyScheme`) | Esquema por defecto; enruta según el emisor | No valida nada por sí mismo: delega en el handler elegido |
| `LocalJwt` | Personal administrativo y de soporte con credenciales locales | HS256 con `Jwt:Key`, `iss`, `aud` y vigencia; `ClockSkew` 30 s |
| `EntraId` | Docentes y estudiantes con identidad institucional | OIDC/JWKS del inquilino, `iss`, `aud`, `tid` y vigencia; un único inquilino |

`ForwardDefaultSelector` lee el claim `iss` **sin validar la firma** solo para elegir el handler.
La firma la valida siempre el handler seleccionado; un `iss` falsificado cae en el esquema
equivocado y se rechaza.

## 2. Variables de configuración

| Clave | Obligatoria | Descripción |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | Sí | Cadena de SQL Server (ver `docs/persistencia.md`) |
| `Jwt:Issuer` | Sí | Emisor propio de los tokens locales |
| `Jwt:Audience` | Sí | Audiencia esperada (la SPA) |
| `Jwt:ExpiraMinutos` | No (60) | Vigencia absoluta del access token |
| `Jwt:Key` | Sí | Clave HMAC de al menos 32 caracteres; **nunca** versionada |
| `EntraId:TenantId` | Para federación | Inquilino institucional; si falta, solo se acepta `LocalJwt` |
| `EntraId:ClientId` | Para federación | Registro de aplicación que expone la API |
| `EntraId:Audience` | No | Audiencia del token; si está vacía se usa `ClientId` |
| `EntraId:AuthorityOverride` | No | Autoridad explícita para metadatos simulados en pruebas |
| `Identity:Lockout:MaxIntentosFallidos` | No (5) | Umbral de bloqueo temporal |
| `Identity:Lockout:MinutosBloqueo` | No (15) | Duración del bloqueo |
| `Seed:AdminEmail` / `Seed:AdminPassword` / `Seed:AdminNombre` | No | Administrador inicial; sin ellos no se crea la cuenta |

`appsettings.json` solo contiene valores no secretos. La clave de firma se inyecta así:

```bash
dotnet user-secrets set "Jwt:Key" "<clave-aleatoria-de-al-menos-32-caracteres>" --project src/Backend.Presentation.Api
```

En contenedor se usan variables de entorno (`Jwt__Key`, `EntraId__TenantId`, `EntraId__ClientId`).

## 3. Claims normalizados

| Claim | Origen local | Origen federado |
| --- | --- | --- |
| `sub` | `Usuario.Id` | Se reemplaza por el `Usuario.Id` interno tras resolver el perfil |
| `jti` | Guid por emisión | — |
| `email` | Correo institucional | `email` (o `preferred_username`) |
| `name` | `NombreCompleto` | `name` (o `preferred_username`) |
| `roles` | Roles de Identity | Intersección entre roles locales y app roles del token |
| `oid` / `tid` | — | Identificador inmutable y inquilino emitidos por Entra ID |

`NameClaimType = "name"` y `RoleClaimType = "roles"` en ambos esquemas, de modo que
`[Authorize(Roles = "Administrativo")]` funciona igual para las dos identidades.

## 4. Resolución del perfil federado

`ResolucionFederadaMiddleware` actúa después de `UseAuthentication` y solo para identidades
autenticadas por `EntraId`:

1. Normaliza los app roles a `Docente`/`Estudiante`; cualquier otro rol se descarta
   (jamás `Administrativo`).
2. Si no queda ningún rol académico → `403 permisos-insuficientes` sin crear perfiles.
3. Busca el perfil local por `IdentificadorFederado` (`oid`); si no existe lo aprovisiona sin
   contraseña local con el rol académico detectado.
4. Si el perfil está inactivo o la intersección de roles queda vacía → `403 permisos-insuficientes`.
5. Actualiza `UltimoAccesoEn`, reemplaza el principal con `sub`/`email`/`name`/`roles` internos y
   registra el acceso federado (usuario, rol aplicado, `oid`, marca de tiempo; nunca el token).

## 5. Endpoints

| Endpoint | Autorización | Respuesta |
| --- | --- | --- |
| `POST /api/auth/login` | Anónimo | `200` con `token`, `tipoToken`, `expiraEn` y `roles`; `401` credenciales inválidas; `423` cuenta bloqueada |
| `GET /api/auth/me` | Cualquiera de los dos esquemas | Perfil: nombre, correo, tipo de actor y roles |
| `GET /api/administracion/verificacion` | `Administrativo` | `200`, `403` o `401` |

## 6. Catálogo de errores RFC 7807

| `type` | HTTP | Condición |
| --- | --- | --- |
| `credenciales-invalidas` | 401 | Correo inexistente, contraseña errónea o cuenta inactiva |
| `token-ausente` | 401 | Endpoint protegido sin `Authorization` (incluye `WWW-Authenticate`) |
| `token-invalido` | 401 | Firma, emisor o audiencia incorrectos |
| `sesion-expirada` | 401 | `exp` vencido |
| `permisos-insuficientes` | 403 | Rol insuficiente o perfil federado no habilitado |
| `cuenta-bloqueada` | 423 | Bloqueo por intentos fallidos |
| `solicitud-invalida` | 400 | DTO inválido, con extensión `errors` por campo |
| `proveedor-identidad-no-disponible` | 503 | No se alcanzan metadatos/JWKS de Entra ID |
| `error-interno` | 500 | Excepción no controlada, sin trazas |

Todo problema incluye `traceId`. El `401` de login **no** lleva `WWW-Authenticate`; el de un
endpoint protegido sí.

## 7. Arranque local

```bash
dotnet user-secrets set "Jwt:Key" "<clave-de-32+-caracteres>" --project src/Backend.Presentation.Api
dotnet run --project src/Backend.Presentation.Api

# Login del administrador sembrado
curl -X POST https://localhost:7xxx/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"correoInstitucional":"admin@universidad.edu","contrasena":"<contraseña>"}'
```

La validación federada se activa sin cambiar código: basta configurar `EntraId:TenantId` y
`EntraId:ClientId` del inquilino institucional.
