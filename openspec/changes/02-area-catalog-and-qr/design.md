# Design

## Context

Ver `proposal.md` — Why. La Etapa 1 dejó operativos: la solución en cuatro capas con la regla de dependencias verificada por `tests/Backend.ArchitectureTests`, el `ApplicationDbContext` de EF Core 10 sobre SQL Server 2022 con `Usuario`/`Rol`, el pipeline de autenticación híbrida, el contrato RFC 7807 y la SPA en Angular 20 con `AuthService`, interceptores y guards.

Restricciones que condicionan este diseño:

- La prueba de arquitectura prohíbe en `Backend.Application` cualquier dependencia de EF Core o ASP.NET Core, y prohíbe que las firmas públicas de `Backend.Presentation.Api` expongan tipos de `Backend.Infrastructure`. Por tanto el acceso a datos y la generación de QR deben entrar por puertos de aplicación.
- `IApplicationDbContext` solo expone hoy `SaveChangesAsync`; no puede crecer con `DbSet<T>` sin romper el aislamiento de la aplicación respecto del ORM.
- Las fotografías no se guardan como BLOB (plan, sección de persistencia): base de datos con metadatos y ruta relativa, archivo en volumen administrado.
- El catálogo público se consulta sin sesión y debe ser barato: proyecciones explícitas con `AsNoTracking()`.
- `@zxing/ngx-scanner` pertenece a la Etapa 3; aquí no se lee ningún QR, solo se generan e imprimen.

## Goals / Non-Goals

**Goals:**

- Un agregado `Espacio` que concentre las invariantes de aforo, estados y puestos, ejecutable en pruebas unitarias sin base de datos ni framework.
- Un identificador QR opaco, único e inmutable, generado una sola vez y persistido con índice único.
- Emisión vectorial (SVG) del código individual y de la hoja de etiquetas A4, sin dependencias gráficas nativas ni rasterizado.
- Un catálogo público paginado con filtros combinables, aislado del modelo de escritura.
- Una funcionalidad Angular autocontenida en `features/areas` que reutilice los interceptores y guards existentes.

**Non-Goals:**

- Disponibilidad horaria, reservas, préstamo directo, aprobaciones y `check-in`: el catálogo solo describe la oferta física.
- Parametrización de ventanas de antelación, duración máxima y franjas de mantenimiento (Etapa 2); aquí solo se persiste `TipoAprobacion`.
- Edición o sustitución de un código QR ya emitido, incluso ante sospecha de compromiso.
- Miniaturas, recorte o procesamiento de imágenes: se almacena el archivo tal como llega, con validación de tipo y tamaño.
- Búsqueda geoespacial, ordenamiento configurable o facetas dinámicas del catálogo.

## Decisions

### 1. Modelo de dominio: agregado `Espacio` con `Puesto` como entidad interna

```
Espacio (raiz de agregado)
+------------------------------------------------------+
| Id  Nombre  Ubicacion  Categoria                     |
| Aforo  TipoAprobacion  Estado                        |
| CodigoQR (VO inmutable, asignado al crear)           |
| FotografiaRuta?  CreadoEn  ActualizadoEn  RowVersion |
+--------------------------+---------------------------+
                           | 0..N puestos (areas de
                           |      computo o mixtas)
                           v
+------------------------------------------------------+
| Puesto                                               |
| Id  EspacioId  Identificador  DescripcionEquipo      |
| CodigoQR (VO inmutable, asignado al crear)           |
| Estado  CreadoEn                                     |
+------------------------------------------------------+
```

Invariantes y punto donde se hacen cumplir:

| Invariante | Dominio | Base de datos |
| :--- | :--- | :--- |
| `Aforo` entre 1 y máximo institucional | `Espacio.Crear` / `CambiarAforo` | validación de aplicación |
| `Aforo` >= puestos activos | `Espacio.CambiarAforo` | — |
| Cantidad de puestos activos <= `Aforo` | `Espacio.AgregarPuesto`, `ReactivarPuesto` | — |
| `(Nombre, Ubicacion)` único | consulta previa en el handler | índice único `IX_Espacios_Nombre_Ubicacion` |
| `Identificador` de puesto único por área | `Espacio.AgregarPuesto` | índice único `IX_Puestos_EspacioId_Identificador` |
| `CodigoQR` único por unidad | `CodigoQR` en la fábrica | índices únicos `IX_Espacios_CodigoQR` y `IX_Puestos_CodigoQR` |
| `CodigoQR` no cambia | sin setter ni método de sustitución | columna no reasignable desde comandos |

`Puesto` no es un agregado independiente: no se carga ni se modifica fuera de su `Espacio` y su aforo depende del área. Se expone como `IReadOnlyCollection<Puesto>` y se muta solo con métodos del agregado (`AgregarPuesto`, `ActualizarPuesto`, `DesactivarPuesto`, `ReactivarPuesto`), lo que impide que un caso de uso salte la validación de aforo.

Alternativa descartada: `Puesto` como agregado raíz con su propio repositorio. Duplicaría la regla de aforo en dos puntos de escritura y permitiría modificarlo sin conocer el estado del área.

