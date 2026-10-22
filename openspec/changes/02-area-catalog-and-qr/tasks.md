# Tasks

## 1. Bloque 1 — Backend, dominio y QR

- [ ] 1.1 Crear en `Backend.Domain` los enums `CategoriaEspacio`, `TipoAprobacion`, `EstadoUnidad` y la clase `MensajesEspacio`; verificar con `dotnet build src/Backend.sln` y con `dotnet test tests/Backend.ArchitectureTests` que el núcleo sigue sin paquetes tecnológicos.
- [ ] 1.2 Implementar el value object `CodigoQR` (`Generar`/`Desde`, Crockford Base32, longitud 29) y `Espacio`/`Puesto` con fábricas, `CambiarAforo`, `AgregarPuesto` y desactivación; verificar con pruebas unitarias de formato del código, colisión en 10 000 muestras, aforo fuera de rango y aforo menor que puestos activos.
- [ ] 1.3 Añadir en `Backend.Application` los puertos `IEspaciosRepository`, `IConsultaEspacios`, `IQrGenerator`, `IAlmacenArchivos`, el tipo `Resultado<T>` y los DTOs de áreas, puestos y catálogo; verificar con `dotnet test tests/Backend.ArchitectureTests` que la aplicación no referencia EF Core ni ASP.NET Core.
- [ ] 1.4 Implementar los handlers de alta, edición, baja y consulta de áreas y puestos con sus validaciones de unicidad y aforo; verificar con pruebas unitarias (xUnit + Moq) los casos de éxito, `400` por dato inválido, `404` y `409`.
- [ ] 1.5 Configurar `EspacioConfiguration` y `PuestoConfiguration`, los `DbSet<Espacio>`/`DbSet<Puesto>` y la migración `AreasYPuestos`; verificar con `dotnet ef database update` y una consulta a `sys.indexes` que existen los cuatro índices únicos y la columna `RowVersion`.
- [ ] 1.6 Implementar `EspaciosRepository` y `ConsultaEspacios` con `AsNoTracking`, proyecciones y `Collate("Latin1_General_CI_AI")`; verificar con pruebas de integración (Testcontainers SQL Server 2022) de filtros por texto, categoría, aforo, estado y paginación.
- [ ] 1.7 Referenciar `QRCoder` 1.8.1 en `Backend.Infrastructure` e implementar `QRCoderService` con SVG individual y hoja A4 paginada según `Espacios:EtiquetasPorHoja`; verificar que el SVG no contiene `<image` ni `data:` y que sus módulos oscuros coinciden con `QRCodeGenerator.CreateQrCode(codigo, ECCLevel.Q)`.
- [ ] 1.8 Implementar `LocalFileStorageService` y el servicio estático `/media` sobre `Almacen:RutaBase`; verificar con pruebas de integración la carga válida, el reemplazo, el rechazo por tipo o tamaño y la URL pública resultante.
- [ ] 1.9 Exponer `EspaciosController`, `PuestosController` y `CatalogoController` con sus DTOs en `Contracts`; verificar con pruebas de integración los códigos `201`, `204`, `400`, `401`, `403`, `404` y `409`, y que el código QR no cambia tras un `PUT`.
- [ ] 1.10 Documentar en `docs/areas-y-qr.md` los endpoints de áreas, usuarios y vinculación, la configuración y la migración; verificar que cada comando y URL del documento responde como se describe contra la API local.
- [ ] 1.11 Extender `IIdentityService` (`ContarCuentasAsync`, `CrearCuentaLocalAsync`, `CambiarRolAsync`, `VincularIdentidadAsync`, `DesvincularIdentidadAsync`) y añadir `IConsultaUsuarios` e `IValidadorTokenEntra`; verificar con `dotnet test tests/Backend.ArchitectureTests` que la aplicación no referencia EF Core, Identity ni `Microsoft.IdentityModel`.
- [ ] 1.12 Implementar `RegistrarUsuarioHandler` con validación de dominio institucional, política de contraseña y transacción serializable del primer administrador; verificar con pruebas de integración el primer alta como `Administrativo`, la segunda como `Estudiante`, dos registros concurrentes con un solo administrador y los rechazos `400`, `403` y `409`.
- [ ] 1.13 Implementar `ConfiguracionTokenEntra`, `ValidadorTokenEntra`, los handlers de vinculación/desvinculación y ajustar `EntraIdProfileResolver` para resolver solo por `oid` vinculado sin intersección de app roles ni aprovisionamiento automático; verificar con pruebas de integración el vínculo idempotente, los `409` por correo u `oid` ajenos y el `403` de una identidad no vinculada.
- [ ] 1.14 Implementar `ConsultarUsuariosHandler`, `CambiarRolHandler` y `UsuariosController` con las guardas de último administrador y autoedición; verificar con pruebas de integración el listado con filtros, el rol efectivo tras el refresh del token, los `409` de las guardas y los `401`/`403` de acceso.

