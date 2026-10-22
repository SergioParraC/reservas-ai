# Design

## Context

Ver `proposal.md` para la motivación. El repositorio solo contiene documentación (`docs/PLAN_INGENIERIA.md`, `README.md`); no hay solución, código, migraciones ni pipeline. Las decisiones de este documento se apoyan en el plan de ingeniería (secciones 2 y 3) y en las capacidades descritas en `specs/`.

Restricciones que condicionan el diseño:

- .NET 10 y Angular 20 son obligatorios; la capa `Backend.Domain` debe quedar en C# puro para que las reglas de disponibilidad y recurrencia (Etapa 2) no dependan de EF Core ni de ASP.NET Core.
- El esquema de identidad es híbrido por decisión de negocio: Entra ID para cualquier miembro de la comunidad institucional —con independencia de su rol— e Identity local para el personal administrativo. Ambos esquemas conceden exactamente los mismos tres roles: `Administrativo`, `Docente` y `Estudiante`. El login local debe sobrevivir a una caída del inquilino (riesgo R2 del plan).
- SQL Server 2022 es la base transaccional; el proyecto apunta a un monolito modular, no a microservicios.
- El contrato de error debe ser RFC 7807 desde el primer endpoint, porque toda la SPA y las etapas siguientes consumirán ese formato.
- El alcance se limita a la fundación técnica y la autenticación: no hay aún entidades de espacio, reserva, QR ni tareas en segundo plano.

## Goals / Non-Goals

**Goals:**

- Una solución compilable y verificable que materialice la regla de dependencias de Clean Architecture a nivel de proyecto y de prueba automatizada.
- Un único punto de verdad para la persistencia de identidad (`ApplicationDbContext`) con migraciones versionadas y arranque reproducible en contenedor.
- Una identidad de usuario institucional (`Usuario`) capaz de representar los tres tipos de actor y de coexistir con el proveedor federado, con exactamente tres roles.
- Un pipeline de autenticación que resuelva el token correcto según su origen, normalice las notificaciones (rol, nombre, identificador) y falle con errores RFC 7807 diferenciados.
- Una sesión local renovable sin reautenticación manual: access token de 60 minutos con expiración absoluta y refresh tokens de un solo uso con rotación y detección de reutilización.
- Una SPA base con Standalone Components, sesión reactiva con Signals y manejo centralizado de tokens, renovación y errores.
- Una base de pruebas (arquitectura, unitarias, integración con SQL Server efímero) que las etapas posteriores puedan extender sin rediseño.

**Non-Goals:**

- CRUD de espacios, puestos, políticas de uso, generación de QR y motor de disponibilidad.
- MFA, recuperación de contraseña, autogestión de perfil y revocación administrativa masiva de sesiones de otros usuarios.
- Refresh tokens para identidades federadas: esas sesiones las renueva MSAL contra Entra ID y la API no emite tokens propios para ellas.
- Autorización granular por recurso (ownership, políticas por área); este diseño solo cubre autorización por rol.
- Almacenamiento de archivos, correo SMTP, tareas en segundo plano y tablero analítico.
- Endurecimiento de producción definitivo (Nginx, TLS, secretos administrados): aquí solo se deja el contenedor de SQL Server y la configuración por entorno.

## Decisions

### 1. Estructura física de la solución

