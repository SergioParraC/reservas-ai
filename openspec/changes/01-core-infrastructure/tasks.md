# Tasks

## 1. Solución base y regla de dependencias

- [ ] 1.1 Crear `src/Backend.sln` con los proyectos `Backend.Domain`, `Backend.Application`, `Backend.Infrastructure` y `Backend.Presentation.Api` sobre `net10.0`, verificando con `dotnet build src/Backend.sln` que compila sin advertencias.
- [ ] 1.2 Declarar las referencias de compilación unidireccionales (`Application → Domain`, `Infrastructure → Application, Domain`, `Presentation.Api → Application, Infrastructure`) y verificar con `dotnet list <proyecto> reference` que no existe ninguna referencia hacia una capa exterior.
- [ ] 1.3 Configurar `Directory.Build.props` con `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true` y `LangVersion` más reciente, verificando que una advertencia intencional rompe la compilación.
- [ ] 1.4 Crear `tests/Backend.ArchitectureTests` con `NetArchTest.Rules` y las pruebas de dependencias entre capas, dominio sin paquetes tecnológicos y firmas de presentación sin tipos de infraestructura; verificar con `dotnet test tests/Backend.ArchitectureTests` que las tres pruebas pasan sobre la solución vacía.
- [ ] 1.5 Comprobar que la prueba de arquitectura detecta la violación: agregar temporalmente una referencia de `Backend.Domain` a `Backend.Infrastructure`, ejecutar `dotnet test` y confirmar que falla; luego revertir el cambio y confirmar que vuelve a pasar.
- [ ] 1.6 Documentar en `README.md` los requisitos previos (.NET 10 SDK, Node, Docker) y los comandos de compilación y pruebas, verificando que cada comando copiado del documento se ejecuta tal cual.

## 2. Núcleo de dominio y puertos de aplicación

- [ ] 2.1 Crear en `Backend.Domain` los enums `TipoActor` (`Administrador`, `Docente`, `Estudiante`, `Soporte`) y `EstadoUsuario`, sin referencias a paquetes externos; verificar con `dotnet list src/Backend.Domain package` que la salida no lista paquetes.
- [ ] 2.2 Crear `DomainException` y las constantes de mensajes de negocio de identidad en `Backend.Domain/Exceptions`, verificando que la prueba de arquitectura del núcleo sigue pasando.
- [ ] 2.3 Definir en `Backend.Application/Interfaces` los puertos `IApplicationDbContext` (`DbSet<Usuario>`, `DbSet<Rol>`, `SaveChangesAsync`), `IIdentityService`, `ITokenService`, `IRelojSistema` e `IUsuarioActual`, verificando que compilan con solo `Microsoft.EntityFrameworkCore` como paquete de la capa.
- [ ] 2.4 Definir los DTOs y contratos internos de autenticación (`CredencialesLocales`, `ResultadoAutenticacion`, `TokenEmitido`, `PerfilUsuario`, DTOs de alta de perfil federado) sin tipos de Identity, verificando que la prueba de arquitectura no detecta tipos de infraestructura en las firmas públicas.
- [ ] 2.5 Implementar `LoginLocalHandler` en `Backend.Application/Autenticacion` que orquesta verificación de credenciales, control de bloqueo y emisión de token, y verificar con pruebas unitarias (xUnit + Moq + FluentAssertions) los casos: credenciales válidas, contraseña incorrecta, usuario inexistente, cuenta inactiva y cuenta bloqueada.
- [ ] 2.6 Implementar `ObtenerPerfilHandler` y las pruebas unitarias que confirman que el perfil devuelto contiene nombre, correo, tipo de actor y roles, y que no incluye datos de seguridad del almacén de identidad.

## 3. Persistencia de identidad con EF Core 10 y SQL Server 2022

