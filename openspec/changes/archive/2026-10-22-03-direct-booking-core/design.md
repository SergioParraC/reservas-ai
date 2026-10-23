# Design

## Context

Ver `proposal.md` — Why. Estado actual relevante:

- `Backend.Domain` expone `Espacio` (raíz con `Puesto` interno, `TipoAprobacion`, `EstadoUnidad`) y `CodigoQR`; no existe ninguna noción de horario ni de ocupación.
- `Backend.Application` trabaja con puertos (`IEspaciosRepository`, `IConsultaEspacios`, `IRelojSistema`, `IUsuarioActual`) y `Resultado<T>`; la prueba de arquitectura prohíbe EF Core y ASP.NET Core en esa capa.
- `ApplicationDbContext` ya usa SQL Server 2022 con `RowVersion` en `Espacios`; la migración `AreasYPuestos` es aditiva.
- La SPA tiene `features/areas` con `EspaciosApi`, stores con Signals, `authGuard`, `roleGuard`, `jwt.interceptor` y `problem-details.interceptor`.

Restricciones que condicionan el diseño:

- El dominio debe seguir en C# puro: ni configuración, ni `TimeZoneInfo` desde DI, ni ORM.
- `IApplicationDbContext` solo expone `SaveChangesAsync`; toda consulta y bloqueo nuevos entran por puertos.
- `Puesto` no es agregado independiente: la reserva lo referencia por `Id` y lo valida, pero nunca muta el agregado `Espacio`.
- El plan fija transacciones estrictas y `RowVersion` para el riesgo R1 (doble-reserva simultánea).
- Etapa 3 (QR, *no-show*, sanciones) no se implementa aquí, pero el enum de estado debe dejarle sitio.

## Goals / Non-Goals

**Goals:**

- Un agregado `Reserva` con invariantes de franja, estado y titularidad, verificable sin base de datos.
- Un `MotorDisponibilidad` puro que calcule franjas y detecte solapamientos con semántica de intervalo semiabierto `[inicio, fin)`.
- Garantizar una sola reserva vigente por puesto y franja bajo concurrencia real, con barreras independientes.
- Aplicar un cupo global de reservas activas por cuenta dentro de la misma transacción del alta.
- Cuatro endpoints REST coherentes con el contrato RFC 7807 existente.
- Una funcionalidad Angular autocontenida que no añada dependencias npm.

**Non-Goals:**

- Recurrencia semestral, aprobaciones multinivel, PDF, notificaciones y sanciones.
- Franjas de mantenimiento o bloqueos administrativos: el motor las admitirá como intervalos ocupados, pero este cambio no las persiste ni las expone.
- Reprogramar, extender o transferir una reserva; solo crear y cancelar.
- Reservar áreas completas o equipos que no sean `Puesto`.
- Recalcular la agenda en tiempo real por WebSocket: la frescura se obtiene al consultar.

## Decisions

### 1. `Reserva` como agregado propio, no colección de `Puesto`

```
Reserva (raiz de agregado)
+----------------------------------------------------------------+
| Id  PuestoId  UsuarioId                                        |
| Franja : FranjaHoraria (VO inmutable: Inicio, Fin)             |
| Estado : EstadoReserva  (Confirmada | Cancelada | ...)          |
| CreadaEn  CanceladaEn?  RowVersion                             |
+----------------------------------------------------------------+
         | PuestoId (referencia por identidad, sin navegacion)
         v
   Espacio / Puesto  --- se lee para validar, nunca se modifica
```

Motivo: una reserva se crea y se cancela con altísima frecuencia comparada con el catálogo; cargar el agregado `Espacio` con todos sus puestos en cada operación acoplaría dos ciclos de vida distintos y reabriría la regla de aforo desde un camino no previsto. `Reserva` se defiende sola: franja válida según política, estado inicial `Confirmada`, cancelación solo por su titular y solo dentro del margen.

Alternativa descartada: `Puesto.Reservas` como colección interna. Obligaría a bloquear el agregado completo en cada alta, mezclaría la concurrencia del catálogo con la de la agenda y crecería sin límite en memoria.

`EstadoReserva`: `Confirmada = 1`, `Cancelada = 2`, `CanceladaPorNoShow = 3` (reservado para la Etapa 3). Este cambio solo produce `1` y `2`; una reserva `3` se trata como no vigente.

### 2. `FranjaHoraria` como value object con intervalo semiabierto