```
src/
├── Backend.sln
├── Backend.Domain/                  # C# puro, 0 ProjectReference
│   ├── Common/                      # EntidadBase, ValueObject
│   ├── Entities/                    # Usuario (referencia conceptual), enums de actor
│   ├── Enums/                       # TipoActor, EstadoUsuario
│   └── Exceptions/                  # DomainException (base para etapas siguientes)
├── Backend.Application/             # → Domain
│   ├── Common/                      # ResultadoAutenticacion, ErrorAutenticacion
│   ├── Interfaces/                  # IApplicationDbContext, IIdentityService,
│   │                                # ITokenService, IRefreshTokenStore,
│   │                                # IEntraIdProfileResolver, IRelojSistema, IUsuarioActual
│   └── Autenticacion/               # LoginLocalHandler, RenovarSesionHandler,
│                                    # CerrarSesionHandler, ObtenerPerfilHandler, DTOs
├── Backend.Infrastructure/          # → Application, Domain
│   ├── Persistence/                 # ApplicationDbContext, Configurations, Migrations
│   ├── Identity/                    # IdentityService, JwtTokenService, RefreshTokenService,
│   │                                # EntraIdProfileResolver, InicializadorIdentity (seed)
│   └── DependencyInjection.cs       # AddInfrastructure(IConfiguration)
└── Backend.Presentation.Api/        # → Application, Infrastructure
    ├── Controllers/                 # AuthController
    ├── Middlewares/                 # ProblemDetailsExceptionHandler, AutenticacionHibridaSetup
    ├── Contracts/                   # LoginRequest, TokenResponse, UsuarioPerfilResponse
    ├── Program.cs                   # IoC + pipeline
    └── appsettings.{Development,Production}.json

tests/
├── Backend.ArchitectureTests/       # NetArchTest.Rules
├── Backend.UnitTests/               # xUnit + Moq + FluentAssertions
└── Backend.IntegrationTests/        # Testcontainers (SQL Server 2022) + WebApplicationFactory

src/frontend/                        # Workspace Angular 20; fuentes en src/frontend/src/app
```

**Por qué:** la propuesta de plan ya fija los cuatro proyectos y sus nombres; esta estructura solo agrega carpetas internas y ubica la SPA en `src/frontend` para no mezclar herramientas de build. Una solución única en `src/Backend.sln` permite que la prueba de arquitectura inspeccione todas las referencias desde un solo lugar.

**Alternativa descartada:** separar `Backend.Identity` como quinto proyecto .NET. Añade una frontera que todavía no tiene consumidores distintos y obliga a una doble configuración de EF Core; se puede extraer más adelante sin cambiar las capacidades.

### 2. Puertos y frontera con EF Core

- `Backend.Domain` no referencia ningún paquete.
- `Backend.Application` referencia solo `Microsoft.EntityFrameworkCore` (paquete de abstracciones) para declarar `IApplicationDbContext` con `DbSet<Usuario>`, `DbSet<Rol>` y `SaveChangesAsync(CancellationToken)`, tal como exige el plan.
- Ninguna entidad de Identity (`UserManager`, `SignInManager`, `IdentityResult`, `Usuario` de EF) cruza hacia `Presentation`: los controladores solo ven DTOs de `Application/Contracts`.
- `IIdentityService` encapsula `UserManager`/`RoleManager`; `ITokenService` encapsula la firma del JWT; `IRefreshTokenStore` encapsula la persistencia y rotación de refresh tokens; `IEntraIdProfileResolver` encapsula la traducción de notificaciones federadas a un perfil y rol efectivos. Las cuatro viven en Application y se implementan en Infrastructure.
- Los controladores no conocen ningún puerto de persistencia: reciben un `ResultadoAutenticacion` o un `ResultadoOperacion` ya resuelto por el caso de uso.

**Por qué:** `IApplicationDbContext` mantiene el patrón del plan y evita una capa de repositorios ceremonial para un CRUD de identidad; a cambio se acepta que Application conozca `DbSet`, riesgo controlado porque la prueba de arquitectura prohíbe que ese tipo atraviese hacia presentación o dominio.

**Alternativa descartada:** repositorios por agregado (`IUsuarioRepository`). Se descarta por ahora: Identity ya abstrae el acceso a usuarios y añadir repositorios encima duplica responsabilidades sin beneficio medible en esta etapa.

### 3. Modelo de identidad

```csharp
// Backend.Infrastructure/Persistence/Identity/Usuario.cs
public class Usuario : IdentityUser<Guid>
{
    public string NombreCompleto { get; private set; }
    public TipoActor TipoActor { get; private set; }        // Administrativo | Docente | Estudiante
    public string? IdentificadorFederado { get; private set; } // claim "oid" de Entra ID, único cuando existe
    public bool Activo { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public DateTimeOffset? UltimoAccesoEn { get; private set; }
}

public class Rol : IdentityRole<Guid> { }
```

Decisiones asociadas:

