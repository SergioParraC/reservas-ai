# Áreas, catálogo QR y gestión de cuentas

Este documento describe los endpoints incorporados en la Etapa 2 para administrar la
infraestructura física del campus, publicar el catálogo y gobernar las cuentas y roles de la
plataforma. Todos los errores conservan el contrato RFC 7807 de la Etapa 1
(`application/problem+json` con `type`, `title`, `status`, `detail`, `traceId` y `errors` por campo).

## Requisitos previos

- API en `http://localhost:8080` (Docker) o en el puerto de `launchSettings.json` (desarrollo).
- Un token local del rol `Administrativo` para los endpoints de escritura.
- SQL Server 2022 con la migración `AreasYPuestos` aplicada.

```bash
dotnet ef database update \
  --project src/Backend.Infrastructure \
  --startup-project src/Backend.Presentation.Api
```

En `Development` la API aplica las migraciones pendientes y siembra los roles al arrancar; en
otros entornos la actualización es un paso explícito del despliegue.

## Catálogo público (sin autenticación)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/catalogo/espacios` | Áreas activas con filtros `q`, `categoria`, `aforoMinimo`, `aforoMaximo`, `pagina`, `tamano`. |
| `GET` | `/api/catalogo/espacios/{id}` | Detalle de un área activa; `404` si está dada de baja. |

```bash
curl "http://localhost:8080/api/catalogo/espacios?q=laboratorio&categoria=Laboratorio&aforoMinimo=20"
```

Respuesta:

```json
{
  "elementos": [
    {
      "id": "0b6f...",
      "nombre": "Laboratório de Redes",
      "ubicacion": "Bloque B",
      "categoria": 2,
      "aforo": 25,
      "tipoAprobacion": 2,
      "fotografiaUrl": "/media/espacios/0b6f.../archivo.png"
    }
  ],
  "total": 1,
  "pagina": 1,
  "tamano": 20
}
```

El catálogo no expone `codigoQR`, la ruta interna del archivo ni áreas inactivas. El texto se
compara con `Latin1_General_CI_AI`: `laboratorio` encuentra `Laboratório`.

## Mantenimiento de áreas (rol `Administrativo`)

| Método | Ruta | Éxito | Notas |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/espacios` | `200` | Incluye inactivas; filtros `q`, `categoria`, `estado`, `pagina`, `tamano`. |
| `GET` | `/api/espacios/{id}` | `200` | Detalle con puestos y código QR. |
| `POST` | `/api/espacios` | `201` + `Location` | Asigna `codigoQR` y estado `Activo`. |
| `PUT` | `/api/espacios/{id}` | `200` | No acepta `codigoQR`; usa `rowVersion` para concurrencia. |
| `DELETE` | `/api/espacios/{id}` | `204` | Baja lógica; conserva el QR. |
| `POST` | `/api/espacios/{id}/puestos` | `201` | Valida aforo e identificador único. |
| `PUT` | `/api/espacios/{id}/puestos/{puestoId}` | `200` | No acepta `codigoQR`. |
| `DELETE` | `/api/espacios/{id}/puestos/{puestoId}` | `204` | Baja lógica del puesto. |
| `POST` | `/api/espacios/{id}/imagen` | `200` | `multipart/form-data`, campo `imagen`. |
| `DELETE` | `/api/espacios/{id}/imagen` | `204` | Elimina archivo y ruta. |
| `GET` | `/api/espacios/{id}/qr.svg` | `200` `image/svg+xml` | Caché inmutable de un año. |
| `GET` | `/api/puestos/{puestoId}/qr.svg` | `200` `image/svg+xml` | QR del puesto. |
| `GET` | `/api/espacios/qr/plantilla` | `200` `image/svg+xml` | `espacioIds`, `incluirPuestos`, `hoja`; cabecera `X-Total-Hojas`. |

Alta de un área:

```bash
TOKEN="<jwt del administrador>"

curl -X POST http://localhost:8080/api/espacios \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
        "nombre": "Laboratorio de Redes",
        "ubicacion": "Bloque B",
        "categoria": "Laboratorio",
        "aforo": 25,
        "tipoAprobacion": "Docente"
      }'