- Inmutable, igualdad por valor, `Inicio` y `Fin` en `DateTimeOffset` con el mismo desplazamiento.
- Invariantes en la fábrica: `Fin > Inicio`, duración entre el mínimo y el máximo de la política, inicio alineado a la granularidad (`IntervaloMinutos`) e inicio dentro del horizonte `[ahora + antelación mínima, ahora + antelación máxima]`.
- `SeSolapaCon(otra)` usa `Inicio < otra.Fin && otra.Inicio < Fin`; por tanto `08:00-09:00` y `09:00-10:00` **no** se solapan, mientras que `09:00-10:30` sí choca con `10:00-11:00`.
- `Duracion`, `Contiene(momento)` y `Desplazar(...)` como operaciones puras.

La política viaja como objeto de dominio `PoliticaReserva` (`DuracionMinima`, `DuracionMaxima`, `IntervaloMinutos`, `AntelacionMinima`, `AntelacionMaxima`, `AntelacionCancelacion`, `HoraApertura`, `HoraCierre`). La aplicación la construye desde `OpcionesReservas`; el dominio nunca lee configuración ni zonas horarias del entorno.

### 3. `MotorDisponibilidad` como servicio de dominio estático

```
GenerarFranjas(fecha, politica, zona, ocupadas, ahora)
        |
        +--> 08:00 [libre]        --> FranjaCandidata(Disponible = true)
        +--> 09:00 [Ocupada]      --> FranjaCandidata(Motivo = Ocupada)
        +--> 10:00 [Antelacion]   --> FranjaCandidata(Motivo = FueraDeAntelacion)
        +--> 11:00 [libre]        --> FranjaCandidata(Disponible = true)
```

- Entrada: fecha solicitada, `PoliticaReserva`, zona horaria, intervalos ocupados (`FranjaHoraria` de reservas vigentes) y el instante actual.
- Salida: `FranjaCandidata(FranjaHoraria Franja, bool Disponible, MotivoNoDisponible? Motivo)` con `MotivoNoDisponible { Ocupada, FueraDeAntelacion, FueraDeHorizonte }`.
- `HaySolapamiento(candidata, ocupadas)` es la **única** rutina de conflicto, compartida por consulta, alta y prueba de concurrencia.
- Sin estado, sin DI, sin `DateTime.Now`: recibe `ahora` desde `IRelojSistema` en la capa de aplicación.

Alternativa descartada: calcular la disponibilidad en SQL con `EXISTS`. Es eficiente pero duplica la regla de conflicto en dos lenguajes y deja la matriz sin prueba unitaria aislada, que es justamente lo que el plan exige.

### 4. Doble reserva simultánea: barreras combinadas y `RowVersion` en EF Core 10

`RowVersion` detecta **actualizaciones** perdidas, no inserciones; por eso el alta se protege con bloqueo y el token cubre las transiciones de estado:

| Barrera | Dónde | Qué detiene |
| :--- | :--- | :--- |
| 1. Transacción `Serializable` + `SELECT ... WITH (UPDLOCK, HOLDLOCK)` sobre la fila del `Puesto` | `ReservasRepository.GuardarSiDisponibleAsync` | Serializa las altas concurrentes del mismo puesto antes de leer la agenda |
| 2. Re-verificación con `MotorDisponibilidad.HaySolapamiento` dentro de la transacción | Mismo método, tras el bloqueo | Solapamientos parciales o contenidos que la lectura previa no vio |
| 3. Índice único filtrado `IX_Reservas_PuestoId_Inicio_Activa` | Tabla `Reservas`, `WHERE [Estado] = 1` | Duplicados exactos de inicio si algo se salta la barrera 2; produce `DbUpdateException` |
| 4. `RowVersion` de `Reserva` (`IsRowVersion()`) | Cancelación y futuras transiciones | Cancelación concurrente o *check-in* de la Etapa 3 que pise un estado ya cambiado |
| 5. Conteo de reservas activas del usuario con `UPDLOCK, HOLDLOCK` sobre `IX_Reservas_UsuarioId_Inicio` | `ReservasRepository.GuardarSiDisponibleAsync`, tras el solapamiento | Que dos altas concurrentes del mismo usuario superen el cupo global |

Secuencia del alta:

```
POST /api/reservas
   |
   v
Handler: valida politica + FranjaHoraria -> arma Reserva.Confirmar(...)
   |
   v
ReservasRepository (ejecuta dentro de CreateExecutionStrategy):
   BEGIN TRAN (Serializable)
     SELECT 1 FROM Puestos WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puestoId
     SELECT Inicio, Fin FROM Reservas
       WHERE PuestoId = @p AND Estado = 1 AND Fin > @inicio AND Inicio < @fin
     HaySolapamiento? --si--> ROLLBACK -> Conflicto (409 franja-no-disponible)
     COUNT(*) FROM Reservas WITH (UPDLOCK, HOLDLOCK)
       WHERE UsuarioId = @u AND Estado = 1
     COUNT >= Reservas:MaxReservasActivasPorUsuario? --si--> ROLLBACK -> Conflicto (409 limite-reservas-alcanzado)
     INSERT Reservas (...)
   COMMIT
   |
   +-- DbUpdateConcurrencyException | DbUpdateException --> Conflicto (409)
   +-- deadlock 1205 --> reintento de la estrategia, maximo 3
```

Notas de implementación EF Core 10:

- `ReservaConfiguration`: `builder.Property(r => r.RowVersion).IsRowVersion();` y `builder.HasIndex(r => new { r.PuestoId, r.Inicio }).IsUnique().HasFilter("[Estado] = 1").HasDatabaseName("IX_Reservas_PuestoId_Inicio_Activa");`
- El `SELECT ... WITH (UPDLOCK, HOLDLOCK)` se emite con `FromSqlInterpolated` sobre `Puestos` dentro de la transacción, sin materializar la entidad.
- La transacción explícita se abre dentro de `Database.CreateExecutionStrategy().ExecuteAsync(...)` para que los reintentos no dejen transacciones huérfanas.
- El repositorio traduce toda excepción de persistencia a `false`, igual que `EspaciosRepository.GuardarCambiosAsync`, para no filtrar tipos del ORM a la aplicación.
- La cancelación carga la reserva con su `RowVersion` original, ejecuta `Reserva.Cancelar(...)` y guarda; un `DbUpdateConcurrencyException` se traduce a `409` sin reintento automático.
- El conteo del cupo se hace con `Estado = Confirmada` y `UsuarioId = @usuario` dentro de la misma transacción, de modo que una cancelación libera cupo en la siguiente lectura y dos altas simultáneas de la misma cuenta no lo exceden.

### 5. Contrato REST

| Endpoint | Autorización | Cuerpo / parámetros | Respuestas |
| :--- | :--- | :--- | :--- |
| `GET /api/puestos/{id}/disponibilidad` | Autenticado | `fecha` (`yyyy-MM-dd`), `desde`/`hasta` opcionales (`HH:mm`), `duracionMinutos` opcional | `200` con `{ puestoId, identificador, espacioId, espacioNombre, zonaHoraria, franjas: [{ inicio, fin, disponible, motivo }] }`; `400` fecha fuera de horizonte; `404` puesto inexistente; `409` puesto no reservable |
| `POST /api/reservas` | Autenticado | `{ puestoId, inicio, fin }` en ISO-8601 con desplazamiento | `201` + `Location: /api/reservas/{id}` con `ReservaDetalle`; `400` franja inválida; `404` puesto inexistente; `409` `franja-no-disponible`, `puesto-no-reservable` o `limite-reservas-alcanzado` |
| `GET /api/reservas/me` | Autenticado (titular) | `desde`, `hasta`, `estado`, `pagina`, `tamano` | `200` con `PaginaResultado<ReservaResumen>` ordenado por inicio ascendente; `400` rango inválido |
| `DELETE /api/reservas/{id}` | Autenticado (titular) | — | `204`; `403` reserva ajena; `404` inexistente; `409` `fuera-de-plazo` o estado no cancelable |

- `ReservasController` ocupa `api/reservas`; la disponibilidad se añade a `PuestosController` (ya existente en `api/puestos`), retirando la restricción de rol solo en esa acción pero manteniendo `[Authorize]`: la agenda nunca se consulta de forma anónima.
- Slugs nuevos en `CatalogoProblemas`: `franja-no-disponible`, `puesto-no-reservable`, `fuera-de-plazo` y `limite-reservas-alcanzado`. Se mantiene `application/problem+json` con `traceId` y `errors`.
- Las fechas se devuelven con el desplazamiento de la zona configurada (`America/Bogota`) y se persisten en UTC; el cliente no decide el desplazamiento.
- `GET /api/reservas/me` nunca devuelve reservas ajenas: el titular se resuelve con `IUsuarioActual.UsuarioId`, no con un parámetro del cliente.

### 6. Componentes Angular 20 en `features/solicitudes-estudiante`