- `Guid` como clave (no `string`): evita colisiones al sembrar datos y alinea el identificador con el `sub` del JWT local.
- `UserName` = correo institucional; índice único sobre `NormalizedEmail` (Identity ya lo crea) e índice único filtrado sobre `IdentificadorFederado` (`WHERE IdentificadorFederado IS NOT NULL`), porque SQL Server trata varios `NULL` como distintos pero queremos exclusividad real cuando hay valor.
- `TipoActor` se persiste como `int` con conversión explícita para que el esquema no dependa del orden de los nombres del enum.
- `Usuario` se ubica en `Backend.Infrastructure/Persistence/Identity` (no en Domain) porque hereda de `IdentityUser<Guid>`, que es una dependencia de framework; el dominio funcional de la Etapa 2 seguirá siendo puro. Si más adelante el dominio necesita usuarios, se modelará un agregado propio y se mapeará desde esta tabla.
- Exactamente tres roles: `Administrativo`, `Docente` y `Estudiante`, sembrados por `InicializadorIdentity` y sin alta dinámica. El rol `Administrativo` corresponde al actor "Administrativos" del plan de ingeniería (parametriza el campus, aprueba auditorios, configura franjas y gestiona sanciones). No se contempla un cuarto rol: cualquier necesidad futura se resuelve como permiso dentro de un rol existente, para no romper `[Authorize(Roles = ...)]` ni la matriz de autorización ya probada.
- `TipoActor` y los roles son el mismo conjunto de valores con propósitos distintos: `TipoActor` es un atributo descriptivo del perfil institucional, mientras que la autorización se resuelve exclusivamente por `IdentityRole`. La prueba de integración verifica que ambos coincidan para cada usuario.
- No se agrega `RowVersion`: la concurrencia optimista es un requisito de las reservas (Etapa 2), no de la autenticación; `ConcurrencyStamp` heredado cubre los updates de Identity.

**Alternativa descartada:** tabla `Usuario` propia con hash de contraseña manual. Se descarta: reimplementar hashing, bloqueo y normalización es riesgo de seguridad sin contrapartida.

### 4. `ApplicationDbContext` y persistencia

```csharp
public class ApplicationDbContext
    : IdentityDbContext<Usuario, Rol, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        builder.Entity<Usuario>(e => { /* tabla, longitudes, índices, conversiones */ });
    }
}
```

- Un único contexto y un único conjunto de migraciones en `Backend.Infrastructure/Persistence/Migrations`, generadas con `dotnet ef migrations add`.
- `UseSqlServer(cadena, o => o.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), null))` para tolerar reinicios del contenedor.
- Configuración Fluent API en clases `IEntityTypeConfiguration<T>`; sin anotaciones en las entidades.
- Segunda tabla del contexto: `TokenRenovacion`, con `Id` (Guid), `UsuarioId` (FK a `AspNetUsers`, borrado en cascada), `HashToken` (SHA-256 del valor entregado al cliente, nunca el valor), `FamiliaId`, `CreadoEn`, `ExpiraEn`, `RevocadoEn`, `ReemplazadoPorHash` y `OrigenSesion` (`Local`). Índice único sobre `HashToken` e índice no único sobre `(FamiliaId, UsuarioId)`. Un índice filtrado `WHERE RevocadoEn IS NULL` acota la búsqueda de sesiones activas.
- `Usuario.Id` se conserva como `Guid` y la relación `TokenRenovacion → Usuario` impide refresh tokens huérfanos cuando se elimina una cuenta.
- `Migrate` automático **solo** en `Development`; en otros entornos se aplica por paso explícito de despliegue, para no sorprender con cambios de esquema.
- Seed del administrador inicial mediante `InicializadorIdentity` idempotente al arrancar, leyendo correo y contraseña de `Seed:AdminEmail` / `Seed:AdminPassword` (variables de entorno o User Secrets). No se usa `HasData` porque exigiría un hash de contraseña versionado en el repositorio.
- Pruebas de integración: Testcontainers levanta SQL Server 2022, aplica migraciones y descarta el contenedor; sin `EnsureCreated` (así se valida la migración real).

**Por qué un solo contexto:** las operaciones de autenticación y las futuras reservas comparten transacción y base; dos contextos obligarían a coordinación distribuida artificial dentro del mismo monolito modular.

### 5. Autenticación híbrida: dos esquemas, una identidad normalizada