```

Categorías admitidas: `Aula`, `Laboratorio`, `Auditorio`, `SalaComputo`, `Biblioteca`,
`Deportivo`, `Oficina`, `Otro`. Tipos de aprobación: `Automatica`, `Docente`, `Administrativo`.
Estados: `Activo`, `Inactivo`. Los valores no distinguen mayúsculas y un valor fuera del enum
responde `400` con el campo señalado.

Edición y baja:

```bash
curl -X PUT http://localhost:8080/api/espacios/$ID \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
        "nombre": "Laboratorio de Redes II",
        "ubicacion": "Bloque B, piso 2",
        "categoria": "Laboratorio",
        "aforo": 30,
        "tipoAprobacion": "Docente",
        "estado": "Activo",
        "rowVersion": "<valor devuelto por GET>"
      }'

curl -X DELETE http://localhost:8080/api/espacios/$ID -H "Authorization: Bearer $TOKEN"
```

Reglas verificadas por el dominio y la base de datos:

- Aforo entre `1` y `Espacios:AforoMaximo`.
- El aforo no puede quedar por debajo de los puestos activos (`409`).
- No se admiten dos áreas con el mismo nombre en la misma ubicación (`409`).
- `codigoQR` se genera una sola vez y ninguna operación permite sustituirlo.
- La ruta de la fotografía se guarda en base de datos; el archivo vive en `Almacen:RutaBase`.

Plantilla de impresión:

```bash
curl -OJ "http://localhost:8080/api/espacios/qr/plantilla?espacioIds=$ID&incluirPuestos=true&hoja=1" \
  -H "Authorization: Bearer $TOKEN"
```

Cada etiqueta incluye el QR vectorial, el nombre de la unidad, la ubicación y el código legible.
La plantilla pagina según `Espacios:EtiquetasPorHoja` (18 por omisión) e informa el total en
`X-Total-Hojas`.

## Autorregistro de cuentas

| Método | Ruta | Rol | Éxito |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/usuarios/registro` | Anónimo (limitado por tasa) | `201` con `id`, `nombreCompleto`, `correoInstitucional` y `rol` |

```bash
curl -X POST http://localhost:8080/api/usuarios/registro \
  -H "Content-Type: application/json" \
  -d '{
        "nombreCompleto": "Ana Administrativa",
        "correoInstitucional": "ana@uninpahu.edu.co",
        "contrasena": "Clave.Segura.2026"
      }'
```

- Si la base no tiene ninguna cuenta, el rol asignado es `Administrativo`; en cualquier otro caso
  es `Estudiante`. La decisión se toma dentro de una transacción serializable, de modo que dos
  registros concurrentes sobre un sistema vacío no crean dos administradores.
- La respuesta no emite token: la SPA redirige al acceso con el correo precargado.
- Se rechaza un correo fuera de `Identidad:DominioCorreoInstitucional` (`400`), un correo ya
  registrado (`409`), una contraseña que no cumple la política de Identity (`400`) y cualquier
  registro cuando `Identidad:PermitirAutorregistro` es `false` (`403`).
- El extremo aplica una ventana deslizante de 5 solicitudes por minuto y dirección; el rechazo
  responde `429` en formato RFC 7807.

## Vinculación con Entra ID

| Método | Ruta | Rol | Éxito |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/usuarios/me/vinculacion-entra` | Sesión local | `200` con `vinculado` y `correoInstitucional` |
| `DELETE` | `/api/usuarios/me/vinculacion-entra` | Cualquier sesión | `204` |

```bash
curl -X POST http://localhost:8080/api/usuarios/me/vinculacion-entra \
  -H "Authorization: Bearer $TOKEN_LOCAL" \
  -H "Content-Type: application/json" \
  -d '{ "tokenEntra": "<access token del inquilino>" }'