```
features/solicitudes-estudiante/
+-- solicitudes.routes.ts          /solicitudes y /solicitudes/puesto/:puestoId
+-- models/reserva.models.ts       FranjaDisponible, ReservaResumen, SolicitudReserva
+-- services/reservas.api.ts       GET disponibilidad, POST, GET me, DELETE
+-- estado/reservas.store.ts       Signals: franjas, reservas, cargando, error, cancelando
+-- mappers/reserva.mapper.ts      ISO-8601 <-> Date con Intl (America/Bogota)
+-- pages/puesto-disponibilidad.page.ts
+-- pages/mis-reservas.page.ts
+-- components/franja-lista.ts
+-- components/reserva-card.ts
```

- Standalone Components, `inject()`, Signals y `computed()`; sin NgRx ni nuevas dependencias npm.
- `reservas.api.ts` reutiliza `environment.apiBaseUrl`, `HttpParams` y el patrón de `EspaciosApi`; el interceptor JWT ya añade el token y `problem-details.interceptor` ya normaliza los errores.
- `PuestoDisponibilidadPage`: lee `puestoId` de la ruta y fecha/duración de *query params*, pide disponibilidad, pinta las franjas y confirma con un `POST`; ante `409` recarga la agenda y muestra el problema.
- `MisReservasPage`: lista paginada con filtro por estado, marca las cancelables (`ahora + margen < inicio`) y pide confirmación antes del `DELETE`; ante `204` refresca la lista.
- Integración: en `espacio-detalle.page` cada puesto activo de un área con `TipoAprobacion.Automatica` enlaza a `/solicitudes/puesto/{puestoId}`; `shared/shell` añade "Mis reservas" para sesiones autenticadas y `app.routes.ts` carga `SOLICITUDES_ROUTES` bajo `authGuard`.
- Zona horaria: la SPA muestra las horas con `Intl.DateTimeFormat('es-CO', { timeZone: 'America/Bogota' })` y reenvía el `DateTimeOffset` recibido sin reconstruirlo en UTC.

### 7. Configuración y opciones

`OpcionesReservas` (en `Application/Common/OpcionesAplicacion.cs`, mismo patrón que `OpcionesEspacios`) se enlaza en `DependencyInjection` como singleton y se registra en `appsettings.json`:

| Clave | Valor por defecto | Uso |
| :--- | :--- | :--- |
| `Reservas:AntelacionMinimaMinutos` | `30` | Inicio mínimo de una reserva nueva |
| `Reservas:AntelacionMaximaDias` | `14` | Horizonte de consulta y de alta |
| `Reservas:DuracionMinimaMinutos` | `30` | Franja mínima |
| `Reservas:DuracionMaximaMinutos` | `240` | Franja máxima |
| `Reservas:IntervaloMinutos` | `15` | Granularidad de alineación de franjas |
| `Reservas:AntelacionCancelacionMinutos` | `30` | Margen mínimo para cancelar |
| `Reservas:MaxReservasActivasPorUsuario` | `2` | Cupo de reservas `Confirmada` simultáneas por cuenta |
| `Reservas:HoraApertura` / `Reservas:HoraCierre` | `07:00` / `21:00` | Jornada reservable |
| `Reservas:ZonaHoraria` | `America/Bogota` | Interpretación de la jornada |


## Risks / Trade-offs

- **Bloqueo de fila del puesto serializa las altas de un mismo equipo** → Es el comportamiento buscado: la contención se limita a un `PuestoId` y dura milisegundos; puestos distintos no se bloquean entre sí.
- **Transacción `Serializable` con reintentos puede degradar el rendimiento bajo carga masiva** → Las consultas usan índices por `PuestoId` y ventana temporal; se acepta la contención antes que una reserva duplicada. El plan ya asume este costo para el riesgo R1.
- **El índice único filtrado solo cubre el mismo `Inicio`** → Los solapamientos parciales dependen de las barreras 1 y 2; la prueba de integración concurrente debe cubrir ambos casos, no solo el duplicado exacto.
- **`RowVersion` no previene inserciones** → Se documenta explícitamente y se complementa con bloqueo; nunca se presenta como única defensa.
- **Diferencia entre reloj del servidor y reloj del estudiante** → Toda validación de antelación y de margen de cancelación usa `IRelojSistema` en el servidor; el cliente solo muestra y deshabilita botones.
- **Zona horaria mal configurada** → `Reservas:ZonaHoraria` se valida al arrancar; un valor inválido impide iniciar la API en lugar de desplazar silenciosamente las franjas.
- **Cambio de hora estacional en zonas con horario de verano** → La jornada se construye con `TimeZoneInfo` de la zona configurada; `America/Bogota` no aplica DST, pero la implementación no asume `-05:00` fijo.
- **Reservas huérfanas si se desactiva un puesto o un área** → Las reservas futuras se conservan; consultar y reservar verifica el estado vigente y un puesto inactivo no admite altas nuevas. La política de cancelación masiva queda para una etapa posterior.
- **Conteo del cupo bajo altas concurrentes del mismo usuario** → El conteo se ejecuta dentro de la misma transacción del alta con `UPDLOCK, HOLDLOCK` sobre `IX_Reservas_UsuarioId_Inicio`, de modo que dos solicitudes simultáneas de la misma cuenta no superan `Reservas:MaxReservasActivasPorUsuario`.

