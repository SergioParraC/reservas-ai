# Tasks

## 1. SoluciÃ³n base y regla de dependencias

- [x] 1.1 Crear `src/Backend.sln` con los proyectos `Backend.Domain`, `Backend.Application`, `Backend.Infrastructure` y `Backend.Presentation.Api` sobre `net10.0`, verificando con `dotnet build src/Backend.sln` que compila sin advertencias.
- [x] 1.2 Declarar las referencias de compilaciÃ³n unidireccionales (`Application â†’ Domain`, `Infrastructure â†’ Application, Domain`, `Presentation.Api â†’ Application, Infrastructure`) y verificar con `dotnet list <proyecto> reference` que no existe ninguna referencia hacia una capa exterior.
- [x] 1.3 Configurar `Directory.Build.props` con `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true` y `LangVersion` mÃ¡s reciente, verificando que una advertencia intencional rompe la compilaciÃ³n.
- [x] 1.4 Crear `tests/Backend.ArchitectureTests` con `NetArchTest.Rules` y las pruebas de dependencias entre capas, dominio sin paquetes tecnolÃ³gicos, aplicaciÃ³n sin tipos de Identity ni de EF Core y firmas de presentaciÃ³n sin tipos de infraestructura; verificar con `dotnet test tests/Backend.ArchitectureTests` que las cuatro pruebas pasan sobre la soluciÃ³n base.
- [x] 1.5 Comprobar que la prueba de arquitectura detecta la violaciÃ³n: agregar temporalmente una referencia de `Backend.Domain` a `Backend.Infrastructure`, ejecutar `dotnet test` y confirmar que falla; luego revertir el cambio y confirmar que vuelve a pasar.
- [x] 1.6 Documentar en `README.md` los requisitos previos (.NET 10 SDK, Node, Docker) y los comandos de compilaciÃ³n y pruebas, verificando que cada comando copiado del documento se ejecuta tal cual.

## 2. NÃºcleo de dominio y puertos de aplicaciÃ³n

- [x] 2.1 Crear en `Backend.Domain` los enums `TipoActor` (`Administrativo`, `Docente`, `Estudiante`) y `EstadoUsuario`, sin referencias a paquetes externos; verificar con `dotnet list src/Backend.Domain package` que la salida no lista paquetes.
- [x] 2.2 Crear `DomainException` y las constantes de mensajes de negocio de identidad en `Backend.Domain/Exceptions`, verificando que la prueba de arquitectura del nÃºcleo sigue pasando.
- [x] 2.3 Definir en `Backend.Application/Interfaces` los puertos `IApplicationDbContext` (solo `SaveChangesAsync`, sin `DbSet<Usuario>` ni `DbSet<Rol>` ni tipos de ASP.NET Core Identity), `IIdentityService` (gestiÃ³n de usuarios, roles y verificaciÃ³n de credenciales), `ITokenService`, `IRelojSistema` e `IUsuarioActual`, verificando que `Backend.Application` no declara paquetes de Identity, de ASP.NET Core ni de EF Core y que la prueba de arquitectura confirma el aislamiento.
- [x] 2.4 Definir los DTOs y contratos internos de autenticaciÃ³n (`CredencialesLocales`, `ResultadoAutenticacion`, `TokenEmitido`, `PerfilUsuario`, DTOs de alta de perfil federado) sin tipos de Identity, verificando que la prueba de arquitectura no detecta tipos de infraestructura en las firmas pÃºblicas.
- [x] 2.5 Implementar `LoginLocalHandler` en `Backend.Application/Autenticacion` que orquesta verificaciÃ³n de credenciales, control de bloqueo y emisiÃ³n de token, y verificar con pruebas unitarias (xUnit + Moq + FluentAssertions) los casos: credenciales vÃ¡lidas, contraseÃ±a incorrecta, usuario inexistente, cuenta inactiva y cuenta bloqueada.
- [x] 2.6 Implementar `ObtenerPerfilHandler` y las pruebas unitarias que confirman que el perfil devuelto contiene nombre, correo, tipo de actor y roles, y que no incluye datos de seguridad del almacÃ©n de identidad.