```
Authorization: Bearer <token>
              │
              ▼
   PolicyScheme "Hibrida"  ── lee "iss" sin validar (solo enrutamiento) ──┐
              │                                                          │
   issuer == Entra tenant  ──sí──►  JwtBearer "EntraId"  (JWKS, aud, iss)│
              │ no                                                       │
              ▼                                                          │
   JwtBearer "LocalJwt"  (clave simétrica HMAC, iss/aud propios) ◄────────┘
              │
              ▼
   ClaimsPrincipal normalizado (sub, name, email, role)
              │
              ▼
   IUsuarioActual → usuario interno + roles
```

- `AddAuthentication(o => { o.DefaultScheme = "Hibrida"; o.DefaultChallengeScheme = "Hibrida"; })` con `AddPolicyScheme("Hibrida", ..., o => o.ForwardDefaultSelector = ctx => EsEntraId(ctx) ? "EntraId" : "LocalJwt")`.
- `EsEntraId` inspecciona el claim `iss` del token sin validarlo. Es seguro porque la decisión solo elige el handler; **la firma la valida siempre el handler elegido**. Un token con `iss` falsificado cae en el esquema equivocado y es rechazado por firma.
- `LocalJwt`: `SymmetricSecurityKey` desde `Jwt:Key` (mínimo 32 bytes, inyectada por entorno), `ValidateIssuer/ValidateAudience/ValidateLifetime/ValidateIssuerSigningKey = true`, `ClockSkew = 30s`.
- `EntraId` (**single-tenant**): `Authority = https://login.microsoftonline.com/{EntraId:TenantId}/v2.0`, `Audience = EntraId:ClientId`, `ValidateIssuer = true` con `ValidIssuer` fijado al inquilino, y `IssuerValidator` adicional que compara el claim `tid` del token contra `EntraId:TenantId`. Un token de otro inquilino se rechaza aunque su firma sea válida. `TokenValidationParameters` declara `RoleClaimType = "roles"` y `NameClaimType = "name"`. No se usa el flujo interactivo (OIDC redirect): la SPA obtiene el token con MSAL y la API solo valida Bearer, lo que mantiene la API sin estado y desacopla el frontend.
- Normalización: ambos handlers configuran `RoleClaimType = "roles"` y `NameClaimType = "name"`, de modo que `[Authorize(Roles = "Administrativo")]` funciona igual con un token local y con uno federado.
- Endpoints: `POST /api/auth/login` es anónimo y solo acepta credenciales locales; `POST /api/auth/refresh` y `POST /api/auth/logout` operan solo sobre refresh tokens locales; `GET /api/auth/me` acepta cualquiera de los dos esquemas; los endpoints administrativos usan `[Authorize(Roles = "Administrativo")]`.

**Resolución del rol federado (opción B: perfil preexistente).** El acceso federado nunca crea cuentas. `IEntraIdProfileResolver` recibe la identidad ya validada y:

1. Busca el perfil local por `IdentificadorFederado` (= `oid`). Si no existe, devuelve `PerfilInexistente`.
2. Si el perfil está `Activo = false`, devuelve `PerfilInactivo`.
3. Calcula los roles efectivos como **intersección** entre los roles del perfil local y los app roles del claim `roles`. Si la intersección es vacía, devuelve `SinRolHabilitado`.
4. Si hay intersección, actualiza `UltimoAccesoEn` y devuelve el perfil con los roles efectivos.

Ambos fallos (`PerfilInexistente`, `PerfilInactivo`, `SinRolHabilitado`) responden `403 Forbidden` con `type` `permisos-insuficientes` y un `detail` que invita a contactar al área administrativa, sin distinguir en la respuesta si el perfil no existe o está deshabilitado; la distinción queda solo en el log de auditoría.

**Por qué la intersección y no "el rol local manda":** si el perfil local fuera autoritativo por sí solo, retirar a una persona del app role en el inquilino no le quitaría el acceso hasta que alguien recordara desactivar su perfil local. La intersección hace que cualquiera de las dos revocaciones —institucional o local— sea efectiva. **Por qué no "el token manda":** permitiría que un cambio de app role en el inquilino otorgara acceso administrativo sin que exista un perfil habilitado en la plataforma, que es justo lo que la opción B prohíbe.