```

El token de Entra ID se valida con la misma configuración de confianza que el esquema federado
(`ConfiguracionTokenEntra`): firma, emisor, audiencia, vigencia e inquilino. El correo del token
debe coincidir con el de la cuenta local y el `oid` no puede pertenecer a otra cuenta; cualquier
discrepancia responde `409` sin escribir. Repetir la vinculación con el mismo `oid` es idempotente.
Una sesión ya federada responde `409`: el acceso institucional ya está activo.

La desvinculación deja `IdentificadorFederado` en `NULL`, conserva la contraseña local y es
idempotente. A partir de la Etapa 2 el rol efectivo de una sesión federada es el asignado en la
plataforma; los app roles del token no conceden permisos y una identidad sin vínculo recibe `403`
con `type` terminado en `/vinculacion-requerida`.

## Consola administrativa de usuarios

| Método | Ruta | Rol | Descripción |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/usuarios` | `Administrativo` | Listado paginado con filtros `q`, `rol`, `vinculado`, `pagina`, `tamano`. |
| `PUT` | `/api/usuarios/{id}/rol` | `Administrativo` | Cambia el rol a `Administrativo`, `Docente` o `Estudiante`. |

```bash
curl "http://localhost:8080/api/usuarios?rol=Estudiante&vinculado=false" \
  -H "Authorization: Bearer $TOKEN"

curl -X PUT http://localhost:8080/api/usuarios/$USUARIO_ID/rol \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "rol": "Docente" }'
```

- La respuesta del cambio incluye `requiereRenovacion: true`; una sesión local conserva el rol
  anterior hasta que se emite un token nuevo (nuevo inicio de sesión), mientras que una sesión
  federada lo ve en la solicitud siguiente.
- El administrador no puede cambiar su propio rol (`409`) y el sistema no puede quedarse sin
  administradores activos (`409`).
- Un rol distinto de los tres oficiales responde `400`; una cuenta inexistente, `404`; un rol
  académico que intente usar la consola, `403`; una solicitud sin credenciales, `401`.
- La proyección nunca devuelve contraseñas, hashes ni sellos de seguridad.

## Configuración

| Clave | Valor por omisión | Uso |
| :--- | :--- | :--- |
| `Espacios:AforoMaximo` | `500` | Límite institucional del aforo. |
| `Espacios:EtiquetasPorHoja` | `18` | Capacidad de la plantilla A4. |
| `Espacios:TamanoMaximoImagenBytes` | `5242880` | Tamaño máximo de la fotografía. |
| `Espacios:MaximoUnidadesPlantilla` | `200` | Unidades por solicitud de plantilla. |
| `Almacen:RutaBase` | `/var/lib/reservas` | Raíz física del almacén de fotografías. |
| `Identidad:PermitirAutorregistro` | `true` | Habilita el formulario público. |
| `Identidad:DominioCorreoInstitucional` | `uninpahu.edu.co` | Dominios admitidos, separados por comas; el prefijo `@` es opcional. |

En `docker-compose.yml` el servicio `api` monta el volumen nombrado `media` en
`/var/lib/reservas`; las fotografías sobreviven a `docker compose down`. La API publica ese
directorio como `/media` y el catálogo devuelve `fotografiaUrl` con esa ruta, nunca la física.

## Migración y persistencia

La migración `AreasYPuestos` crea las tablas `Espacios` y `Puestos`, la conversión del código QR
(`nvarchar(29)`), `RowVersion` para concurrencia optimista y los índices:

- `IX_Espacios_CodigoQR` (único)
- `IX_Puestos_CodigoQR` (único)
- `IX_Espacios_Nombre_Ubicacion` (único)
- `IX_Puestos_EspacioId_Identificador` (único)
- `IX_Espacios_Estado_Categoria_Aforo` (búsqueda del catálogo)

El rollback es aditivo: revertir la rama y ejecutar
`dotnet ef database update InitialCreate` elimina solo `Puestos` y `Espacios`.

## Verificación

```bash
dotnet build src/Backend.sln
dotnet test tests/Backend.UnitTests
dotnet test tests/Backend.ArchitectureTests
dotnet test tests/Backend.IntegrationTests   # requiere Docker
```

Las pruebas de integración cubren el primer administrador y la carrera de registros, la
vinculación idempotente y sus conflictos, el cambio de rol con guardas, el CRUD de áreas,
la inmutabilidad del QR, el catálogo con filtros sin acentos, la fotografía pública y la
plantilla paginada.