El máximo de aforo no es una constante del dominio: `Espacio.Crear` y `CambiarAforo` reciben el límite como parámetro y la capa de aplicación lo obtiene de `Espacios:AforoMaximo`. El dominio permanece puro y la política institucional se cambia sin recompilar el núcleo.

Alternativa descartada: sembrar `AforoMaximo` como constante en `Backend.Domain`.

### 2. `CodigoQR` como objeto de valor opaco, generado una sola vez

```csharp
public sealed record CodigoQR
{
    public const string Prefijo = "QR-";
    public string Valor { get; }
    public static CodigoQR Generar();
    public static CodigoQR Desde(string valor);
}
```

- **Formato:** `QR-` + 26 caracteres del alfabeto Crockford Base32 (0-9 y A-Z sin `I`, `L`, `O`, `U`), derivados de 16 bytes de `RandomNumberGenerator` (128 bits). Sin guiones internos ni minúsculas: se imprime y se transcribe sin ambigüedad.
- **Payload codificado en el QR:** el propio `Valor`, no una URL. Una URL acoplaría la placa física al dominio de despliegue y obligaría a reimprimir si cambia el host. En la Etapa 3 el escáner enviará el valor a la API, que lo resolverá contra el índice único.
- **Inmutabilidad:** constructor privado, sin método `Cambiar`/`Regenerar` y sin setter en `Espacio`/`Puesto`; los comandos de actualización no reciben ni mapean `CodigoQR`. Se persiste con conversión de una sola vía hacia la columna.
- **Unicidad:** 128 bits aleatorios hacen la colisión despreciable, y el índice único la convierte en `409` si el proveedor la detecta.

Alternativa descartada: derivar el código del `Id` del área (Base32 del GUID). Es predecible y permitiría enumerar unidades a partir de un código conocido.

Alternativa descartada: `Guid.NewGuid().ToString()` como columna `string`. Deja el formato abierto a cambios accidentales entre proyectos y no valida la rehidratación.

### 3. Persistencia EF Core 10 con repositorios tras puertos de aplicación

`IApplicationDbContext` se mantiene con `SaveChangesAsync` y se añaden puertos tipados con el dominio:

| Puerto | Responsabilidad | Implementación |
| :--- | :--- | :--- |
| `IEspaciosRepository` | carga del agregado con sus puestos, alta, baja y verificación de unicidad | `EspaciosRepository` sobre `ApplicationDbContext` |
| `IConsultaEspacios` | proyecciones de lectura paginadas del listado administrativo y del catálogo | `ConsultaEspacios` con `AsNoTracking()` y `Select` a DTOs |
| `IQrGenerator` | SVG de un código y de una hoja de etiquetas | `QRCoderService` |
| `IAlmacenArchivos` | guardar, reemplazar y eliminar fotografías | `LocalFileStorageService` |

`ApplicationDbContext` incorpora `DbSet<Espacio>` y `DbSet<Puesto>` porque vive en `Backend.Infrastructure`; la capa de aplicación nunca ve `DbSet`, solo los puertos.

Configuración Fluent API (`EspacioConfiguration`, `PuestoConfiguration`):

- Tablas `Espacios` y `Puestos`, claves `Guid` generadas en el dominio.
- Longitudes: `Nombre` 100, `Ubicacion` 150, `DescripcionEquipo` 150, `FotografiaRuta` 300, `CodigoQR` 29.
- Enums con `HasConversion<int>()`, igual que `UsuarioConfiguration`, para no depender del orden de los nombres.
- `CodigoQR` con `HasConversion(v => v.Valor, v => CodigoQR.Desde(v))` y `HasMaxLength(29)`.
- `RowVersion` con `IsRowVersion()`; un `DbUpdateConcurrencyException` se traduce a `409`.
- `Puestos` con `OnDelete(DeleteBehavior.Cascade)`: la baja es lógica, pero el esquema no deja huérfanos si se elimina un área en mantenimiento.
- Índices: `IX_Espacios_CodigoQR` y `IX_Puestos_CodigoQR` únicos; `IX_Espacios_Nombre_Ubicacion` único; `IX_Puestos_EspacioId_Identificador` único; `IX_Espacios_Estado_Categoria_Aforo` para el catálogo.
- Sin filtro global por estado: el listado administrativo necesita ver inactivos y el filtro del catálogo se aplica en la consulta pública.

Migración `AreasYPuestos` generada con `dotnet ef migrations add` sobre `ApplicationDbContext`; es aditiva y no hay datos previos que migrar.

### 4. Casos de uso, validación y traducción de errores

`Backend.Application/Modules/Areas` se organiza por operación, con el mismo estilo del módulo `Autenticacion` existente (clase handler + DTOs, sin MediatR):

```
Modules/Areas/
+-- Espacios/
|   +-- CrearEspacioHandler.cs        ActualizarEspacioHandler.cs
|   +-- DesactivarEspacioHandler.cs   ObtenerEspacioHandler.cs
|   +-- ListarEspaciosHandler.cs      SubirFotografiaHandler.cs
|   +-- Puestos/ (Crear, Actualizar, Desactivar)
+-- Catalogo/
|   +-- ConsultarCatalogoHandler.cs   ObtenerEspacioPublicoHandler.cs
+-- Plantillas/
|   +-- GenerarPlantillaQrHandler.cs  GenerarQrIndividualHandler.cs
+-- Dtos/ (contratos internos y mapeos)
```

