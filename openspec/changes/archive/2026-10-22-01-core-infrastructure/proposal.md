# Proposal

## Why

El repositorio contiene hoy únicamente documentación de planificación (`docs/PLAN_INGENIERIA.md`): no existe solución compilable, base de datos versionada ni mecanismo de identidad. Sin una fundación técnica verificable, cada capacidad posterior (catálogo de espacios, motor de disponibilidad, bandejas de aprobación, control QR) tendería a inventar su propia plomería de persistencia, autorización y manejo de errores, rompiendo la regla de dependencias de Clean Architecture y duplicando el esquema de seguridad híbrida que el negocio exige.

Esta propuesta establece la base técnica de la Etapa 1 (Sprints 1 y 2): una solución .NET 10 en cuatro capas con la regla de dependencias impuesta en tiempo de compilación, una SPA base en Angular 20 con Standalone Components, el modelo de identidad `Usuario` sobre ASP.NET Core Identity con EF Core 10 / SQL Server 2022, y el middleware de autenticación híbrida (JWT local con renovación rotativa para el personal administrativo + tokens Bearer de Microsoft Entra ID para cualquier miembro de la comunidad institucional que tenga perfil habilitado, con independencia de su rol) con errores normalizados bajo RFC 7807. Es el requisito habilitante de todo el resto del alcance.

## What Changes

- Crear la solución backend en .NET 10 con cuatro proyectos: `Backend.Domain`, `Backend.Application`, `Backend.Infrastructure` y `Backend.Presentation.Api`, con referencias unidireccionales hacia el núcleo y una prueba de arquitectura que falle ante cualquier violación de la regla de dependencias.
- Incorporar `Backend.Domain` como núcleo C# puro: sin dependencias de EF Core, ASP.NET Core, Identity ni paquetes de terceros.
- Definir los puertos en `Backend.Application` (`IApplicationDbContext`, `IIdentityService`, `ITokenService`, `IRefreshTokenStore`, `IEntraIdProfileResolver`, contratos de autenticación) y los DTOs de transporte de autenticación, sin exponer entidades del ORM en la capa de presentación.
- Implementar en `Backend.Infrastructure` el `ApplicationDbContext` derivado de `IdentityDbContext<Usuario, Rol, Guid>` (EF Core 10.x sobre SQL Server 2022), la configuración Fluent API, la migración inicial y los adaptadores de Identity, emisión y rotación de tokens locales, y validación de tokens de Entra ID.
- Modelar `Usuario : IdentityUser<Guid>` con los atributos institucionales mínimos requeridos por los actores (nombre completo, tipo de actor, identidad federada opcional) y exactamente tres roles: `Administrativo`, `Docente` y `Estudiante`.
- Configurar el esquema de autenticación híbrida en `Backend.Presentation.Api`: política de esquemas múltiples que resuelve JWT local para el personal administrativo y Bearer de Entra ID de un único inquilino (OIDC/JWKS con validación explícita de `tid`) para cualquier rol; el acceso federado exige un perfil creado previamente en la plataforma y los roles efectivos resultan de la intersección entre el perfil local y los app roles del token.
- Añadir la renovación de sesión local: access token de 60 minutos con expiración absoluta (sin rastreo de inactividad en servidor) y refresh tokens de un solo uso con rotación, detección de reutilización y revocación de la cadena completa; `POST /api/auth/refresh` y `POST /api/auth/logout`. Los usuarios federados renuevan con Entra ID en el cliente, no con este mecanismo.
- Sustituir las respuestas de error por defecto de ASP.NET Core por un contrato RFC 7807 (`application/problem+json`) aplicado a fallos de autenticación, autorización y validación, mediante middleware de excepciones y `ProblemDetails` enriquecido.
- Crear la SPA base en Angular 20 con Standalone Components: estructura `core/`, `shared/` y `features/`, `AuthService`, `JwtInterceptor`, `ErrorInterceptor`, `AuthGuard`/`RoleGuard`, renovación silenciosa de la sesión, manejo de estado con Signals y entornos de configuración.
- Añadir la orquestación local mínima: Docker Compose con SQL Server 2022, archivos `appsettings` por entorno, endpoint de salud y scripts de arranque de la SPA.
- Añadir la infraestructura de pruebas: proyectos xUnit con Moq y FluentAssertions para dominio/aplicación, pruebas de integración con Testcontainers (SQL Server 2022) y `WebApplicationFactory` para el pipeline de autenticación y la rotación de tokens, y pruebas unitarias de interceptor/guard en Angular.