**Alternativa futura (sin cambio de contrato):** si la institución solo expone grupos de Microsoft Graph, se registra una implementación de `IEntraIdProfileResolver` que consulte Graph y devuelva los mismos tres roles. Ni la autorización, ni los controladores, ni las pruebas de autorización cambian; solo la implementación registrada en el contenedor de dependencias.

**Por qué `PolicyScheme` y no un único `JwtBearer` con varios emisores:** un solo handler obligaría a compartir claves y reglas, impide mapear roles distinto por origen y devuelve siempre `401` genérico, incluso cuando el problema real es que no se alcanzan las claves del inquilino (el `503` exigido por la especificación). `PolicyScheme` permite además fallar de forma diferenciada sin tocar los controladores.

**Alternativa descartada:** `Cookie` + `OpenIdConnect` con redirect desde la API. Rompe el modelo SPA + API REST stateless y complica CORS; MSAL en el cliente ya resuelve el flujo.

### 6. Emisión y renovación de tokens locales

#### 6.1 Access token (expiración absoluta)

`JwtTokenService` (Infrastructure, implementa `ITokenService`) emite un JWT HS256 con:

| Claim | Valor |
| :--- | :--- |
| `sub` | `Usuario.Id` (Guid) |
| `jti` | Guid nuevo por emisión |
| `email` | correo institucional |
| `name` | `NombreCompleto` |
| `roles` | roles de Identity del usuario (subconjunto de los tres) |
| `iss` / `aud` | `Jwt:Issuer` / `Jwt:Audience` |
| `iat` / `exp` | emisión y expiración (`Jwt:ExpiraMinutos`, por defecto 60) |

- **Expiración absoluta, no por inactividad.** No se rastrea actividad por usuario en el servidor: no hay tabla de latidos, ni renovación deslizante del access token, ni dependencia de una llamada previa para mantener viva la sesión. Un token emitido a las 09:00 expira a las 10:00 aunque el usuario haya estado activo hasta 09:59. Esto elimina estado por sesión en la API y hace el comportamiento predecible y verificable.
- La vigencia es configurable, pero el valor por defecto (60) es el que documenta la matriz de pruebas.
- La clave de firma nunca se versiona; `dotnet user-secrets` en desarrollo y variables de entorno en contenedor.
- El token no incluye datos sensibles (ni hash, ni estado de bloqueo) y no se registra completo en logs.

#### 6.2 Refresh token (un solo uso, con rotación)

- Valor opaco de 256 bits generado con `RandomNumberGenerator`; **solo se persiste su SHA-256** en `TokenRenovacion.HashToken`. El valor en claro se devuelve una única vez al cliente y nunca se puede recuperar.
- `FamiliaId` identifica la cadena completa de una sesión. Todos los tokens rotados desde un login comparten familia.
- **Vida de la familia:** 7 días por defecto (`Jwt:RenovacionDias`). Cada token rotado hereda el `ExpiraEn` de la familia, de modo que la sesión tiene un techo absoluto de 7 días y no se extiende indefinidamente con cada renovación. Pasado ese techo, el usuario vuelve a autenticarse.
- **Rotación:** cada `POST /api/auth/refresh` marca el token presentado como reemplazado (`RevocadoEn`, `ReemplazadoPorHash`) y emite un par nuevo (access + refresh) en la misma transacción. Un refresh token nunca se puede usar dos veces.
- **Detección de reutilización:** si llega un token cuyo `RevocadoEn` no es nulo y que tiene `ReemplazadoPorHash`, se interpreta como robo o duplicación: se revoca **toda la familia** (`RevocadoEn` en todos sus tokens) y se responde `401` con `type` `sesion-revocada`. El usuario legítimo pierde la sesión, que es el comportamiento correcto ante una posible compromisión.
- **Cierre de sesión:** `POST /api/auth/logout` revoca la familia del refresh token presentado, y requiere un access token válido del mismo usuario para no permitir que un tercero cierre sesiones ajenas.
- **Limpieza:** los tokens con `ExpiraEn` vencido se eliminan por una tarea de mantenimiento simple; no se necesita `BackgroundService` en esta etapa, basta con purgar en el propio `refresh` y en el arranque.
- `IRefreshTokenStore` expone `EmitirAsync`, `RotarAsync` y `RevocarFamiliaAsync`; el caso de uso `RenovarSesionHandler` decide el resultado y el controlador solo lo traduce a HTTP.

