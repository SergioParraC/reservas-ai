# Verificación de la Etapa 2

Fecha de ejecución: 2026-10-23.

## Resultado

| Verificación | Comando | Resultado |
| :--- | :--- | :--- |
| Compilación del backend | `dotnet build src/Backend.sln` | Correcta, 0 advertencias y 0 errores |
| Pruebas del backend | `dotnet test src/Backend.sln` | **117 correctas**: 68 unitarias, 4 de arquitectura y 45 de integración |
| Pruebas de la SPA | `npm test` (en `src/frontend`) | **52 correctas** |
| Compilación de la SPA | `npm run build` (en `src/frontend`) | Correcta, bundle generado en `dist/frontend` |
| Servidor de desarrollo | `npm start -- --host 127.0.0.1 --port 4300` | `/catalogo` responde `200` con `app-root` |
| Especificación | `openspec validate 02-area-catalog-and-qr --strict` | Válida |

## Humo integral

El flujo se ejecuta en `tests/Backend.IntegrationTests/Smoke/FlujoEtapa2Tests.cs` contra la API
real hospedada con SQL Server 2022 (Testcontainers) y el almacén de fotografías en un directorio
temporal:

1. Registro del primer administrador (`201`, rol `Administrativo`).
2. Alta de un área con aforo 3 y código QR de 29 caracteres.
3. Carga de una fotografía PNG y comprobación de la URL pública `/media/espacios/...`.
4. Alta de dos puestos (`PC-01`, `PC-02`).
5. Plantilla QR de una hoja con tres etiquetas: un `<path>` por área y por puesto, sin `<image>`
   ni `data:`.
6. Consulta anónima del catálogo con texto, categoría y aforo mínimo; total 1 y sin `codigoQR`.
7. Registro de la segunda cuenta, inicio de sesión y vinculación con Entra ID.
8. Cambio de rol a `Docente` por el administrador, respuesta con `requiereRenovacion` y nuevo
   token con el rol vigente.

La vinculación del paso 7 usa el contrato `IValidadorTokenEntra` con un validador simulado en el
host de pruebas; el código de producción valida firma, emisor, audiencia, vigencia e inquilino con
`ValidadorTokenEntra`. Para ejecutar ese paso contra un inquilino real deben configurarse
`EntraId:TenantId`, `EntraId:ClientId` y `environment.entraId` (cliente MSAL, autoridad y scopes).

## Pruebas de la SPA relevantes

- `espacios.api.spec.ts`: URLs, cuerpos con enums, `FormData` de la fotografía y descarga `blob`.
- `catalogo.store.spec.ts`: debounce de 300 ms, filtros, paginación y *query params*.
- `catalogo.page.spec.ts`: tarjetas, estado vacío y delegación de filtros.
- `espacio-form.page.spec.ts`: aforo inválido, `400` por campo y `409` de conflicto.
- `plantilla-qr-panel.spec.ts`: solicitud `blob`, objeto URL y revocación al destruir.
- `entra.service.spec.ts`: `loginRedirect` y `acquireTokenSilent` sobre MSAL simulado.
- `registro.page.spec.ts`, `login.page.spec.ts`, `perfil.page.spec.ts` y
  `usuarios-admin.page.spec.ts`: contraseña inválida, rol asignado, SSO, vínculo/desvínculo,
  `409` y bloqueo de la consola sin rol `Administrativo`.
- `sesion.store.spec.ts`, `jwt.interceptor.spec.ts` y `auth.guard.spec.ts`: origen `local`/`entra`.

## Limitaciones

- El humo visual en un navegador real no se automatiza; las rutas y guards se validan con pruebas
  unitarias y el servidor de desarrollo se comprobó por HTTP.
- La vinculación con un inquilino Entra real requiere credenciales del registro de aplicación; el
  contrato y los rechazos se cubren con el validador simulado y las pruebas de integración.