## Migration Plan

1. Dominio: `FranjaHoraria`, `PoliticaReserva`, `EstadoReserva`, `Reserva`, `MotorDisponibilidad` y `MensajesReserva`; prueba unitaria de solapamientos, duración, alineación y cancelación.
2. `OpcionesReservas`, puertos `IReservasRepository` / `IConsultaReservas`, DTOs y handlers de disponibilidad, alta, consulta y cancelación; la suite de arquitectura sigue en verde.
3. `ReservaConfiguration`, `DbSet<Reserva>`, migración `Reservas` con `RowVersion` e índice único filtrado; verificar con `dotnet ef database update` e inspección de `sys.indexes`.
4. `ReservasRepository` (bloqueo, transacción, conteo del cupo y reintentos) y `ConsultaReservas` (`AsNoTracking`, proyecciones); pruebas de integración con Testcontainers, incluidas dos altas concurrentes, dos altas del mismo usuario y dos cancelaciones concurrentes.
5. `ReservasController` y la acción de disponibilidad en `PuestosController`, con slugs de problema nuevos; pruebas HTTP de `200`, `201`, `204`, `400`, `401`, `403`, `404` y `409`.
6. SPA `features/solicitudes-estudiante`, enlaces en catálogo y shell, rutas y pruebas de store, servicio y componentes.
7. Cierre: `openspec validate 03-direct-booking-core --strict`, `dotnet test src/Backend.sln`, `npm test`, `npm run build` y humo (consulta de franjas → reserva → verificación en "Mis reservas" → rechazo de una segunda reserva solapada → cancelación dentro del plazo).

**Rollback:** el cambio es aditivo. Revertir la rama y, si la migración ya se aplicó, ejecutar `dotnet ef database update AreasYPuestos`; el `Down` elimina solo la tabla `Reservas` y sus índices. La SPA y la configuración `Reservas:*` pueden revertirse sin afectar al catálogo ni a la identidad.

## Decisiones de Diseño Adoptadas

Las preguntas abiertas de la versión anterior quedan cerradas con los siguientes acuerdos; no hay puntos pendientes que puedan alterar los contratos de esta etapa.

1. **Disponibilidad autenticada.** `GET /api/puestos/{id}/disponibilidad` exige sesión con `[Authorize]` y no se publica de forma anónima, aunque el catálogo sí lo sea. Se evita exponer la agenda de ocupación a rastreadores y se conserva un único flujo de autorización para consultar y reservar.
2. **Cupo global de reservas activas.** `Reservas:MaxReservasActivasPorUsuario` (por defecto `2`) limita las reservas en estado `Confirmada` de una misma cuenta. El alta responde `409` con `type` `limite-reservas-alcanzado` cuando el cupo está lleno, y el conteo se ejecuta dentro de la transacción del alta para que dos solicitudes simultáneas de la misma cuenta no lo superen. Una reserva `Cancelada` deja de contar de inmediato.
3. **Plazo global de cancelación.** `Reservas:AntelacionCancelacionMinutos` (por defecto `30`) es único para toda la institución; no se parametriza por área ni por categoría en esta etapa. Cancelar exige que falten al menos esos minutos para el inicio de la franja; una reserva con menos antelación permanece `Confirmada`.
4. **Inmutabilidad del código QR.** El `CodigoQR` identifica al recurso físico y no a la reserva: cancelar solo cambia el estado transaccional de `Reserva` a `Cancelada`. El QR del `Puesto` no se regenera, no se reasigna y es el mismo antes y después de cualquier reserva o cancelación, en coherencia con `espacios/gestion-espacios`.