#### 6.3 Alcance por esquema

Los refresh tokens son un mecanismo **exclusivo del esquema local**. Una identidad federada no recibe tokens propios: MSAL renueva el access token de Entra ID en el navegador y la API solo lo vuelve a validar. Mezclar ambos mecanismos obligaría a que la API custodie la relación con el inquilino y duplicaría la lógica de revocación.

**Alternativa descartada:** refresh tokens de larga vida sin rotación. Es el esquema más simple, pero un token filtrado da acceso indefinido sin dejar rastro; la rotación con detección de reutilización convierte ese mismo token filtrado en una señal detectable y en una revocación efectiva.

**Alternativa descartada:** sesiones con expiración deslizante por inactividad en el servidor. Requiere una tabla de actividad por sesión y una escritura en cada solicitud; el requisito del negocio es expiración absoluta, así que añadirlo sería coste sin beneficio.

### 7. Errores RFC 7807 en un solo punto

- `builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx => { /* traceId + instance */ })`.
- `ProblemDetailsExceptionHandler : IExceptionHandler` captura excepciones no controladas y responde `500` genérico.
- Los rechazos de autenticación **no** son excepciones: se materializan en `JwtBearerEvents` (`OnChallenge`, `OnForbidden`, `OnAuthenticationFailed`) y en un `ResultadoAutenticacion` fallido dentro del caso de uso. Así no se usa el control de flujo por excepciones.
- Catálogo de `type` estable (URIs bajo `https://api.reservas-espacios.edu/problems/`):

| `type` (slug) | HTTP | Origen |
| :--- | :---: | :--- |
| `credenciales-invalidas` | 401 | login local (usuario inexistente, contraseña errónea, cuenta inactiva) |
| `token-ausente` | 401 | endpoint protegido sin `Authorization` |
| `token-invalido` | 401 | firma, emisor o audiencia incorrectos |
| `sesion-expirada` | 401 | `exp` vencido |
| `token-renovacion-invalido` | 401 | refresh token desconocido, expirado o perteneciente a otra cuenta |
| `sesion-revocada` | 401 | reutilización de un refresh token ya rotado; se revoca la familia completa |
| `permisos-insuficientes` | 403 | rol no autorizado, perfil federado inexistente o perfil inactivo |
| `cuenta-bloqueada` | 423 | bloqueo de Identity por intentos fallidos |
| `solicitud-invalida` | 400 | DTO inválido, con extensión `errors` por campo |
| `proveedor-identidad-no-disponible` | 503 | no se alcanzan metadatos/JWKS de Entra ID |
| `error-interno` | 500 | excepción no controlada |

- Todo problema incluye la extensión `traceId` (`Activity.Current?.TraceId` o `HttpContext.TraceIdentifier`), que también se escribe en el log del evento.
- El mensaje de credenciales inválidas es idéntico para usuario inexistente y contraseña errónea; el `401` de login no incluye `WWW-Authenticate` (no es un challenge de esquema).

**Alternativa descartada:** un middleware propio que serialice `ProblemDetails` a mano. Duplicaría el comportamiento que .NET 10 ya expone y perdería la integración con `[ApiController]` y con los filtros de validación.

### 8. SPA Angular 20 con Standalone Components

```
src/frontend/src/app/
├── app.config.ts                  # provideRouter, provideHttpClient(withInterceptors), zoneless
├── app.routes.ts                  # rutas raíz + lazy loading de features
├── core/
│   ├── auth/                      # auth.service.ts (Signals), auth.models.ts, sesion.store.ts
│   ├── interceptors/              # jwt.interceptor.ts, renovacion.interceptor.ts,
│   │                              # problem-details.interceptor.ts
│   ├── guards/                    # auth.guard.ts (CanActivateFn), role.guard.ts
│   └── config/                    # api.config.ts (baseUrl por entorno)
├── shared/                        # shell, layout, componentes reutilizables
└── features/
    ├── auth/                      # login.page.ts, login-form.component.ts
    └── inicio/                    # página protegida mínima de verificación
```