Fuera de alcance de este cambio (pertenece a etapas posteriores): entidades y CRUD de espacios o puestos, generación de QR, motor de disponibilidad, reservas recurrentes, carga de PDF, notificaciones SMTP, `BackgroundService` de liberación por inasistencia, sanciones y analítica. Tampoco cubre MFA, recuperación de contraseña, autogestión de perfil ni revocación administrativa masiva de sesiones.

## Capabilities

### New Capabilities

- `architecture/clean-architecture-baseline`: la solución compila en cuatro capas con dependencias unidireccionales hacia el dominio y sin fugas de tipos de infraestructura hacia capas internas.
- `identity/local-authentication`: inicio de sesión con credenciales locales de ASP.NET Core Identity, emisión de JWT propio y renovación rotativa con detección de reutilización para el personal administrativo.
- `identity/entra-id-federation`: aceptación de tokens Bearer emitidos por el inquilino institucional de Microsoft Entra ID para cualquier rol, resolviendo el rol efectivo contra un perfil previamente creado en la plataforma.
- `api-contracts/problem-details`: contrato uniforme de errores de autenticación y autorización en formato RFC 7807 (`application/problem+json`).

### Modified Capabilities

- Ninguna. El proyecto no tiene especificaciones principales previas (`openspec/specs/` está vacío); este cambio introduce las primeras capacidades.

## Impact

- **Código afectado (nuevo):** `src/Backend.Domain`, `src/Backend.Application`, `src/Backend.Infrastructure`, `src/Backend.Presentation.Api` (con su `Program.cs` y pipeline HTTP), `tests/` (unitarias, arquitectura e integración) y la SPA en `src/frontend` (o el directorio que defina la solución) con `core/`, `shared/` y `features/auth`.
- **APIs expuestas:** `POST /api/auth/login` (JWT local), `POST /api/auth/refresh` (rotación del par de tokens), `POST /api/auth/logout` (revocación), `GET /api/auth/me` (perfil del portador del token), contrato de error RFC 7807 para `400`, `401`, `403`, `409`, `423` y `500`; el resto de endpoints queda para cambios posteriores.
- **Dependencias nuevas:** .NET 10 SDK, ASP.NET Core Identity, EF Core 10.x con `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.Identity.Web` (o `Microsoft.IdentityModel.*` equivalente para validar Entra ID), Swashbuckle/OpenAPI, xUnit, Moq, FluentAssertions, Testcontainers; Angular 20 con `@angular/router` y RxJS; SQL Server 2022 en contenedor.
- **Configuración y secretos:** claves de firma JWT, `Authority`/`Audience`/`TenantId`/`ClientId` de Entra ID y cadena de conexión; ninguna clave se versiona, se consumen desde variables de entorno y `User Secrets`.
- **Seguridad:** cambia el modelo de acceso del sistema completo; cualquier endpoint futuro asumirá el usuario resuelto por este middleware y exactamente los roles `Administrativo`, `Docente` y `Estudiante`. Se añade un almacén de refresh tokens con hashes, lo que convierte el cierre de sesión y la rotación en parte del contrato de seguridad.
- **Procesos:** el pipeline CI debe compilar la solución, ejecutar la prueba de arquitectura y las pruebas de integración contra SQL Server efímero; se convierte en la puerta de calidad para los cambios siguientes.
- **Riesgo principal:** la validación de Entra ID depende de la conectividad con el inquilino (riesgo R2 del plan); el login local con Identity es el mecanismo de contingencia y debe quedar operativo aunque la validación federada no pueda alcanzar el proveedor.