- [ ] 3.1 Agregar a `Backend.Infrastructure` los paquetes de EF Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`, `Design`) y las referencias de Identity, verificando que `dotnet restore` y `dotnet build` terminan sin errores.
- [ ] 3.2 Implementar `Usuario : IdentityUser<Guid>` y `Rol : IdentityRole<Guid>` con `NombreCompleto`, `TipoActor`, `IdentificadorFederado`, `Activo`, `CreadoEn` y `UltimoAccesoEn`, verificando que la compilación pasa con constructores privados y propiedades de solo escritura interna.
- [ ] 3.3 Implementar `ApplicationDbContext : IdentityDbContext<Usuario, Rol, Guid>, IApplicationDbContext` con `ApplyConfigurationsFromAssembly` y verificar que la prueba de arquitectura confirma que ningún tipo de EF aparece en las firmas de presentación.
- [ ] 3.4 Crear las configuraciones Fluent API (`UsuarioConfiguration`, `RolConfiguration`) con tabla `AspNetUsers`, longitudes máximas, conversión de `TipoActor` a `int` e índice único filtrado sobre `IdentificadorFederado`; verificar que el modelo generado (`dotnet ef dbcontext script` o snapshot) contiene el índice.
- [ ] 3.5 Añadir `AddInfrastructure(IConfiguration)` en `Backend.Infrastructure/DependencyInjection.cs` con `UseSqlServer(..., EnableRetryOnFailure)` y registro de servicios, verificando que `dotnet build` pasa y que la cadena de conexión ausente produce un error de arranque explícito.
- [ ] 3.6 Generar la migración inicial `InitialIdentity` con `dotnet ef migrations add` y verificar que el script SQL generado crea las tablas de Identity, el índice filtrado y las columnas de auditoría.
- [ ] 3.7 Implementar `InicializadorIdentity` idempotente que crea los roles base y el administrador inicial desde `Seed:AdminEmail` / `Seed:AdminPassword`, verificando con una prueba de integración que ejecutarlo dos veces no duplica usuarios ni roles.
- [ ] 3.8 Configurar en `Program.cs` la aplicación de migraciones automática solo en `Development`, verificando con una prueba de integración (Testcontainers SQL Server 2022) que el esquema se aplica y que un entorno no-Development no migra automáticamente.
- [ ] 3.9 Documentar en `docs/persistencia.md` el esquema de identidad, el comando de migración y la política de seed, verificando que los comandos documentados ejecutan sin error contra el contenedor.

## 4. Autenticación local con JWT propio

- [ ] 4.1 Implementar `JwtTokenService` en `Backend.Infrastructure/Identity` con firma HS256, claims `sub`, `jti`, `email`, `name`, `roles`, `iss`, `aud`, `iat`, `exp` y expiración configurable (`Jwt:ExpiraMinutos`, por defecto 60); verificar con pruebas unitarias que el token decodificado contiene exactamente esos claims y la expiración esperada.
- [ ] 4.2 Implementar `IdentityService` que encapsula `UserManager`/`RoleManager` (`VerificarCredencialesAsync`, `ObtenerRolesAsync`, `BuscarPorCorreoAsync`, `ActualizarUltimoAccesoAsync`) y verificar con pruebas unitarias por sustitución que no se filtran tipos de Identity hacia el caso de uso.
- [ ] 4.3 Configurar el bloqueo por intentos fallidos (`LockoutOptions`) y verificar con una prueba de integración que superar el umbral responde `423 Locked` con `type` de cuenta bloqueada y que un login exitoso reinicia el contador.
- [ ] 4.4 Añadir la validación de la política de contraseñas y el requisito de correo único normalizado, verificando con una prueba de integración que un alta de usuario con correo repetido es rechazada por el almacén de identidad.
- [ ] 4.5 Configurar la clave de firma fuera del repositorio (`Jwt:Key` por User Secrets en desarrollo y variable de entorno en contenedor) y verificar que `appsettings.json` versionado no contiene ninguna clave ni cadena de conexión real.
- [ ] 4.6 Implementar `POST /api/auth/login` en `AuthController` traduciendo `ResultadoAutenticacion` a `200 OK` con token o al problema RFC 7807 correspondiente, verificando con pruebas de integración los escenarios de credenciales válidas, incorrectas y correo no registrado.
- [ ] 4.7 Implementar `GET /api/auth/me` que resuelve el portador del token y devuelve nombre, correo, tipo de actor y roles, verificando con pruebas de integración que responde `200` con token vigente y `401` sin token.
- [ ] 4.8 Aplicar `[Authorize(Roles = "Administrador,Soporte")]` a un endpoint administrativo de verificación y comprobar con pruebas de integración las respuestas `200`, `403` y `401` según el rol y la presencia del token.

## 5. Contrato de errores RFC 7807

- [ ] 5.1 Registrar `AddProblemDetails()` con `CustomizeProblemDetails` que añade `traceId` e `instance`, verificando con una prueba de integración que la respuesta incluye los miembros `type`, `title`, `status`, `detail`, `instance` y la extensión `traceId`.
- [ ] 5.2 Implementar el catálogo de `type` estables (`credenciales-invalidas`, `token-ausente`, `token-invalido`, `sesion-expirada`, `permisos-insuficientes`, `cuenta-bloqueada`, `solicitud-invalida`, `proveedor-identidad-no-disponible`, `error-interno`) con `Content-Type: application/problem+json`, verificando con pruebas que cada condición produce el `status` y el `type` documentados en `design.md`.
- [ ] 5.3 Implementar `ProblemDetailsExceptionHandler` (`IExceptionHandler`) para excepciones no controladas y verificar con una prueba de integración que responde `500` sin trazas de pila ni nombres de clases internas.
- [ ] 5.4 Configurar la validación de DTOs de autenticación para que un cuerpo inválido responda `400` con `errors` por campo y sin ejecutar la verificación de credenciales; verificar con pruebas de integración de cuerpo incompleto y correo con formato inválido.
- [ ] 5.5 Implementar los `JwtBearerEvents` (`OnChallenge`, `OnForbidden`, `OnAuthenticationFailed`) que emiten los problemas de autenticación y autorización con `WWW-Authenticate` cuando corresponde, verificando con pruebas de integración que el `401` por ausencia de token incluye el encabezado y el `401` de login no.
- [ ] 5.6 Comprobar que los mensajes no permiten enumerar usuarios: verificar con una prueba de integración que el cuerpo del `401` es idéntico para correo inexistente y contraseña incorrecta.

## 6. Autenticación híbrida con Entra ID

- [ ] 6.1 Registrar `AddJwtBearer("LocalJwt")` con `iss`, `aud`, clave simétrica, `ValidateLifetime` y `ClockSkew` de 30 segundos, verificando con pruebas de integración que un token alterado o de otro emisor recibe `401` con `type` de token inválido.
- [ ] 6.2 Registrar `AddJwtBearer("EntraId")` con `Authority` del inquilino, `Audience` del cliente, validación de emisor y `RoleClaimType="roles"`; verificar con pruebas de integración (metadatos simulados) que un token con firma válida del inquilino accede y uno expirado o de otra audiencia recibe `401`.
- [ ] 6.3 Implementar el `PolicyScheme` de resolución (`ForwardDefaultSelector` por `iss`) y verificar con pruebas que un token local se valida solo con la clave local, un token federado solo con las claves del inquilino y un `iss` falsificado se rechaza por firma.
- [ ] 6.4 Implementar `EntraIdProfileResolver` que resuelve o crea el perfil local por `oid` con rol `Docente` o `Estudiante` derivado de app roles, verificando con pruebas de integración el primer acceso (alta del perfil) y el segundo (reutilización sin duplicar).
- [ ] 6.5 Verificar que un token federado válido sin habilitación académica responde `403` con `type` de permisos insuficientes y que no se crea ni modifica ningún rol administrativo.
- [ ] 6.6 Implementar el mapeo del fallo de metadatos/JWKS a `503` con `type` de proveedor no disponible, verificando con una prueba de integración que inyecta un `IConfigurationManager` con error de red y que el login local sigue respondiendo `200` en la misma ejecución.
- [ ] 6.7 Unificar `NameClaimType` y `RoleClaimType` en ambos esquemas y verificar con una prueba de integración que `[Authorize(Roles=...)]` autoriza igual a un docente federado y a un administrador local con los roles correctos.
- [ ] 6.8 Añadir el registro de auditoría de intentos federados (usuario interno, rol aplicado, resultado y marca de tiempo, sin token ni datos sensibles) y verificar con una prueba que el log de acceso exitoso y el de rechazo contienen los campos esperados.
- [ ] 6.9 Documentar en `docs/autenticacion.md` las variables `Jwt:*`, `EntraId:TenantId`, `EntraId:ClientId` y el procedimiento de activación federada, verificando que un lector puede configurar el esquema solo con ese documento.

## 7. SPA base en Angular 20

- [ ] 7.1 Generar el workspace Angular 20 en `src/frontend` con `bootstrapApplication`, `app.config.ts` (`provideRouter`, `provideHttpClient(withInterceptors)`, `provideZonelessChangeDetection`) y sin `NgModule`; verificar con `npm run build` y con una búsqueda de `@NgModule` sin resultados.
- [ ] 7.2 Crear la estructura `core/`, `shared/` y `features/` con rutas lazy y una página protegida mínima, verificando que `npm run build` genera el paquete estático y que la ruta protegida existe en el bundle.
- [ ] 7.3 Implementar `AuthService` y `SesionStore` con Signals (login, logout, `estaAutenticado`, roles) y verificar con pruebas de TestBed que el estado cambia tras login y se limpia tras logout.
- [ ] 7.4 Implementar `jwt.interceptor.ts` que adjunta `Authorization: Bearer` solo a las solicitudes dirigidas a la API, verificando con pruebas unitarias que se añade el encabezado, que no se añade sin sesión y que una recarga de la aplicación conserva la sesión vigente.
- [ ] 7.5 Implementar `problem-details.interceptor.ts` que traduce el contrato RFC 7807, redirige al login ante `sesion-expirada`, muestra el `detail` ante `403` y aplica un mensaje genérico ante respuestas que no cumplen el formato; verificar con pruebas unitarias de los tres casos.
- [ ] 7.6 Implementar `auth.guard.ts` y `role.guard.ts` funcionales y verificar con pruebas unitarias que una ruta protegida sin sesión redirige al acceso y que un rol insuficiente no renderiza el contenido.
- [ ] 7.7 Implementar la pantalla de login con el formulario reactivo y el manejo de errores del interceptor, verificando con pruebas de componente que un envío válido autentica y que uno inválido muestra el mensaje del problema.
- [ ] 7.8 Configurar `proxy.conf.json` y los entornos (`environment.development.ts`, `environment.ts`) con la URL base de la API, verificando con `npm start` que una llamada a `/api/auth/me` llega al backend sin errores de CORS.
- [ ] 7.9 Configurar CORS en la API para el origen de la SPA y verificar con una prueba de integración que un origen permitido recibe los encabezados y uno no permitido es rechazado.

## 8. Entorno de ejecución y automatización

- [ ] 8.1 Crear `docker-compose.yml` con `mssql` (`2022-latest`, volumen, `healthcheck` con `sqlcmd`) y `api` (`depends_on: service_healthy`), verificando con `docker compose up -d` que ambos servicios quedan `healthy`.
- [ ] 8.2 Crear el `Dockerfile` multi-stage de la API con usuario no root, verificando con `docker build` y con una solicitud a `/health` desde el contenedor.
- [ ] 8.3 Exponer `/health` con `AddHealthChecks().AddDbContextCheck<ApplicationDbContext>()`, verificando que responde `200` con la base disponible y `503` al detener el contenedor de SQL Server.
- [ ] 8.4 Añadir `tests/Backend.UnitTests` al pipeline de compilación y verificar con `dotnet test src/Backend.sln` que unitarias y arquitectura se ejecutan juntas.
- [ ] 8.5 Configurar el pipeline de CI (`.github/workflows/ci.yml`) con restore, build Release, pruebas de arquitectura, unitarias e integración con Testcontainers, verificando que una ejecución verde produce los tres reportes y que una violación de capas rompe el pipeline.
- [ ] 8.6 Documentar en `docs/infraestructura-base.md` el arranque local completo (compose, migraciones, seed, SPA) y verificar siguiendo el documento en una copia limpia del repositorio.

## 9. Verificación integral de la etapa

- [ ] 9.1 Ejecutar la verificación de humo completa (compose arriba → login local de administrador → `GET /api/auth/me` → endpoint administrativo `200` → cierre de sesión → `401`) y registrar el resultado en el reporte de la etapa.
- [ ] 9.2 Ejecutar la suite completa (`dotnet test src/Backend.sln`, `npm test`, `npm run build`) y confirmar que no hay pruebas fallidas ni advertencias de compilación.
- [ ] 9.3 Revisar la matriz de trazabilidad de las cuatro capacidades (arquitectura, autenticación local, federación Entra ID, RFC 7807) contra escenarios y pruebas, confirmando que cada escenario tiene al menos una prueba automatizada o una verificación manual documentada.

## Workflow follow-up

- Ejecutar `openspec validate 01-core-infrastructure --strict` y corregir cualquier advertencia antes de solicitar revisión.
- Archivar el cambio con el flujo de OpenSpec una vez que la revisión y el commit atómico estén aprobados.
- Verificar el resultado archivado en `openspec/specs/` antes de iniciar el cambio de la siguiente capacidad.