- Sin `NgModule`: bootstrap con `bootstrapApplication`, rutas con `loadComponent`, guards e interceptores funcionales.
- Estado de sesión en un `SesionStore` basado en `signal`/`computed`; el par de tokens se persiste para sobrevivir a recargas y se descarta al cerrar sesión o al recibir `sesion-revocada`.
- `renovacion.interceptor.ts` reintenta una vez la solicitud que recibió `sesion-expirada`: llama a `/api/auth/refresh` con el refresh token vigente, guarda el par nuevo y repite la petición original. Usa una única renovación en vuelo (*single-flight*) compartida por todas las solicitudes concurrentes y una marca por solicitud para no reintentar dos veces. Si la renovación falla, descarta la sesión y redirige al acceso. Esto es necesario porque el access token dura 60 minutos y expirará durante una jornada de trabajo normal.
- Almacenamiento del par: `localStorage`, para que una recarga no obligue a reautenticarse. Es un compromiso consciente: queda expuesto a XSS, que se mitiga con la expiración absoluta del access token, la rotación con detección de reutilización y una política de contenido estricta al empaquetar la SPA en Nginx. La alternativa con cookies `HttpOnly` exigiría protección CSRF y un modelo de API con estado de sesión, fuera del alcance de esta etapa.
- Al cerrar sesión, la SPA llama primero a `/api/auth/logout` (revocación en servidor) y descarta el par localmente aunque la llamada falle: el cliente nunca debe conservar un token que decidió abandonar.
- `provideZonelessChangeDetection()` + `OnPush` en todos los componentes: menos ciclos de detección y Signals como única fuente de reactividad.
- `problem-details.interceptor.ts` es el único lugar que traduce el contrato RFC 7807 a mensajes de UI; contempla respuestas que no cumplen el formato (HTML de un proxy) con un mensaje genérico.
- Desarrollo contra la API con `proxy.conf.json` (`/api` → `http://localhost:5xxx`), evitando CORS en local; CORS se configura igualmente en la API para entornos separados.
- Entra ID en el cliente se integra con MSAL Angular en la etapa en que exista el registro de aplicación; en este cambio solo se deja el punto de extensión `EntraIdMsalService` documentado, ya que las credenciales del inquilino no forman parte del repositorio.

**Alternativa descartada:** NgModules y `BehaviorSubject` para la sesión. Se descartan porque el plan exige Standalone Components y Signals, y mezclar ambos estilos complica la migración futura.

### 9. Pruebas y verificación

| Nivel | Proyecto | Herramientas | Qué valida |
| :--- | :--- | :--- | :--- |
| Arquitectura | `Backend.ArchitectureTests` | NetArchTest.Rules | Regla de dependencias, dominio sin paquetes tecnológicos, controladores sin tipos de infraestructura |
| Unitaria | `Backend.UnitTests` | xUnit, Moq, FluentAssertions | Casos de uso de login, mapeo de resultados a códigos, construcción de claims |
| Integración | `Backend.IntegrationTests` | Testcontainers MSSQL, `WebApplicationFactory<Program>` | Migraciones reales, login end-to-end, `401/403/423` con `application/problem+json`, convivencia de esquemas, `503` simulado |
| Cliente | `ng test` (Karma + Jasmine) | Angular TestBed | `jwt.interceptor`, `problem-details.interceptor`, guards |

- `Program.cs` termina con `public partial class Program { }` para que `WebApplicationFactory` pueda usarlo.
- La prueba de arquitectura se ejecuta en el pipeline junto a las unitarias: es la puerta que impide que la Etapa 2 introduzca acoplamientos.
- Para el `503`, el test inyecta un `IConfigurationManager<OpenIdConnectConfiguration>` que lanza excepción de red, evitando depender de Internet.
- La prueba de integración usa una base por clase y limpia tablas de identidad; el contenedor se reutiliza dentro de la colección de pruebas para no pagar el arranque por cada test.

### 10. Orquestación local

