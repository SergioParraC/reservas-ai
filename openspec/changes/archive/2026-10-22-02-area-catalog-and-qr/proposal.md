# Proposal

## Why

La Etapa 1 dejó una plataforma autenticada pero sin infraestructura física que administrar: no existe ninguna entidad de espacio, puesto ni identificador QR, por lo que ni el administrador puede registrar el campus ni el estudiante puede consultar dónde reservar. Sin esta capa, el préstamo directo de puestos de cómputo (Módulo 3 parcial) no tiene sujeto de reserva y la validación presencial por QR de la Etapa 3 no tendría placas físicas que escanear.

## What Changes

- Incorporar al dominio las entidades `Espacio` (agregado raíz) y `Puesto` (sub-puesto de cómputo), el objeto de valor inmutable `CodigoQR` y los enums `CategoriaEspacio`, `TipoAprobacion` y `EstadoUnidad`, con las invariantes de aforo, unicidad de identificador de puesto e inmutabilidad del código QR.
- Mantener `IApplicationDbContext` con `SaveChangesAsync` y añadir puertos de aplicación sin tipos de EF Core: `IEspaciosRepository`, `IConsultaEspacios`, `IQrGenerator` e `IAlmacenArchivos`.
- Implementar en `Backend.Infrastructure` la configuración Fluent API de `Espacio` y `Puesto`, la conversión del value object `CodigoQR`, los índices únicos, el token de concurrencia optimista (`RowVersion`) y la migración `AreasYPuestos`.
- Implementar `QRCoderService` con el paquete `QRCoder` 1.8.1: codificación de la matriz de módulos y composición de SVG vectoriales (código individual y hoja de etiquetas A4), sin dependencias gráficas nativas.
- Implementar `LocalFileStorageService` para las fotografías de áreas: se guarda en base de datos solo la ruta relativa y el archivo en el volumen administrado.
- Exponer los endpoints REST administrativos de CRUD de áreas y puestos, carga de fotografía y descarga de QR, y los endpoints públicos del catálogo con filtros y paginación.
- Crear la funcionalidad `features/areas` en Angular 20 con Standalone Components y Signals: catálogo público con filtros, detalle de área, mantenimiento administrativo de áreas y puestos, y previsualización/descarga de la plantilla de impresión QR.
- Añadir la configuración de aforo máximo, etiquetas por hoja y ruta base del almacén, y las pruebas unitarias, de integración y de componente de la etapa.
- Exponer `POST /api/usuarios/registro` con formulario público de creación de cuenta: la primera cuenta registrada recibe el rol `Administrativo` y todas las siguientes nacen con rol `Estudiante`; el rol queda asignado en la plataforma y no depende de Entra ID.
- Exponer `POST|DELETE /api/usuarios/me/vinculacion-entra` y la vista de configuración de la cuenta para asociar el perfil local con la identidad institucional (`oid`) validando un token de Entra ID, de modo que la misma cuenta pueda entrar con contraseña local o con el correo de la universidad.
- Exponer `GET /api/usuarios` y `PUT /api/usuarios/{id}/rol` para que el personal administrativo liste las cuentas registradas y cambie su rol; el rol local pasa a ser la única fuente de verdad de la autorización, incluso para sesiones federadas.
- Extender la SPA con la vista de registro, el botón de acceso con el correo de la universidad, la pantalla de vinculación de cuenta y la consola administrativa de usuarios.

Fuera de alcance: motor de disponibilidad y reserva efectiva (Etapa 2), carga de PDF, aprobaciones, escáner QR y *check-in* (Etapa 3), notificaciones, sanciones y analítica. La edición de políticas de antelación y franjas de mantenimiento también queda para la Etapa 2; aquí solo se persiste la clasificación de aprobación de cada área. Tampoco se incluyen recuperación de contraseña, MFA, eliminación de cuentas ni invitaciones por correo: el alta es un autorregistro validado por dominio institucional.

## Capabilities

### New Capabilities