- Cada handler devuelve `Resultado<T>` en `Backend.Application/Common`, con `Exito`, `Valor`, `TipoFallo` (`Validacion`, `Conflicto`, `NoEncontrado`) y un diccionario `Errores` campo → mensaje. No se añade FluentValidation: el proyecto no lo usa y los comandos son pequeños; la validación se escribe explícitamente, igual que en `LoginLocalHandler`.
- Un `DomainException` de regla de negocio se captura en el handler solo para clasificarlo (`Conflicto` en unicidad y aforo, `Validacion` en el resto); el middleware RFC 7807 de la Etapa 1 sigue siendo el único que compone el cuerpo del error.
- La presentación mapea `TipoFallo` a códigos HTTP: `Validacion` → `400`, `Conflicto` → `409`, `NoEncontrado` → `404`.

Alternativa descartada: MediatR con pipeline de validación. Añade indirección y paquetes que el plan no contempla para un puñado de comandos.

### 5. Contratos REST

| Método y ruta | Rol | Éxito | Notas |
| :--- | :--- | :--- | :--- |
| `GET /api/espacios` | Administrativo | `200` | filtros `q`, `categoria`, `estado`, `pagina`, `tamano` |
| `GET /api/espacios/{id}` | Administrativo | `200` | incluye puestos y código QR |
| `POST /api/espacios` | Administrativo | `201` + `Location` | asigna `CodigoQR` y estado `Activo` |
| `PUT /api/espacios/{id}` | Administrativo | `200` | no acepta `codigoQR`; `rowVersion` en el cuerpo |
| `DELETE /api/espacios/{id}` | Administrativo | `204` | baja lógica |
| `POST /api/espacios/{id}/puestos` | Administrativo | `201` | valida aforo y unicidad de identificador |
| `PUT /api/espacios/{id}/puestos/{puestoId}` | Administrativo | `200` | no acepta `codigoQR` |
| `DELETE /api/espacios/{id}/puestos/{puestoId}` | Administrativo | `204` | baja lógica del puesto |
| `POST /api/espacios/{id}/imagen` | Administrativo | `200` | `multipart/form-data`, una imagen |
| `DELETE /api/espacios/{id}/imagen` | Administrativo | `204` | elimina archivo y ruta |
| `GET /api/espacios/{id}/qr.svg` | Administrativo | `200` `image/svg+xml` | caché `public, max-age=31536000, immutable` |
| `GET /api/puestos/{puestoId}/qr.svg` | Administrativo | `200` `image/svg+xml` | mismo caché |
| `GET /api/espacios/qr/plantilla` | Administrativo | `200` `image/svg+xml` | `espacioIds`, `incluirPuestos`, `hoja` |
| `GET /api/catalogo/espacios` | Anónimo | `200` | solo activos, paginado |
| `GET /api/catalogo/espacios/{id}` | Anónimo | `200` | `404` si el área está inactiva |

- `EspaciosController` y `PuestosController` llevan `[Authorize(Roles = RolesAplicacion.Administrativo)]` a nivel de clase, como `AdministracionController`; `CatalogoController` lleva `[AllowAnonymous]`.
- Los DTOs de entrada y salida viven en `Backend.Presentation.Api/Contracts` y son tipos propios: la prueba de arquitectura rechaza cualquier firma pública que exponga tipos de infraestructura.
- Los listados devuelven `PaginaResultado<T>` (`elementos`, `total`, `pagina`, `tamano`).
- El código QR se incluye en las respuestas administrativas porque el operario debe contrastarlo con la placa física; nunca en las públicas.
- El catálogo no expone disponibilidad horaria: describe la oferta, no la ocupación.

### 6. Servicio QR: codificador de `QRCoder` y render SVG propio

`QRCoder` 1.8.1 se referencia **solo** en `Backend.Infrastructure`; la aplicación depende del puerto `IQrGenerator`.

```
IQrGenerator
+-- string GenerarSvg(string contenido, OpcionesQr opciones)
+-- string GenerarPlantillaSvg(PlantillaQr plantilla)
```

`QRCoderService` usa `QRCodeGenerator.CreateQrCode(contenido, ECCLevel.Q)` para obtener la matriz de módulos y compone el SVG dibujando un `<path>` con un rectángulo por módulo oscuro. Decisiones asociadas:

- **Por qué no `SvgQRCode.GetGraphic`:** devuelve un documento SVG completo con su propio `<svg>` raíz; componer una hoja con varias etiquetas obligaría a manipular cadenas y a inyectar atributos de posición. Con la matriz de módulos el mismo código sirve para el QR individual y para la hoja.
- **Por qué no `QRCode`/`PngByteQRCode`:** el requisito exige plantilla vectorial y esos renderizadores producen mapa de bits (además de arrastrar `System.Drawing.Common`).
- **Corrección de errores:** `ECCLevel.Q` (25 %), suficiente para una etiqueta impresa que puede ensuciarse o iluminarse mal.
- **Etiqueta individual:** lado de 40 mm, margen de silencio de 4 módulos y el código legible en texto monoespaciado bajo el QR.
- **Hoja A4:** 210 x 297 mm, márgenes de 10 mm, cuadrícula de 3 columnas x 6 filas = 18 etiquetas de 60 x 40 mm; la capacidad se lee de `Espacios:EtiquetasPorHoja` y el layout se deriva de ella. Cada hoja es un SVG independiente con `viewBox` en milímetros.
- **Paginación:** el handler calcula `totalHojas = techo(unidades / etiquetasPorHoja)`; la respuesta incluye `X-Total-Hojas` y `Content-Disposition: attachment; filename="plantilla-qr-<fecha>-hoja-<n>.svg"`.
- **Caché:** el código nunca cambia, así que los SVG individuales se sirven con caché inmutable; la plantilla no se cachea porque depende de la selección.
- **Sin estado:** el servicio es determinista para una misma entrada, lo que permite comparar su salida con la matriz del codificador en las pruebas.

Alternativa descartada: generar la plantilla en el cliente. Obligaría a añadir una librería JS de QR y la impresión dejaría de ser reproducible desde la API.

### 7. Fotografías: archivo en volumen y ruta relativa en base de datos

- `IAlmacenArchivos.GuardarAsync(flujo, nombreOriginal, tipoContenido)` devuelve la ruta relativa `espacios/{espacioId}/{guid}.{ext}`; la base solo guarda esa cadena en `FotografiaRuta`.
- `LocalFileStorageService` escribe bajo `Almacen:RutaBase` (por omisión `/var/lib/reservas`, montado como volumen en `docker-compose`) y crea el directorio del área si no existe.
- Validación antes de escribir: tipos permitidos `image/jpeg`, `image/png`, `image/webp`; tamaño máximo 5 MB; verificación de *magic bytes* para no confiar en el `Content-Type` declarado; nombre generado por el servidor (nunca el del cliente) para evitar travesía de rutas.
- Reemplazo de fotografía: se escribe el archivo nuevo, se actualiza la ruta y se elimina el anterior en un `try` con registro de advertencia; un archivo huérfano no rompe la operación.
- Publicación: `UseStaticFiles` con `PhysicalFileProvider` sobre `Almacen:RutaBase` y `RequestPath = "/media"`, de modo que el catálogo devuelve `urlFotografia = "/media/espacios/{id}/{archivo}"` y nunca la ruta física.
- La baja del área no borra la fotografía: la baja es lógica y la evidencia debe conservarse; la limpieza de huérfanos es una tarea operativa fuera del alcance.

Alternativa descartada: BLOB en SQL Server. El plan lo prohíbe explícitamente y encarecería las consultas del catálogo.

### 8. Frontend Angular 20 en `features/areas`

```
src/app/features/areas/
+-- areas.routes.ts
+-- models/espacio.models.ts          # DTOs y enums espejo de la API
+-- services/espacios.api.ts          # HttpClient tipado
+-- estado/catalogo.store.ts          # Signals: filtros, pagina, resultados
+-- estado/espacios-admin.store.ts    # Signals: listado y seleccion
+-- pages/catalogo.page.ts            # publico, sin guard
+-- pages/espacio-detalle.page.ts     # publico, sin guard
+-- pages/admin/espacios-admin.page.ts
+-- pages/admin/espacio-form.page.ts  # alta y edicion
+-- pages/admin/puestos.page.ts       # puestos del area
+-- components/espacio-card.ts        # reutilizado por catalogo y admin
+-- components/filtros-catalogo.ts    # Reactive Forms + debounce
+-- components/plantilla-qr-panel.ts  # previsualizacion y descarga
```

- **Rutas:** `catalogo` y `catalogo/:id` son públicas; `administracion/espacios`, `administracion/espacios/nuevo`, `administracion/espacios/:id/editar` y `administracion/espacios/:id/puestos` usan `canActivate: [authGuard, roleGuard]` con `data: { roles: [ROLES.administrativo] }`, igual que la ruta `administracion` existente. Se registran con `loadChildren` para conservar el *lazy loading* por funcionalidad.
- **Estado:** `signal()` para resultados, filtros y estado de carga; `computed()` para el resumen de filtros activos y la paginación. No se introduce NgRx.
- **Filtros:** formulario reactivo con `debounceTime(300)` y `distinctUntilChanged` antes de disparar la consulta; los filtros se reflejan como *query params* (`q`, `categoria`, `aforoMinimo`) para que el catálogo sea enlazable.
- **Formulario de área:** validadores espejo del dominio (`required`, `maxLength`, `min(1)`, `max(aforoMaximo)` con el máximo publicado por la API), y el bloque de puestos deshabilitado cuando la categoría no admite puestos individuales.
- **Errores:** se reutiliza `problem-details.interceptor` de la Etapa 1; los `400` con `errors` por campo se pintan junto al control correspondiente y los `409` como mensaje de conflicto del formulario.
- **Plantilla QR:** el panel solicita el SVG con `responseType: 'blob'`, lo muestra en un `<object type="image/svg+xml">` y ofrece descarga con `URL.createObjectURL`; la URL se revoca al destruir el componente.
- **Fotografía:** `input type="file"` con vista previa local y `FormData` al endpoint de imagen; el mensaje de tamaño o tipo proviene del `400` del servidor.
- **Accesibilidad:** encabezados semánticos en tarjetas y tablas, `aria-live` para el resultado de los filtros y texto alternativo en la fotografía.

