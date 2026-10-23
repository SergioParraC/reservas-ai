# Persistencia de identidad (Etapa 1)

Documento operativo del esquema de identidad sobre **EF Core 10 + SQL Server 2022**.
Cubre la configuración local, los comandos de migración verificados, la política de seed y la
suite de pruebas de integración con Testcontainers.

## 1. Requisitos

| Componente | Versión usada |
| --- | --- |
| SDK de .NET | 10.0.302 |
| `dotnet-ef` | 10.0.13 (`dotnet tool update --global dotnet-ef`) |
| EF Core (`SqlServer`, `Design`, `Identity.EntityFrameworkCore`) | 10.0.13 |
| SQL Server | 2022 (instancia local `LAPTOP-ASUS-F15\SQLEXPRESS` o contenedor) |
| Docker Desktop | Solo para la suite de integración con Testcontainers |

> La CLI `dotnet-ef` debe compartir la versión mayor de EF Core del proyecto. Con la CLI 9.x y
> EF Core 10 la generación de migraciones falla.

## 2. Cadena de conexión local

La cadena de desarrollo vive en `src/Backend.Presentation.Api/appsettings.Development.json`
(autenticación integrada de Windows, sin contraseña versionada):

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=LAPTOP-ASUS-F15\\SQLEXPRESS;Initial Catalog=BudgetCivilDb;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name=\"SQL Server Management Studio\";Command Timeout=0"
}
```

`AddInfrastructure` lanza `InvalidOperationException` al arrancar si
`ConnectionStrings:DefaultConnection` no está configurada, de modo que la API no inicia con una
persistencia mal configurada.

## 3. Esquema de identidad

Un único contexto (`ApplicationDbContext`) hereda de `IdentityDbContext<Usuario, Rol, Guid>` y
aplica las configuraciones Fluent API de su propio ensamblado.

| Tabla | Contenido |
| --- | --- |
| `AspNetUsers` | Perfil institucional: columnas de Identity más `NombreCompleto`, `TipoActor`, `IdentificadorFederado`, `Activo`, `CreadoEn`, `UltimoAccesoEn` |
| `AspNetRoles` | Exactamente tres roles: `Administrativo`, `Docente`, `Estudiante` |
| `AspNetUserRoles` | Asignación de roles por usuario |
| `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims` | Soportes de Identity |
| `__EFMigrationsHistory` | Migraciones aplicadas |

Detalles del modelo:

- `NombreCompleto` es `nvarchar(200) NOT NULL`.
- `TipoActor` se persiste como `int NOT NULL` con conversión explícita, para que el esquema no
  dependa del orden de los nombres del enum.
- `IdentificadorFederado` es `nvarchar(100) NULL` con el índice único filtrado
  `IX_AspNetUsers_IdentificadorFederado ... WHERE [IdentificadorFederado] IS NOT NULL`.
- `CreadoEn` es `datetimeoffset NOT NULL` y `UltimoAccesoEn` es `datetimeoffset NULL`.
- Las migraciones viven en `src/Backend.Infrastructure/Persistence/Migrations`.

## 4. Migraciones

```bash
# Crear una migración nueva
dotnet ef migrations add <Nombre> --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api

# Aplicarla a la base local
dotnet ef database update --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api

# Revisar el estado
dotnet ef migrations list --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api

# Generar el script SQL equivalente
dotnet ef migrations script --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api --output esquema-inicial.sql
```

Estado verificado en este repositorio: la migración `20261022125402_InitialCreate` está
**aplicada** en `LAPTOP-ASUS-F15\SQLEXPRESS` (base `BudgetCivilDb`) y el script SQL contiene las
tablas de Identity, el índice filtrado y las columnas de auditoría.

### Aplicación automática

`Program.cs` aplica migraciones y ejecuta la siembra **solo** cuando
`ASPNETCORE_ENVIRONMENT=Development`. En cualquier otro entorno el esquema se actualiza como paso
explícito de despliegue (`dotnet ef database update` o el script SQL).

## 5. Política de seed

`InicializadorIdentity` es idempotente y se ejecuta después de migrar:

1. Crea los roles `Administrativo`, `Docente` y `Estudiante` si no existen. No se admiten otros.
2. Crea el administrador inicial si no existe y le asigna el rol `Administrativo`.

Las credenciales del administrador **no** se versionan. Se leen de `Seed:AdminEmail`,
`Seed:AdminPassword` y, opcionalmente, `Seed:AdminNombre`:

```bash
dotnet user-secrets set "Seed:AdminEmail" "admin@uninpahu.edu.co" --project src/Backend.Presentation.Api
dotnet user-secrets set "Seed:AdminPassword" "<contraseña-segura>" --project src/Backend.Presentation.Api
dotnet user-secrets set "Seed:AdminNombre" "Administrador del Sistema" --project src/Backend.Presentation.Api
```

Si las claves no están configuradas, la siembra de roles continúa y el administrador se omite con
un registro informativo; la aplicación arranca normalmente.

### Clave de firma JWT

La API emite y valida tokens locales con una clave simétrica que **no** se versiona. En desarrollo
se configura por User Secrets y en contenedor mediante una variable de entorno:

```bash
dotnet user-secrets set "Jwt:Key" "<clave-aleatoria-de-al-menos-32-caracteres>" --project src/Backend.Presentation.Api
```

`Jwt:Issuer`, `Jwt:Audience` y `Jwt:ExpiraMinutos` sí viven en `appsettings.json` porque no son
secretos. Si `Jwt:Key` falta o tiene menos de 32 caracteres, la API no arranca: el fallo es
explícito para no emitir tokens con una firma débil.

## 6. Pruebas

| Proyecto | Requiere Docker | Qué valida |
| --- | --- | --- |
| `tests/Backend.UnitTests` | No | Casos de uso de autenticación y modelo de EF Core (longitudes, conversión de `TipoActor`, índice filtrado) sin abrir conexión |
| `tests/Backend.ArchitectureTests` | No | Regla de dependencias, pureza del dominio y aislamiento de la aplicación |
| `tests/Backend.IntegrationTests` | Sí | Migración real sobre SQL Server 2022, idempotencia del seed y política de migraciones al arranque |

```bash
# Toda la solución (las pruebas con Docker se omiten si Docker no está activo)
dotnet test src/Backend.sln
```

La suite de integración es modular:

- `Infraestructura/SqlServerContainerFixture` levanta **un** contenedor
  `mcr.microsoft.com/mssql/server:2022-latest` de forma diferida y crea una base vacía por prueba.
- `Infraestructura/DockerFactAttribute` omite la prueba con un mensaje explícito cuando
  `docker info` no responde, de modo que la suite unitaria permanece verde sin Docker.
- `Persistencia/EsquemaIdentityTests`, `Persistencia/InicializadorIdentityTests` y
  `Persistencia/MigracionesAlArranqueTests` cubren esquema, seed y arranque.

El contenedor se descarta al finalizar la colección; no se usa `EnsureCreated`, por lo que cada
prueba valida la migración real.