- `docker-compose.yml` con `mssql` (imagen `mcr.microsoft.com/mssql/server:2022-latest`, volumen de datos, `healthcheck` con `sqlcmd`) y `api` (build del Dockerfile multi-stage, `depends_on: condition: service_healthy`). La SPA se ejecuta con `ng serve` en desarrollo; su empaquetado en Nginx pertenece a la Etapa 4.
- `/health` expuesto por la API (`AddHealthChecks().AddDbContextCheck<ApplicationDbContext>()`) para el `healthcheck` del contenedor de API.
- Configuración por entorno: ninguna cadena de conexión ni clave en `appsettings.json` versionado con valor real; se leen de variables de entorno en compose y de User Secrets en local.

## Risks / Trade-offs

- **Application referencia EF Core (`DbSet`) por `IApplicationDbContext`** → Mitigación: la prueba de arquitectura prohíbe tipos de EF/infraestructura en firmas de presentación y en el dominio; si en la Etapa 2 el acoplamiento estorba, se sustituye por puertos específicos sin cambiar las capacidades.
- **`Usuario` vive en Infrastructure por heredar `IdentityUser<Guid>`** → Mitigación: se documenta la frontera; el dominio funcional no lo consume y cualquier acceso pasa por DTOs.
- **Enrutamiento de esquema basado en un claim no validado (`iss`)** → Mitigación: la decisión solo selecciona el handler; la validación de firma es obligatoria en ambos y hay escenario de prueba con `iss` falsificado.
- **Sin refresh token, sesiones administrativas de 60 minutos** → Mitigación: expiración configurable y aviso de sesión expirada en la SPA; se evaluará refresh token cuando existan operaciones de larga duración en la Etapa 2.
- **Auto-provisión de perfiles federados en el primer acceso** → Riesgo de crear cuentas no habilitadas. Mitigación: solo se provisionan roles `Docente`/`Estudiante` derivados de app roles del inquilino; jamás `Administrador`, y el perfil nace `Activo` únicamente si el `oid` está en el directorio institucional (validado por el propio token).
- **Dependencia de red con Entra ID** → Mitigación: caché de configuración OIDC, `EnableRetryOnFailure` no aplica aquí, pero el login local no depende del inquilino y el `503` es explícito (riesgo R2).
- **Migración automática en Development puede enmascarar deriva de esquema en producción** → Mitigación: en otros entornos el despliegue aplica migraciones como paso explícito y verificable.
- **SQL Server en contenedor sobre Docker Desktop** → Consumo de memoria alto (~2 GB). Mitigación: límites en compose y alternativa documentada de usar una instancia local cambiando solo la cadena de conexión.

## Migration Plan

1. Crear la solución, proyectos, referencias y la prueba de arquitectura; ejecutar la compilación en limpio como línea base.
2. Incorporar el contexto de identidad y la migración inicial; generar la base con `dotnet ef database update` contra el contenedor.
3. Registrar los esquemas de autenticación y el contrato RFC 7807; verificar con pruebas de integración antes de tocar la SPA.
4. Construir la SPA base (servicios, interceptores, guards, login) y conectar con la API mediante proxy.
5. Sembrar el administrador inicial y ejecutar la verificación de humo: login local → `/api/auth/me` → endpoint administrativo → cierre de sesión.
6. Activar la validación federada cuando exista el registro de aplicación en el inquilino, sin cambiar código: solo configuración (`EntraId:TenantId`, `ClientId`).

**Rollback:** el cambio es aditivo (repositorio sin código previo). Si falla, se descarta la rama y se elimina el contenedor y el volumen de datos (`docker compose down -v`); no hay datos productivos que migrar.

## Open Questions

- ¿Los roles institucionales llegarán como app roles de Entra ID o como grupos? Se asume app roles (`roles` claim) porque evita consultas Graph en el camino crítico; si la institución solo expone grupos, se ajusta `EntraIdProfileResolver` sin afectar las capacidades especificadas.
- ¿La sesión administrativa debe expirar por inactividad además de por tiempo absoluto? Se difiere; afecta solo la duración configurada.
- ¿El registro de aplicación de Entra ID será multi-tenant o de un único inquilino? Se asume un único `TenantId`; un modelo multi-tenant requeriría elegir el inquilino por solicitud.