Alternativa descartada: un único componente con dos modos (público y administrativo). Mezclaría autorización, filtros y formularios en una plantilla difícil de probar.

### 9. Pruebas

| Nivel | Proyecto | Qué cubre |
| :--- | :--- | :--- |
| Dominio | `tests/Backend.UnitTests` | `CodigoQR` (formato, unicidad estadística, rehidratación inválida), aforo y puestos, `DomainException` esperada |
| Aplicación | `tests/Backend.UnitTests` | handlers con Moq sobre `IEspaciosRepository`, `IConsultaEspacios`, `IQrGenerator` y `IAlmacenArchivos`; mapeo de `Resultado<T>` |
| Arquitectura | `tests/Backend.ArchitectureTests` | la suite existente debe seguir verde: sin EF en aplicación y sin tipos de infraestructura en firmas públicas |
| Integración | `tests/Backend.IntegrationTests` | Testcontainers SQL Server 2022 + `WebApplicationFactory`: CRUD, `409` de aforo y de unicidad, `403`/`401`, catálogo con filtros, inmutabilidad del QR tras `PUT`, migración aplicada |
| Contrato QR | `tests/Backend.IntegrationTests` | el SVG individual contiene exactamente los módulos oscuros de `QRCodeGenerator.CreateQrCode(codigo, ECCLevel.Q)`, lo que prueba que la placa impresa es decodificable sin añadir un lector |
| Registro y vinculación | `tests/Backend.IntegrationTests` | primer alta como `Administrativo` con dos registros concurrentes, dominio y contraseña rechazados, vínculo idempotente, `409` por correo o `oid` ajenos y desvinculación |
| Roles | `tests/Backend.IntegrationTests` | `GET /api/usuarios` con filtros, `PUT .../rol` persistido, `409` del último administrador y de la autoedición, `403` de roles académicos y rol local prevalente sobre app roles |
| Frontend | `src/frontend` (Karma/Jasmine) | `catalogo.store` (filtros y paginación), `espacios.api` (URLs y `FormData`), guard de la ruta administrativa, formulario con aforo inválido |

La comparación módulo a módulo evita introducir un decodificador de imagen en las pruebas; la verificación con lector real queda como comprobación manual documentada en la tarea de cierre.

### 10. Configuración y despliegue

- Claves nuevas: `Espacios:AforoMaximo` (500), `Espacios:EtiquetasPorHoja` (18), `Espacios:TamanoMaximoImagenBytes` (5242880), `Almacen:RutaBase`, `Identidad:PermitirAutorregistro` (true) e `Identidad:DominioCorreoInstitucional` (lista separada por comas).
- `docker-compose.yml`: volumen nombrado `media` montado en `/var/lib/reservas` del servicio `api`, para que las fotografías sobrevivan a un `docker compose down`.
- `appsettings.Development.json` puede apuntar `Almacen:RutaBase` a una carpeta local; ningún valor sensible se versiona.
- La migración `AreasYPuestos` se aplica con `dotnet ef database update` en desarrollo (ya existe el arranque automático) y como paso explícito de despliegue en otros entornos.

### 11. Registro de cuentas con rol inicial derivado del estado del sistema

`POST /api/usuarios/registro` es anónimo y lo atiende `RegistrarUsuarioHandler` en `Backend.Application/Modules/Usuarios`.

```
POST /api/usuarios/registro
   |
   +-- Identidad:PermitirAutorregistro = false --> 403
   +-- correo fuera de Identidad:DominioCorreoInstitucional --> 400
   +-- correo ya registrado --> 409
   +-- contrasena fuera de politica --> 400 con detalle
   |
   +-- tx SERIALIZABLE:
         contar cuentas == 0 ? rol = Administrativo : rol = Estudiante
         crear cuenta + asignar rol + TipoActor derivado
   |
   +--> 201 { id, nombreCompleto, correo, rol }   (sin token)
```

- **Regla del primer administrador:** el rol se decide contando cuentas dentro de una transacción `Serializable` sobre `ApplicationDbContext`, de modo que dos registros concurrentes sobre un sistema vacío no producen dos administradores. Alternativa descartada: contar y crear sin transacción, que abre exactamente esa carrera.
- **Sin token en la respuesta:** el registro no inicia sesión; la SPA redirige al formulario de acceso con el correo precargado. Evita decidir aquí entre token local y sesión federada.
- **`TipoActor` derivado:** `Administrativo` → `Administrativo`, `Estudiante` → `Estudiante`, `Docente` → `Docente`. El tipo de actor nunca se recibe del cliente.
- **Validación de dominio de correo:** `Identidad:DominioCorreoInstitucional` admite una lista separada por comas; sin coincidencia, `400`. El objetivo es que el correo local coincida con el de Entra ID y la vinculación posterior no falle por discrepancia.
- **Política de contraseña:** se reutilizan las opciones de ASP.NET Core Identity ya configuradas (longitud mínima y composición); no se duplica la regla en el handler, se traduce el error de `UserManager` a `400` con el detalle por regla.
- **Limitación de tasa:** `AddRateLimiter` con ventana deslizante sobre el endpoint de registro y `OnRejected` escribiendo un problema RFC 7807 con `429`, para no romper el contrato de errores.
- **Puertos nuevos:** `IIdentityService` crece con `ContarCuentasAsync`, `BuscarCuentaLocalAsync`, `CrearCuentaLocalAsync`, `CambiarRolAsync` y `VincularIdentidadAsync`/`DesvincularIdentidadAsync`; el listado administrativo usa el puerto de lectura `IConsultaUsuarios` con proyección y `AsNoTracking`.

