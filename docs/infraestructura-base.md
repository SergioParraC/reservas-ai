# Infraestructura base: arranque local completo

Guía reproducible para levantar API, base de datos y SPA desde un clon limpio.

## 1. Prerrequisitos

| Herramienta | Versión |
| --- | --- |
| SDK de .NET | 10.0.x |
| `dotnet-ef` | 10.0.x (`dotnet tool update --global dotnet-ef`) |
| Node.js + npm | 22+ |
| SQL Server | 2022 (instancia local o contenedor) |
| Docker Desktop | Solo si se usa `docker compose` o las pruebas de integración |

## 2. Opción A — SQL Server local

1. Verifica la instancia (`MSSQL$SQLEXPRESS` debe estar en ejecución):

   ```powershell
   Get-Service | Where-Object { $_.Name -like 'MSSQL*' } | Select-Object Name, Status
   ```

2. Configura la cadena y la clave de firma (nunca se versionan):

   ```bash
   dotnet user-secrets set "Jwt:Key" "<clave-de-32+-caracteres>" --project src/Backend.Presentation.Api
   dotnet user-secrets set "Seed:AdminEmail" "admin@universidad.edu" --project src/Backend.Presentation.Api
   dotnet user-secrets set "Seed:AdminPassword" "<contraseña-segura>" --project src/Backend.Presentation.Api
   ```

   La cadena vive en `src/Backend.Presentation.Api/appsettings.Development.json`
   (`ConnectionStrings:DefaultConnection`).

3. Aplica el esquema y arranca la API:

   ```bash
   dotnet ef database update --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api
   dotnet run --project src/Backend.Presentation.Api
   ```

   En `Development` la API aplica migraciones y siembra los roles y el administrador al arrancar.
   Comprueba la salud del proceso: `curl http://localhost:5085/health` → `Healthy`.

## 3. Opción B — Todo en contenedores

```bash
docker compose up -d --build
docker compose ps           # mssql y api en estado healthy
```

- API: `http://localhost:8080` (`/health`, `/openapi/v1.json` en Development).
- SQL Server: `localhost:1433`, usuario `sa`, contraseña `${MSSQL_SA_PASSWORD}` (valor por defecto
  de desarrollo en `docker-compose.yml`; cámbialo con un archivo `.env`).
- La API espera a que `mssql` esté `healthy` (`depends_on.condition: service_healthy`).

Variables opcionales para `docker compose`:

```bash
MSSQL_SA_PASSWORD=... JWT_KEY=... SEED_ADMIN_EMAIL=... SEED_ADMIN_PASSWORD=... docker compose up -d --build
```

Para detener y borrar datos: `docker compose down -v`.

## 4. Cliente web (SPA Angular 20)

```bash
cd src/frontend
npm ci
npm start          # http://localhost:4200 con proxy /api -> https://localhost:7129
```

`proxy.conf.json` evita CORS en desarrollo. En la API, la política CORS solo admite los orígenes
declarados en `Cors:OrigenesPermitidos` (`http://localhost:4200` por defecto).

Build y pruebas del cliente:

```bash
npm run build
npm test
```

## 5. Verificación de humo

Con la API en `http://localhost:5085` (o `http://localhost:8080` en contenedor):

```bash
# 1. Estado de la base de datos
curl -i http://localhost:5085/health
# 200 OK · cuerpo: Healthy

# 2. Login local del administrador
curl -s -X POST http://localhost:5085/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"correoInstitucional":"admin@universidad.edu","contrasena":"<contraseña>"}'
# 200 OK · { token, tipoToken: "Bearer", expiraEn, roles: ["Administrativo"] }

# 3. Perfil del portador (usar el token anterior)
curl -s http://localhost:5085/api/auth/me -H "Authorization: Bearer <token>"
# 200 OK · { id, nombreCompleto, correoInstitucional, tipoActor, roles }

# 4. Endpoint administrativo protegido
curl -i http://localhost:5085/api/administracion/verificacion -H "Authorization: Bearer <token>"
# 200 OK

# 5. Sin sesión (equivalente a cerrar sesión en la SPA)
curl -i http://localhost:5085/api/auth/me
# 401 Unauthorized · application/problem+json · type .../token-ausente · WWW-Authenticate: Bearer
```

En la SPA: entrar en `http://localhost:4200`, iniciar sesión, abrir **Inicio** (perfil) y
**Verificación administrativa**, cerrar sesión y comprobar que vuelve a `/login`.

## 6. Pruebas y tubería de integración continua

```bash
dotnet build src/Backend.sln -c Release
dotnet test src/Backend.sln                  # arquitectura + unitarias; integración se omite sin Docker
dotnet test tests/Backend.IntegrationTests   # requiere Docker activo (Testcontainers SQL Server 2022)
```

`.github/workflows/ci.yml` ejecuta en cada `push`/`pull request`:

1. `dotnet restore` y `dotnet build -c Release`.
2. Pruebas de arquitectura, unitarias e integración con Testcontainers, con reportes `.trx`
   publicados como artefacto (tres reportes).
3. `npm ci`, `npm run build` y `npm test` para la SPA.

Una violación de la regla de dependencias rompe el paso de arquitectura y, por tanto, el pipeline.

## 7. Matriz de trazabilidad

| Capacidad | Requisito verificado por | Prueba automatizada |
| --- | --- | --- |
| Arquitectura limpia | Compilación de las cuatro capas y dependencias unidireccionales | `Backend.ArchitectureTests.ArquitecturaTests` (4 pruebas) |
| Arquitectura limpia | Dominio y aplicación sin tecnologías externas | `ElDominioNoDependeDePaquetesTecnologicos`, `LaAplicacionNoDependeDeIdentityNiDeEntityFramework` |
| Autenticación local | Credenciales válidas, incorrectas, inexistentes, inactivas y bloqueadas | `Backend.UnitTests.Autenticacion.LoginLocalHandlerTests` |
| Autenticación local | Bloqueo 423, reinicio del contador, política de contraseñas y correo único | `IdentityServiceTests` (Testcontainers) |
| Autenticación local | `POST /api/auth/login`, `GET /api/auth/me`, `403` por rol | `EndpointsAutenticacionTests`, `ProblemDetailsTests` |
| Federación Entra ID | Aprovisionamiento y reutilización de perfiles, `403` sin rol académico, `401` de otro inquilino, `503` sin proveedor | `HybridAuthTests` (Testcontainers) |
| Contrato RFC 7807 | `type`, `title`, `status`, `detail`, `instance`, `traceId`; `400` por campo; `500` sin trazas | `ProblemDetailsTests` |
| Persistencia | Esquema, índice filtrado, seed idempotente, migraciones solo en Development | `EsquemaIdentityTests`, `InicializadorIdentityTests`, `MigracionesAlArranqueTests` |
| Operación | `/health` con y sin base disponible; CORS permitido y denegado | `SaludTests`, `CorsTests` |
| Cliente web | Sesión con Signals, interceptores, guards y formulario de login | `sesion.store.spec.ts`, `jwt.interceptor.spec.ts`, `problem-details.interceptor.spec.ts`, `auth.guard.spec.ts`, `role.guard.spec.ts`, `login.page.spec.ts` |