## 2. Bloque 2 — Frontend Angular e integración

- [ ] 2.1 Crear `features/areas` con `areas.routes.ts`, los modelos y `EspaciosApi`; verificar con `npm run build` y pruebas de servicio con `HttpTestingController` de las URLs y del `FormData` de la fotografía.
- [ ] 2.2 Implementar `catalogo.store` y `espacios-admin.store` con Signals y filtros con `debounceTime`; verificar con pruebas unitarias los cambios de filtro, la paginación y la escritura de *query params*.
- [ ] 2.3 Implementar `catalogo.page`, `espacio-detalle.page` y `espacio-card` con estados de carga, vacío y error; verificar con pruebas de componente el render del catálogo y el mensaje sin coincidencias.
- [ ] 2.4 Implementar `espacios-admin.page`, `espacio-form.page` y `puestos.page` con validadores espejo del dominio y manejo de `400`/`409`; verificar con pruebas de formulario de aforo inválido, conflicto de aforo y bloqueo de la ruta sin rol `Administrativo`.
- [ ] 2.5 Implementar `plantilla-qr-panel` con previsualización SVG y descarga; verificar con prueba de componente que solicita `blob`, crea el objeto URL y lo revoca al destruir.
- [ ] 2.6 Registrar las rutas públicas y administrativas (catálogo, registro, callback de Entra, perfil y consola de usuarios) en `app.routes.ts` y los enlaces en `shared/shell`; verificar manualmente con `npm start` que el catálogo responde sin sesión y que `/administracion/espacios` y `/administracion/usuarios` redirigen al acceso sin token.
- [ ] 2.7 Añadir `@azure/msal-browser` 5.26.0 y `core/auth/entra.service.ts` con la configuración de `environment.entraId`; verificar con pruebas unitarias de `loginRedirect` y `acquireTokenSilent` sobre un mock y con `npm run build`.
- [ ] 2.8 Extender `sesion.store`, `jwt.interceptor` y `authGuard` con el origen de sesión (`local`/`entra`); verificar con pruebas unitarias que el encabezado usa el token local o el de Entra y que el guard acepta ambas sesiones.
- [ ] 2.9 Implementar `registro.page`, `entra-callback.page` y el botón "Correo de la universidad" en `login.page`; verificar con pruebas de componente la contraseña inválida, el `201` con el rol asignado y la invocación de `loginRedirect`.
- [ ] 2.10 Implementar `perfil.page` (vincular y desvincular) y `usuarios-admin.page` (listado, filtros y cambio de rol); verificar con pruebas de componente el estado del vínculo, el `409` de conflicto y el bloqueo de la consola sin rol `Administrativo`.
- [ ] 2.11 Ejecutar la verificación integral: `dotnet test src/Backend.sln`, `npm test`, `npm run build` y el humo (registro del primer administrador → alta de área con fotografía y dos puestos → plantilla QR de tres etiquetas → catálogo filtrado → vinculación con Entra ID → cambio de rol de una cuenta), registrando el resultado en el reporte de la etapa.

## Workflow follow-up

- Ejecutar `openspec validate 02-area-catalog-and-qr --strict` y corregir advertencias antes de la revisión.
- Archivar el cambio con el flujo de OpenSpec una vez aprobado el commit atómico.