### 12. Vinculación de la cuenta con Entra ID desde una sesión local

```
SPA (sesion local) --> MSAL acquireTokenSilent(scope api) --> token Entra
      |                                                          |
      +--> POST /api/usuarios/me/vinculacion-entra  <------------+
             Authorization: Bearer <JWT local>
             { "tokenEntra": "<access token de Entra>" }
                    |
                    +-- IValidadorTokenEntra (Infrastructure)
                    |     firma, tid/issuer, audiencia, vigencia
                    |
                    +-- correo del token == correo local ? --no--> 409
                    +-- oid ya vinculado a otra cuenta ?  --si--> 409
                    +-- mismo oid ya vinculado a esta cuenta --> 200 idempotente
                    |
                    +--> guarda IdentificadorFederado = oid --> 200
```

- **Doble credencial en la misma solicitud:** la sesión local autoriza la operación y el token de Entra prueba la posesión de la identidad institucional. Por eso el endpoint exige `[Authorize]` y recibe el token en el cuerpo, no en el encabezado.
- **Validador compartido:** se extrae la construcción de `TokenValidationParameters` a `ConfiguracionTokenEntra`, usada por el esquema JwtBearer de Entra ID y por `ValidadorTokenEntra`. Así la vinculación valida con exactamente las mismas reglas que el inicio de sesión federado y no aparece una segunda política de confianza.
- **Puerto en la aplicación:** `IValidadorTokenEntra.ValidarAsync(string token, CancellationToken)` devuelve `IdentidadFederada` (`oid`, nombre, correo), el mismo contrato ya usado por el resolutor federado. La aplicación no referencia `Microsoft.IdentityModel`.
- **Coincidencia de correo obligatoria:** se compara el correo del token con `CorreoInstitucional` sin distinguir mayúsculas. Es la única barrera que impide que una cuenta secuestrada se apropie de la identidad institucional de otra persona.
- **Idempotencia:** repetir la vinculación con el mismo `oid` devuelve `200` y conserva la fecha de vínculo original; no se escribe de nuevo.
- **Sesión ya federada:** si el portador llegó por Entra ID, la cuenta ya está vinculada por definición y la operación responde `409` explicando que el acceso institucional ya está activo.
- **Desvinculación:** `DELETE /api/usuarios/me/vinculacion-entra` deja `IdentificadorFederado` en `null`; es idempotente y no toca la contraseña local. El índice único filtrado existente libera el `oid` para otra cuenta.
- **Auditoría:** se registra el `oid` y el identificador interno en la vinculación y la desvinculación; nunca el token ni su contenido completo.

### 13. El rol local como única fuente de autorización

```
Token Entra valido
      |
      v
EntraIdProfileResolver.ResolveAsync(oid)
      |
      +-- sin cuenta vinculada ----------> error SinVinculacion --> 403 (vincule su cuenta)
      +-- cuenta inactiva ---------------> error CuentaInactiva  --> 403
      +-- cuenta encontrada -------------> PerfilUsuario con los roles LOCALES
```

- **Se elimina la intersección con app roles:** el resolutor deja de calcular el rol efectivo a partir de `IdentidadFederada.Roles`; el claim de roles del token solo se conserva para auditoría. Consecuencia directa: un app role administrativo en Entra ID no concede permisos en la plataforma.
- **Se elimina el aprovisionamiento automático:** un `oid` sin vínculo ya no crea perfil. El primer acceso federado exige registrarse y vincular la cuenta; el mensaje del `403` lo indica. Esto convierte la vinculación en un paso obligatorio y auditable.
- **Retiro de `CrearPerfilFederadoAsync` y `AltaPerfilFederado`:** quedan sin uso al desaparecer el autoaprovisionamiento. Se retiran para no mantener dos caminos de alta con reglas de rol distintas; es un cambio interno, sin efecto en contratos REST.
- **Cambio de rol:** `PUT /api/usuarios/{id}/rol` delega en `IIdentityService.CambiarRolAsync`, que ejecuta `RemoveFromRole`/`AddToRole` y actualiza `TipoActor` dentro de una transacción del `DbContext` compartido con el almacén de Identity. Si la asignación falla, se revierte el rol anterior.
- **Guardas del cambio:** el objetivo no puede ser el propio portador (evita autodegradarse y perder la consola a mitad de la operación) y el sistema no puede quedar sin administradores activos (`409`).
- **Vigencia del cambio:** una sesión federada lo ve en la solicitud siguiente porque el middleware resuelve el perfil en cada petición; una sesión local conserva el rol del token ya emitido hasta `POST /api/auth/refresh`, que vuelve a leer los roles del almacén. La respuesta incluye `requiereRenovacion: true` y la consola lo advierte.
- **Lectura administrativa:** `IConsultaUsuarios.ListarAsync(filtro)` proyecta sobre las tablas de Identity con `AsNoTracking` y devuelve `id`, nombre, correo, rol, tipo de actor, activo, vinculado, creado y último acceso. No se expone ningún hash ni sello de seguridad.
- **Riesgo asumido:** mientras el administrador no corrija un rol, la cuenta conserva el rol inicial (`Estudiante`). Es la contrapartida de que el rol sea una decisión administrativa y no una derivación automática del proveedor.