- `espacios/gestion-espacios`: alta, mantenimiento y baja lógica de áreas y puestos con validación de aforo, asignación de un código QR inmutable por unidad física, emisión de plantillas vectoriales de impresión y consulta pública del catálogo con filtros.
- `identity/user-accounts`: autorregistro de cuentas con rol inicial derivado del estado del sistema y vinculación/desvinculación de la identidad institucional de Entra ID para habilitar el doble método de acceso.
- `identity/role-administration`: consulta administrativa de las cuentas registradas y cambio de rol desde la plataforma como fuente única de autorización.

### Modified Capabilities

- `identity/entra-id-federation`: el rol efectivo de una sesión federada deja de calcularse por intersección con los app roles del token y pasa a ser el rol asignado en la plataforma; además se elimina el aprovisionamiento automático de perfiles en el primer acceso federado, que ahora exige una cuenta vinculada.

## Impact

- **Código afectado (nuevo):** `src/Backend.Domain/Entities`, `ValueObjects`, `Enums` y `Exceptions`; `src/Backend.Application/Modules/Areas` (comandos, consultas, DTOs y validadores), `Modules/Usuarios`, `Application/Interfaces` e `IApplicationDbContext`; `src/Backend.Infrastructure/Persistence/Configurations`, `Migrations`, `Services/QRCoderService`, `Services/LocalFileStorageService` e `Identity/EntraIdProfileResolver`; `src/Backend.Presentation.Api/Controllers/EspaciosController`, `PuestosController`, `CatalogoController` y `UsuariosController`, más sus DTOs en `Contracts`; `src/frontend/src/app/features/areas`, `features/auth`, `features/perfil`, `features/administracion/usuarios`, `core/auth` y `app.routes.ts`.
- **APIs expuestas:** `GET|POST /api/espacios`, `GET|PUT|DELETE /api/espacios/{id}`, `POST|PUT|DELETE /api/espacios/{id}/puestos[/{puestoId}]`, `POST|DELETE /api/espacios/{id}/imagen`, `GET /api/espacios/{id}/qr.svg`, `GET /api/puestos/{id}/qr.svg`, `GET /api/espacios/qr/plantilla`, `GET /api/catalogo/espacios[/{id}]`, `POST /api/usuarios/registro`, `POST|DELETE /api/usuarios/me/vinculacion-entra`, `GET /api/usuarios` y `PUT /api/usuarios/{id}/rol`. Los errores conservan el contrato RFC 7807 de la Etapa 1.
- **Dependencias nuevas:** `QRCoder` 1.8.1 en `Backend.Infrastructure` y `@azure/msal-browser` 5.26.0 en la SPA (flujo OIDC con PKCE). No se añaden paquetes al dominio ni a la aplicación.
- **Configuración y almacenamiento:** claves `Espacios:AforoMaximo`, `Espacios:EtiquetasPorHoja`, `Almacen:RutaBase`, `Identidad:PermitirAutorregistro` y `Identidad:DominioCorreoInstitucional`; se requiere un volumen persistente para las fotografías y un límite de tamaño y tipos MIME permitidos.
- **Persistencia:** migración con las tablas `Espacios` y `Puestos`, índices únicos sobre `CodigoQR` e `(EspacioId, Identificador)`, y `RowVersion` para concurrencia optimista. Las cuentas y los roles reutilizan el esquema de ASP.NET Core Identity; el vínculo federado usa la columna `IdentificadorFederado` ya existente con su índice único. La migración es aditiva.
- **Seguridad:** el `CodigoQR` es un identificador de control físico y no se expone en respuestas públicas ni es editable por API. Las mutaciones de áreas y la gestión de usuarios exigen rol `Administrativo`; el catálogo es anónimo y el autorregistro queda acotado por dominio institucional y limitación de tasa. El cambio altera el modelo de autorización heredado: los app roles de Entra ID dejan de decidir el rol de la plataforma.
- **Riesgo principal:** el autorregistro público permite crear cuentas `Estudiante` sin intervención administrativa; se mitiga con dominio de correo obligatorio, limitación de tasa, estado activo por omisión y la consola de usuarios para corregir el rol asignado.