## 3. Persistencia de identidad con EF Core 10 y SQL Server 2022

- [x] 3.1 Agregar a `Backend.Infrastructure` los paquetes de EF Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`, `Design`) y las referencias de Identity, verificando que `dotnet restore` y `dotnet build` terminan sin errores.
- [x] 3.2 Implementar `Usuario : IdentityUser<Guid>` y `Rol : IdentityRole<Guid>` con `NombreCompleto`, `TipoActor`, `IdentificadorFederado`, `Activo`, `CreadoEn` y `UltimoAccesoEn`, verificando que la compilaciÃ³n pasa con constructores privados y propiedades de solo escritura interna.
- [x] 3.3 Implementar `ApplicationDbContext : IdentityDbContext<Usuario, Rol, Guid>, IApplicationDbContext` (implementando `SaveChangesAsync` como unidad de trabajo) con `ApplyConfigurationsFromAssembly` y verificar que la prueba de arquitectura confirma que ningÃºn tipo de EF aparece en las firmas de presentaciÃ³n.
- [x] 3.4 Crear las configuraciones Fluent API (`UsuarioConfiguration`, `RolConfiguration`) con tabla `AspNetUsers`, longitudes mÃ¡ximas, conversiÃ³n de `TipoActor` a `int` e Ã­ndice Ãºnico filtrado sobre `IdentificadorFederado`; verificar que el modelo generado (`dotnet ef dbcontext script` o snapshot) contiene el Ã­ndice.
- [x] 3.5 AÃ±adir `AddInfrastructure(IConfiguration)` en `Backend.Infrastructure/DependencyInjection.cs` con `UseSqlServer(..., EnableRetryOnFailure)` y registro de servicios, verificando que `dotnet build` pasa y que la cadena de conexiÃ³n ausente produce un error de arranque explÃ­cito.
- [x] 3.6 Generar la migraciÃ³n inicial `InitialCreate` con `dotnet ef migrations add` y verificar con `dotnet ef migrations script` que el script SQL crea las tablas de Identity, el Ã­ndice Ãºnico filtrado sobre `IdentificadorFederado` y las columnas de auditorÃ­a; aplicarla con `dotnet ef database update` contra la instancia local.
- [x] 3.7 Implementar `InicializadorIdentity` idempotente que crea los roles base y el administrador inicial desde `Seed:AdminEmail` / `Seed:AdminPassword`, verificando con una prueba de integraciÃ³n que ejecutarlo dos veces no duplica usuarios ni roles.
- [x] 3.8 Configurar en `Program.cs` la aplicaciÃ³n de migraciones automÃ¡tica solo en `Development`, verificando con una prueba de integraciÃ³n (Testcontainers SQL Server 2022) que el esquema se aplica y que un entorno no-Development no migra automÃ¡ticamente.
- [x] 3.9 Documentar en `docs/persistencia.md` el esquema de identidad, el comando de migraciÃ³n y la polÃ­tica de seed, verificando que los comandos documentados ejecutan sin error contra el contenedor.

## 4. AutenticaciÃ³n local con JWT propio

- [x] 4.1 Implementar `JwtTokenService` en `Backend.Infrastructure/Identity` con firma HS256, claims `sub`, `jti`, `email`, `name`, `roles`, `iss`, `aud`, `iat`, `exp` y expiraciÃ³n configurable (`Jwt:ExpiraMinutos`, por defecto 60); verificar con pruebas unitarias que el token decodificado contiene exactamente esos claims y la expiraciÃ³n esperada.
- [x] 4.2 Implementar `IdentityService` que encapsula `UserManager`/`RoleManager` (`VerificarCredencialesAsync`, `ObtenerRolesAsync`, `BuscarPorCorreoAsync`, `BuscarPorIdAsync`, `ActualizarUltimoAccesoAsync`) sin exponer tipos de Identity, y verificar con pruebas unitarias por sustituciÃ³n que el caso de uso solo ve `CuentaUsuario`/`PerfilUsuario`.
- [x] 4.3 Configurar el bloqueo por intentos fallidos (`LockoutOptions`) y verificar con una prueba de integraciÃ³n que superar el umbral responde `423 Locked` con `type` de cuenta bloqueada y que un login exitoso reinicia el contador.
- [x] 4.4 AÃ±adir la validaciÃ³n de la polÃ­tica de contraseÃ±as y el requisito de correo Ãºnico normalizado, verificando con una prueba de integraciÃ³n que un alta de usuario con correo repetido es rechazada por el almacÃ©n de identidad.
- [x] 4.5 Configurar la clave de firma fuera del repositorio (`Jwt:Key` por User Secrets en desarrollo y variable de entorno en contenedor) y verificar que `appsettings.json` versionado no contiene ninguna clave ni cadena de conexiÃ³n real.
- [x] 4.6 Implementar `POST /api/auth/login` en `AuthController` traduciendo `ResultadoAutenticacion` a `200 OK` con token o al problema RFC 7807 correspondiente, verificando con pruebas de integraciÃ³n los escenarios de credenciales vÃ¡lidas, incorrectas y correo no registrado.
- [x] 4.7 Implementar `GET /api/auth/me` que resuelve el portador del token y devuelve nombre, correo, tipo de actor y roles, verificando con pruebas de integraciÃ³n que responde `200` con token vigente y `401` sin token.
- [x] 4.8 Aplicar `[Authorize(Roles = "Administrativo")]` a un endpoint administrativo de verificaciÃ³n y comprobar con pruebas de integraciÃ³n las respuestas `200`, `403` y `401` segÃºn el rol y la presencia del token.

## 5. Contrato de errores RFC 7807

- [x] 5.1 Registrar `AddProblemDetails()` con `CustomizeProblemDetails` que aÃ±ade `traceId` e `instance`, verificando con una prueba de integraciÃ³n que la respuesta incluye los miembros `type`, `title`, `status`, `detail`, `instance` y la extensiÃ³n `traceId`.
- [x] 5.2 Implementar el catÃ¡logo de `type` estables (`credenciales-invalidas`, `token-ausente`, `token-invalido`, `sesion-expirada`, `permisos-insuficientes`, `cuenta-bloqueada`, `solicitud-invalida`, `proveedor-identidad-no-disponible`, `error-interno`) con `Content-Type: application/problem+json`, verificando con pruebas que cada condiciÃ³n produce el `status` y el `type` documentados en `design.md`.
- [x] 5.3 Implementar `ProblemDetailsExceptionHandler` (`IExceptionHandler`) para excepciones no controladas y verificar con una prueba de integraciÃ³n que responde `500` sin trazas de pila ni nombres de clases internas.
- [x] 5.4 Configurar la validaciÃ³n de DTOs de autenticaciÃ³n para que un cuerpo invÃ¡lido responda `400` con `errors` por campo y sin ejecutar la verificaciÃ³n de credenciales; verificar con pruebas de integraciÃ³n de cuerpo incompleto y correo con formato invÃ¡lido.
- [x] 5.5 Implementar los `JwtBearerEvents` (`OnChallenge`, `OnForbidden`, `OnAuthenticationFailed`) que emiten los problemas de autenticaciÃ³n y autorizaciÃ³n con `WWW-Authenticate` cuando corresponde, verificando con pruebas de integraciÃ³n que el `401` por ausencia de token incluye el encabezado y el `401` de login no.
- [x] 5.6 Comprobar que los mensajes no permiten enumerar usuarios: verificar con una prueba de integraciÃ³n que el cuerpo del `401` es idÃ©ntico para correo inexistente y contraseÃ±a incorrecta.

## 6. AutenticaciÃ³n hÃ­brida con Entra ID

- [x] 6.1 Registrar `AddJwtBearer("LocalJwt")` con `iss`, `aud`, clave simÃ©trica, `ValidateLifetime` y `ClockSkew` de 30 segundos, verificando con pruebas de integraciÃ³n que un token alterado o de otro emisor recibe `401` con `type` de token invÃ¡lido.
- [x] 6.2 Registrar `AddJwtBearer("EntraId")` con `Authority` del inquilino, `Audience` del cliente, validaciÃ³n de emisor y `RoleClaimType="roles"`; verificar con pruebas de integraciÃ³n (metadatos simulados) que un token con firma vÃ¡lida del inquilino accede y uno expirado o de otra audiencia recibe `401`.
- [x] 6.3 Implementar el `PolicyScheme` de resoluciÃ³n (`ForwardDefaultSelector` por `iss`) y verificar con pruebas que un token local se valida solo con la clave local, un token federado solo con las claves del inquilino y un `iss` falsificado se rechaza por firma.
- [x] 6.4 Implementar `EntraIdProfileResolver` que resuelve o crea el perfil local por `oid` con rol `Docente` o `Estudiante` derivado de app roles, verificando con pruebas de integraciÃ³n el primer acceso (alta del perfil) y el segundo (reutilizaciÃ³n sin duplicar).
- [x] 6.5 Verificar que un token federado vÃ¡lido sin habilitaciÃ³n acadÃ©mica responde `403` con `type` de permisos insuficientes y que no se crea ni modifica ningÃºn rol administrativo.
- [x] 6.6 Implementar el mapeo del fallo de metadatos/JWKS a `503` con `type` de proveedor no disponible, verificando con una prueba de integraciÃ³n que inyecta un `IConfigurationManager` con error de red y que el login local sigue respondiendo `200` en la misma ejecuciÃ³n.
- [x] 6.7 Unificar `NameClaimType` y `RoleClaimType` en ambos esquemas y verificar con una prueba de integraciÃ³n que `[Authorize(Roles=...)]` autoriza igual a un docente federado y a un administrador local con los roles correctos.
- [x] 6.8 AÃ±adir el registro de auditorÃ­a de intentos federados (usuario interno, rol aplicado, resultado y marca de tiempo, sin token ni datos sensibles) y verificar con una prueba que el log de acceso exitoso y el de rechazo contienen los campos esperados.
- [x] 6.9 Documentar en `docs/autenticacion.md` las variables `Jwt:*`, `EntraId:TenantId`, `EntraId:ClientId` y el procedimiento de activaciÃ³n federada, verificando que un lector puede configurar el esquema solo con ese documento.

## 7. SPA base en Angular 20

- [x] 7.1 Generar el workspace Angular 20 en `src/frontend` con `bootstrapApplication`, `app.config.ts` (`provideRouter`, `provideHttpClient(withInterceptors)`, `provideZonelessChangeDetection`) y sin `NgModule`; verificar con `npm run build` y con una bÃºsqueda de `@NgModule` sin resultados.
- [x] 7.2 Crear la estructura `core/`, `shared/` y `features/` con rutas lazy y una pÃ¡gina protegida mÃ­nima, verificando que `npm run build` genera el paquete estÃ¡tico y que la ruta protegida existe en el bundle.
- [x] 7.3 Implementar `AuthService` y `SesionStore` con Signals (login, logout, `estaAutenticado`, roles) y verificar con pruebas de TestBed que el estado cambia tras login y se limpia tras logout.
- [x] 7.4 Implementar `jwt.interceptor.ts` que adjunta `Authorization: Bearer` solo a las solicitudes dirigidas a la API, verificando con pruebas unitarias que se aÃ±ade el encabezado, que no se aÃ±ade sin sesiÃ³n y que una recarga de la aplicaciÃ³n conserva la sesiÃ³n vigente.
- [x] 7.5 Implementar `problem-details.interceptor.ts` que traduce el contrato RFC 7807, redirige al login ante `sesion-expirada`, muestra el `detail` ante `403` y aplica un mensaje genÃ©rico ante respuestas que no cumplen el formato; verificar con pruebas unitarias de los tres casos.
- [x] 7.6 Implementar `auth.guard.ts` y `role.guard.ts` funcionales y verificar con pruebas unitarias que una ruta protegida sin sesiÃ³n redirige al acceso y que un rol insuficiente no renderiza el contenido.
- [x] 7.7 Implementar la pantalla de login con el formulario reactivo y el manejo de errores del interceptor, verificando con pruebas de componente que un envÃ­o vÃ¡lido autentica y que uno invÃ¡lido muestra el mensaje del problema.
- [x] 7.8 Configurar `proxy.conf.json` y los entornos (`environment.development.ts`, `environment.ts`) con la URL base de la API, verificando con `npm start` que una llamada a `/api/auth/me` llega al backend sin errores de CORS.
- [x] 7.9 Configurar CORS en la API para el origen de la SPA y verificar con una prueba de integraciÃ³n que un origen permitido recibe los encabezados y uno no permitido es rechazado.

## 8. Entorno de ejecuciÃ³n y automatizaciÃ³n

- [x] 8.1 Crear `docker-compose.yml` con `mssql` (`2022-latest`, volumen, `healthcheck` con `sqlcmd`) y `api` (`depends_on: service_healthy`), verificando con `docker compose up -d` que ambos servicios quedan `healthy`.
- [x] 8.2 Crear el `Dockerfile` multi-stage de la API con usuario no root, verificando con `docker build` y con una solicitud a `/health` desde el contenedor.
- [x] 8.3 Exponer `/health` con `AddHealthChecks().AddDbContextCheck<ApplicationDbContext>()`, verificando que responde `200` con la base disponible y `503` al detener el contenedor de SQL Server.
- [x] 8.4 AÃ±adir `tests/Backend.UnitTests` al pipeline de compilaciÃ³n y verificar con `dotnet test src/Backend.sln` que unitarias y arquitectura se ejecutan juntas.
- [x] 8.5 Configurar el pipeline de CI (`.github/workflows/ci.yml`) con restore, build Release, pruebas de arquitectura, unitarias e integraciÃ³n con Testcontainers, verificando que una ejecuciÃ³n verde produce los tres reportes y que una violaciÃ³n de capas rompe el pipeline.
- [x] 8.6 Documentar en `docs/infraestructura-base.md` el arranque local completo (compose, migraciones, seed, SPA) y verificar siguiendo el documento en una copia limpia del repositorio.

## 9. VerificaciÃ³n integral de la etapa

- [x] 9.1 Ejecutar la verificaciÃ³n de humo completa (compose arriba â†’ login local de administrador â†’ `GET /api/auth/me` â†’ endpoint administrativo `200` â†’ cierre de sesiÃ³n â†’ `401`) y registrar el resultado en el reporte de la etapa.
- [x] 9.2 Ejecutar la suite completa (`dotnet test src/Backend.sln`, `npm test`, `npm run build`) y confirmar que no hay pruebas fallidas ni advertencias de compilaciÃ³n.
- [x] 9.3 Revisar la matriz de trazabilidad de las cuatro capacidades (arquitectura, autenticaciÃ³n local, federaciÃ³n Entra ID, RFC 7807) contra escenarios y pruebas, confirmando que cada escenario tiene al menos una prueba automatizada o una verificaciÃ³n manual documentada.

## Workflow follow-up

- Ejecutar `openspec validate 01-core-infrastructure --strict` y corregir cualquier advertencia antes de solicitar revisiÃ³n.
- Archivar el cambio con el flujo de OpenSpec una vez que la revisiÃ³n y el commit atÃ³mico estÃ©n aprobados.
- Verificar el resultado archivado en `openspec/specs/` antes de iniciar el cambio de la siguiente capacidad.