### 14. Frontend: registro, doble acceso, vinculación y consola de usuarios

```
src/app/
+-- core/auth/entra.service.ts          # MSAL Browser: loginRedirect, acquireTokenSilent, logout
+-- core/auth/sesion.store.ts           # + origen: 'local' | 'entra'
+-- core/interceptors/jwt.interceptor.ts # elige el token segun el origen de la sesion
+-- features/auth/registro.page.ts      # /registro (publica)
+-- features/auth/entra-callback.page.ts# /auth/entra (handleRedirectPromise)
+-- features/auth/login.page.ts         # + boton "Correo de la universidad" + enlace a registro
+-- features/perfil/perfil.page.ts      # /perfil (authGuard): vincular y desvincular
+-- features/administracion/usuarios/usuarios-admin.page.ts # /administracion/usuarios
```

- **Dependencia `@azure/msal-browser` 5.26.0** con flujo `loginRedirect` y PKCE. Configuración en `environment.entraId` (`clientId`, `authority`, `redirectUri` y `scopes` del API); no se usa `@azure/msal-angular` porque la SPA solo necesita adquirir tokens, no interceptores propios de esa librería.
- **Sesión con origen:** `sesion.store` guarda `origen`. El interceptor adjunta el JWT local o el access token de Entra según ese valor, y `authGuard` acepta ambas sesiones. Un único punto de decisión evita mezclar credenciales.
- **Callback:** `/auth/entra` ejecuta `handleRedirectPromise()`, obtiene el token con `acquireTokenSilent` y redirige a la ruta solicitada. Si no hay cuenta MSAL activa, vuelve al acceso con un mensaje del interceptor de problemas.
- **Registro:** formulario reactivo con nombre, correo, contraseña y confirmación; el `201` muestra el rol asignado y redirige al acceso con el correo precargado. Los `400` por campo y el `409` de correo existente se pintan junto al control.
- **Perfil:** muestra correo, rol y estado del vínculo; el botón "Vincular cuenta de la universidad" obtiene el token de Entra y llama al endpoint, y "Desvincular" lo retira. Si el correo institucional no coincide, el `409` se explica en la propia tarjeta.
- **Consola de usuarios:** tabla paginada con filtros por texto, rol y vínculo; el rol se cambia con un `select` y confirmación, y el `409` del último administrador o de la autoedición se muestra como conflicto. La pantalla avisa de que la cuenta afectada debe renovar sesión.
- **Navegación:** `shared/shell` incorpora los enlaces "Usuarios" (solo `Administrativo`, visible con `HasRole`/`@if` sobre los roles de la sesión) y "Mi cuenta" para todos los usuarios autenticados.
- **Pruebas de componente:** registro con contraseña inválida, botón de Entra invocando `loginRedirect`, tarjeta de perfil con y sin vínculo, y consola de usuarios bloqueando a un rol académico.

## Risks / Trade-offs


- **Un código QR comprometido no se puede rotar** → Mitigación: valor opaco de 128 bits no enumerable, nunca expuesto en el catálogo público, y la sustitución implica reemplazar la unidad física; la limitación se documenta como decisión consciente.
- **Colisión de códigos QR** → Mitigación: índice único, traducción a `409` y un reintento acotado de la generación en el handler.
- **La búsqueda por texto pierde acentos según la *collation* del servidor** → Mitigación: la consulta de infraestructura aplica `Collate("Latin1_General_CI_AI")` de forma explícita y una prueba de integración busca `laboratorio` esperando `Laboratório`.
- **`LIKE '%texto%'` no usa el índice y degrada con volumen** → Mitigación: paginación obligatoria, tamaño máximo de página 50 y filtros indexados por estado y categoría; si el inventario supera decenas de miles de filas se evaluará índice de texto completo.
- **Edición concurrente del área** → Mitigación: `RowVersion` en el cuerpo del `PUT`; un `409` indica recargar y el cliente conserva los valores editados.
- **Archivo huérfano si la escritura en disco precede al `SaveChanges`** → Mitigación: borrado *best-effort* en el `catch` y verificación periódica operativa del volumen.
- **Almacenamiento local atado a una sola instancia** → Mitigación: `IAlmacenArchivos` es un puerto; migrar a un almacén de objetos (S3/MinIO) no cambia casos de uso ni contratos.
- **Plantilla SVG con muchas unidades** → Mitigación: máximo de 200 unidades por solicitud (`400` si se excede) y paginación por hoja, para acotar el tamaño de la respuesta.
- **Fidelidad tipográfica de la etiqueta** → Mitigación: el texto usa `font-family="monospace"` con tamaños absolutos; una variación de fuente no afecta al QR, que es el elemento funcional.
- **`QRCoder` como dependencia nueva** → Mitigación: licencia MIT, uso limitado a la matriz de módulos y encapsulado tras `IQrGenerator`.
- **Catálogo público sin límite de peticiones** → Mitigación: paginación con tamaño acotado; el control de tasa se delega en el proxy inverso en producción.
- **Carrera en el registro del primer administrador** → Mitigación: conteo y alta dentro de una transacción `Serializable`, con prueba de integración de dos registros concurrentes que verifica un solo `Administrativo`.
- **Autorregistro público como vector de cuentas basura** → Mitigación: dominio de correo obligatorio, limitación de tasa con `429`, política de contraseña y consola administrativa para corregir roles; la desactivación de cuentas queda como deuda de la siguiente iteración.
- **Una cuenta se apropia de la identidad institucional de otra persona** → Mitigación: la vinculación exige coincidencia exacta de correo con el perfil local y `oid` libre; cualquier discrepancia responde `409` sin escribir.
- **Sesión local con rol obsoleto tras un cambio administrativo** → Mitigación: el refresh vuelve a leer los roles y la consola advierte que la cuenta afectada debe renovar sesión; no se revocan tokens en caliente en esta etapa.
- **Despliegue sobre perfiles federados ya aprovisionados** → Mitigación: el autoaprovisionamiento de la Etapa 1 ya escribió `IdentificadorFederado` en esos perfiles, así que siguen resolviendo; solo las identidades nuevas reciben `403` y deben registrarse y vincularse.
- **Discrepancia entre el correo registrado y el `preferred_username` del inquilino** → Mitigación: mensaje `409` explícito en la tarjeta de vinculación; el administrador puede corregir el rol, pero el correo lo fija el usuario al registrarse.
- **Dos credenciales en la SPA (JWT local y token de Entra)** → Mitigación: `sesion.store` expone `origen` y el interceptor decide en un único punto; pruebas unitarias de ambos orígenes y del callback.

## Migration Plan

1. Dominio (`Espacio`, `Puesto`, `CodigoQR`, enums y mensajes) con pruebas unitarias; compilar sin tocar la base de datos.
2. Configuración Fluent API y migración `AreasYPuestos`; ejecutar `dotnet ef database update` y verificar índices únicos y `RowVersion`.
3. Puertos de aplicación y handlers de áreas con pruebas unitarias; la suite de arquitectura debe seguir en verde.
4. `QRCoderService`, `LocalFileStorageService` y endpoints de áreas con pruebas de integración, incluida la comparación de módulos del SVG.
5. Registro, vinculación federada y cambio de rol en el backend (`Modules/Usuarios`, `IValidadorTokenEntra`, ajuste del resolutor federado) con pruebas de integración de bootstrap, vínculo y guardas de administrador.
6. Funcionalidad Angular de áreas y de identidad (registro, acceso federado, perfil y consola de usuarios), con pruebas de store, servicios y componentes.
7. Cierre: `openspec validate --strict`, suite completa (`dotnet test`, `npm test`, `npm run build`) y humo extremo a extremo (registro del primer administrador → alta de área con QR → catálogo público → vinculación con Entra ID → cambio de rol de una cuenta).

**Rollback:** el cambio es aditivo. Revertir la rama y, si la migración ya se aplicó, ejecutar `dotnet ef database update InitialCreate` (el `Down` elimina solo `Puestos` y `Espacios`). El volumen `media` puede vaciarse sin afectar a la identidad ni a la autenticación.

## Open Questions

- ¿La institución exigirá restringir el acceso anónimo a las fotografías del catálogo? Hoy `/media` es público porque el catálogo lo es; cerrarlo implicaría servir imágenes por un endpoint autenticado.
- ¿Todas las áreas requieren placa QR o solo las que admiten reserva? El modelo permite ambas; la plantilla solo imprime unidades registradas y la decisión operativa no cambia el código.
- ¿La baja de un área con reservas futuras debe bloquearse? Depende del motor de la Etapa 2, que aún no existe; se resolverá al integrar reservas.
- ¿Se necesita exportación del inventario a CSV para migración de datos? Fuera de alcance; puede añadirse como consulta sin tocar el modelo.
- ¿El administrador debe poder crear cuentas en nombre de terceros (alta asistida) en lugar de depender del autorregistro? Hoy no se incluye; la consola solo lista y cambia roles.
- ¿Se debe notificar por correo al usuario afectado cuando cambia su rol? Requiere el módulo SMTP de la Etapa 2 y no cambia los contratos de esta etapa.
- ¿Qué ocurre con un usuario cuyo correo registrado no existe en el inquilino? Solo podrá entrar con contraseña local; la vinculación fallará con `409` hasta que use el correo institucional correcto.





