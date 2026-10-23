# Design

## Context

Ver `proposal.md` — Why. Estado actual que condiciona el diseño:

- `Reserva` es raíz de agregado por puesto con `FranjaHoraria` `[inicio, fin)`, `EstadoReserva` (`Confirmada = 1`, `Cancelada = 2`, `CanceladaPorNoShow = 3`) y `RowVersion`; `LiberarPorNoShow` ya existe como gancho de esta etapa, pero `EstaVigente` solo cubre `Confirmada`.
- El índice único filtrado `IX_Reservas_PuestoId_Inicio_Activa` usa `[Estado] = 1`, y `ReservasRepository.GuardarSiDisponibleAsync` revalida solapamiento y cupo también con `[Estado] = 1`: todo estado que siga ocupando la franja debe entrar en esas tres comprobaciones.
- `Backend.Application` trabaja con puertos (`IReservasRepository`, `IConsultaReservas`, `IRelojSistema`, `IUsuarioActual`) y `Resultado<T>`; la prueba de arquitectura prohíbe EF Core, ASP.NET Core y `Microsoft.Extensions` en esa capa, así que el `BackgroundService` no puede vivir ahí.
- `Backend.Infrastructure` ya registra `DespachadorCorreos` (`BackgroundService` con `IServiceScopeFactory`, opciones validadas al arrancar y `try/catch` por ciclo); se replica ese patrón.
- Cada `Puesto` tiene `CodigoQR` inmutable (`QR-` + 26 caracteres Crockford Base32) que identifica su placa física; `Reserva` referencia al puesto por identidad y no guarda el código.
- La SPA es Angular 20 con Standalone Components, Signals, `authGuard`, `roleGuard` y `problem-details.interceptor`; no hay dependencia de escaneo instalada.

Restricciones: el QR se compara en el servidor (nunca en el cliente), la tolerancia llega desde `Reservas:VentanaToleranciaCheckInMinutos` (ningún literal en dominio ni en base de datos), la ventana temporal se evalúa con el reloj del servidor y la migración debe ser aditiva sobre datos existentes.

## Goals / Non-Goals

**Goals:**

- Confirmar presencia con la placa QR estática del puesto y una ventana simétrica leída de `Reservas:VentanaToleranciaCheckInMinutos`.
- Que `EnUso` bloquee la franja y cuente para el cupo del titular igual que `Confirmada`, sin relajar la anti-doble-reserva.
- Que el check-out se pueda registrar en cualquier momento desde `EnUso` y devuelva el remanente de la franja al motor de disponibilidad sin intervención manual.
- Liberar sin intervención humana toda reserva vencida sin check-in, de forma idempotente y recuperable tras una caída del proceso.
- Un mapa de ocupación instantáneo y de solo lectura, por área y en totales, que refleje la liberación por No-Show y por salida anticipada.

**Non-Goals:**

- QR dinámico o rotativo, geolocalización, Bluetooth o biometría.
- Extender el uso más allá del fin planificado: un check-out tardío registra la hora real, pero no alarga la franja.
- Reprogramar la franja liberada, notificar al ausente o sancionarlo (Etapa 4).
- Tiempo real por SignalR/WebSocket: el tablero consulta por sondeo.
- Check-in sobre `ReservaEspacio` (series semestrales y solicitudes aprobadas): esta etapa cubre solo `Reserva` de puesto.

## Decisions

### 1. `Reserva` gana marcas temporales y una máquina de estados explícita

```
                        RegistrarCheckIn(ahora)          RegistrarCheckOut(ahora)
  Confirmada (1) ------------------------------> EnUso (4) ------------------------> Finalizada (5)
       |                                                                                  (terminal)
       | Cancelar(ahora, politica)
       v
  Cancelada (2)   (terminal)

  Confirmada (1) --LiberarPorNoShow(ahora)--> NoShow (3)   (terminal)
```

| Operación | Estado origen | Precondición | Resultado |
| :--- | :--- | :--- | :--- |
| `RegistrarCheckIn` | `Confirmada` | `Inicio - tol <= ahora <= Inicio + tol` | `EnUso`, `FechaCheckIn = ahora` |
| `RegistrarCheckOut` | `EnUso` | ninguna temporal: basta con `FechaCheckIn` registrada | `Finalizada`, `FechaCheckOut = ahora`, `Fin = max(Inicio, min(FinPlanificado, ahora))` |
| `LiberarPorNoShow` | `Confirmada` | la invoca el barrido al vencer la tolerancia | `NoShow`, `CanceladaEn = ahora` |

`FinPlanificado` se fija en el alta con el `Fin` pactado y nunca cambia; `Fin` se acorta al salir antes y jamás se extiende (`min(FinPlanificado, ahora)`), con piso en `Inicio` para no producir una franja invertida. `CanceladaPorNoShow` se renombra a `NoShow` conservando el valor `3`, de modo que ninguna fila persistida cambia de significado; `EnUso = 4` y `Finalizada = 5` son valores nuevos. `EstaVigente` pasa a `Estado is Confirmada or EnUso`, de modo que `Finalizada` deja de ocupar en el mismo instante en que se guarda. Toda transición inválida (`Confirmada`, `NoShow`, `Cancelada`, `Finalizada`) lanza `ConflictoException` (`transicion-invalida`), igual que hace hoy `Cancelar`.

### 2. `Reservas:VentanaToleranciaCheckInMinutos` es la única fuente de la tolerancia

`OpcionesReservas` (ya registrada como singleton en `DependencyInjection` con `Validar()`) suma la clave y su conversión a `TimeSpan`:

```csharp
public int VentanaToleranciaCheckInMinutos { get; set; } = 15;

public TimeSpan ObtenerVentanaTolerancia() => TimeSpan.FromMinutes(VentanaToleranciaCheckInMinutos);
```

```json
"Reservas": {
  "AntelacionCancelacionMinutos": 30,
  "VentanaToleranciaCheckInMinutos": 15
}
```

`RegistrarCheckInHandler` y `NoShowBackgroundService` reciben la misma instancia de `OpcionesReservas` y llaman a `ObtenerVentanaTolerancia()`; el dominio nunca lee configuración: `EstaEnVentanaDeCheckIn(momento, tolerancia)` y `LiberarPorNoShow` reciben el `TimeSpan`. `Validar()` rechaza valores menores que 1 o mayores que 720 y falla el arranque, de modo que no existe ni un `15` literal en el dominio, ni una columna `ToleranciaMinutos` en la base de datos, ni dos valores que puedan divergir entre el endpoint y el worker. Cambiar la clave en `appsettings.json` y reiniciar reajusta ambos caminos a la vez.

Alternativa descartada: guardar la ventana por área o por reserva. Añade una columna y una consulta por cada check-in, y permitiría que dos puestos de la misma jornada se validen con márgenes distintos sin necesidad de negocio; el margen es institucional, como la jornada y el calendario.

Alternativa descartada: constante en `Reserva` o valor por omisión en el handler. Duplicaría la fuente y obligaría a recompilar para ajustar la operación.

### 3. El QR estático del puesto es la prueba de presencia

El check-in recibe el código leído de la placa y lo compara con `Puesto.CodigoQR` del puesto reservado, rehidratándolo con `CodigoQR.Desde` (valida prefijo, longitud y alfabeto). El código no se deduce ni se devuelve en la respuesta del check-in.

```
usuario autenticado + reservaId + codigoQr
        |
        +-- reserva no existe .......................... 404 reserva-no-encontrada
        +-- reserva de otro titular .................... 403 reserva-ajena
        +-- estado != Confirmada ....................... 409 transicion-invalida
        +-- codigoQr no es el del puesto ............... 409 check-in-codigo-invalido
        +-- ahora fuera de [Inicio-tol, Inicio+tol] .... 409 check-in-fuera-de-ventana
        +-- todo valido ................................ 200 EnUso
```

Límite aceptado y documentado (riesgo R3 del plan): una fotografía de la placa permite el fraude, pero solo dentro de la ventana y con una reserva propia vigente en ese puesto; el QR nunca se comparte en la respuesta.

### 4. `EnUso` ocupa la franja: tres puntos de persistencia que deben ampliarse juntos

- Índice único: `HasFilter("[Estado] IN (1, 4)")`, migrado con `DROP INDEX` + `CREATE UNIQUE INDEX`.
- Revalidación de solapamiento en `GuardarSiDisponibleAsync`: `Estado == Confirmada || Estado == EnUso`.
- Cupo del titular: `[Estado] IN (1, 4)` (quien está usando un puesto sigue teniendo una reserva activa).
- Nuevo índice `IX_Reservas_Estado_Inicio` para el barrido (`Estado = 1 AND Inicio <= @limite`).

Si se omite cualquiera de los tres primeros, el índice deja de proteger la franja en el minuto exacto en que la reserva empieza a usarse.

### 5. Check-out flexible: el remanente se libera acortando `Fin`

```
Reserva EnUso                        check-out 10:45
+-------------------+----------------+
10:00              10:45            11:00
|      usado        |   remanente    |
                    ^
                    Fin := 10:45   (FinPlanificado := 11:00)

GET /api/puestos/{id}/disponibilidad -> la franja 10:45-11:00 vuelve a estar Disponible
POST /api/reservas (otro usuario)    -> 201 Created sobre 10:45-11:00
```

- `RegistrarCheckOut(momento)` exige únicamente `Estado == EnUso` (es decir, `FechaCheckIn` ya registrada) y aplica `Fin = Max(Inicio, Min(FinPlanificado, momento))`. No hay ventana superior: un check-out a las `11:30` de una franja `10:00-11:00` responde `200`, guarda `FechaCheckOut = 11:30` y deja `Fin = 11:00`.
- `EstaVigente` excluye `Finalizada`, y tanto `IConsultaReservas.ObtenerOcupadasAsync` como la revalidación del alta filtran `Estado IN (1, 4)`: el remanente reaparece en la siguiente consulta de disponibilidad sin recalcular nada ni escribir filas extra. `MotorDisponibilidad` no cambia: solo recibe menos intervalos ocupados.
- La carrera por el remanente (dos usuarios solicitando `10:45-11:00` a la vez) la resuelve la transacción serializable ya existente en `GuardarSiDisponibleAsync` más el índice único filtrado; no se añade bloqueo nuevo.
- El corte nunca invierte la franja: si la salida ocurre antes del `Inicio` (ingreso anticipado + salida inmediata), `Fin = Inicio` y la franja completa vuelve a estar libre.

### 6. `NoShowBackgroundService`: barrido periódico con lote atómico y lógica en aplicación

```
PeriodicTimer(5 min)  ->  scope nuevo  ->  LiberarReservasVencidasHandler
                                              |
                                              v
                        ListarSinCheckInVencidasAsync(ahora, tol, maximo)
                                              |
                                   reserva.LiberarPorNoShow(ahora)
                                              |
                                   GuardarCambiosAsync()  (un lote)
```

- El servicio (`Backend.Infrastructure/BackgroundJobs/NoShowBackgroundService.cs`) resuelve `IServiceScopeFactory` por ciclo, como `DespachadorCorreos`, y usa `PeriodicTimer` para no acumular ciclos solapados.
- El corte es `Inicio + tolerancia <= ahora`, con la tolerancia de `OpcionesReservas.ObtenerVentanaTolerancia()`; el dominio decide y el handler de aplicación solo orquesta. La lista se pide con tope (`MaximoReservasPorBarrido`) ordenada por inicio ascendente, para que un atraso se recupere en varios ciclos.
- El barrido no toca `EnUso`: una sesión abierta se cierra solo con el check-out del titular, que puede llegar a cualquier hora (`Finalizada`). El mapa de ocupación limita el conteo a la franja en curso, así que una sesión olvidada no infla la ocupación.
- Un `try/catch` por ciclo registra con `ILogger` y no detiene el host (R4). El lote se confirma una vez: si falla, se revierte completo y el siguiente ciclo lo reintenta; el barrido es idempotente porque solo toma reservas `Confirmada` vencidas.
- `Habilitado = false` deshabilita el servicio (útil en pruebas y en entornos sin worker), igual que `Smtp:Habilitado`.

### 7. Contratos REST

| Endpoint | Rol | Éxito | Errores |
| :--- | :--- | :--- | :--- |
| `POST /api/reservas/{id}/check-in` | autenticado, titular | `200` con `ReservaDetalle` | `400`, `401`, `403`, `404`, `409` |
| `POST /api/reservas/{id}/check-out` | autenticado, titular | `200` con `ReservaDetalle` | `401`, `403`, `404`, `409` |
| `GET /api/monitoreo/ocupacion-actual` | `Administrativo` | `200` con `OcupacionActual` | `401`, `403` |

```json
// POST /api/reservas/{id}/check-in
{ "codigoQr": "QR-7T3K9M2XQ4ZP8V6BN1C5HJ0WRS" }

// 200 OK (check-in y check-out devuelven el mismo contrato)
{
  "id": "…", "estado": "EnUso",
  "inicio": "2026-11-03T10:00:00-05:00",
  "finPlanificado": "2026-11-03T11:00:00-05:00",
  "fin": "2026-11-03T11:00:00-05:00",
  "fechaCheckIn": "2026-11-03T10:02:00-05:00",
  "fechaCheckOut": null
}
```

El check-out no lleva cuerpo: la única entrada es el identificador de la reserva y la condición es el estado `EnUso`. Tras un check-out a las `10:45`, la respuesta trae `estado: "Finalizada"`, `fechaCheckOut` `10:45`, `fin` `10:45` y `finPlanificado` `11:00`. Los rechazos del check-out usan `409 transicion-invalida` para `Confirmada`, `NoShow`, `Cancelada` y `Finalizada`, y `403 reserva-ajena` si la reserva es de otro usuario.

El mapa devuelve `generadoEn`, `toleranciaMinutos` (leída de `Reservas:VentanaToleranciaCheckInMinutos`), `totales` y `areas[]`; cada área trae `aforo`, `puestosActivos`, `enUso`, `pendientesCheckIn`, `sesionesSinCerrar`, `libres`, `porcentajeOcupacion`, `proximaLiberacion` y `enCurso[]` (`reservaId`, `puestoIdentificador`, `titular`, `inicio`, `fin`, `fechaCheckIn`). `enUso` cuenta solo reservas `EnUso` cuya franja contiene el instante consultado, `sesionesSinCerrar` informa las que ya pasaron su `Fin` sin salida, `libres = puestosActivos - enUso - pendientesCheckIn` y `porcentajeOcupacion = enUso / puestosActivos`.

### 8. Frontend: cámara nativa con degradación explícita y tablero por sondeo

```
features/checkin                         features/administracion/monitoreo
+-----------------------------+          +-------------------------------+
| escaner.page (/checkin/:id) |          | monitoreo.page (polling 15 s) |
|  camara-escaner             |          |  totales-ocupacion            |
|   getUserMedia(environment) |          |  area-tablero  x N            |
|   BarcodeDetector(qr_code)  |          |  monitoreo.store (Signals)    |
|   sin API -> codigo manual  |          |                               |
+--------------+--------------+          +---------------+---------------+
               |                                         |
        POST check-in/out                       GET ocupacion-actual
```

- `BarcodeDetector` no está en todos los navegadores (Firefox/Safari): se detecta con `'BarcodeDetector' in window`; si falta, la pantalla muestra un campo de código y el mismo flujo. Tipado mínimo propio en `models/barcode-detector.d.ts` (sin dependencia npm nueva).
- La cámara exige contexto seguro (HTTPS o `localhost`) y permiso explícito; al salir de la página se detienen las pistas del `MediaStream` con `takeUntilDestroyed`.
- `/checkin/:reservaId` se abre desde la tarjeta de "Mis reservas"; la página muestra la franja, la ventana permitida (calculada con `inicio ± tolerancia`) y, tras el ingreso, el botón de salida siempre habilitado mientras el estado sea `EnUso` (sin cuenta atrás ni bloqueo por hora). Al salir, la pantalla informa el remanente liberado (`fin` acortado) y refresca la disponibilidad.
- El tablero usa `timer(0, 15000)` + `switchMap` y expone `cargando`, `error` y `actualizadoEn`; sin WebSocket (non-goal).
- `reserva-card` traduce los estados nuevos (`EnUso`, `Finalizada`, `NoShow`) y muestra `FechaCheckIn`/`FechaCheckOut`.

## Risks / Trade-offs

- **Barrido detenido retiene franjas ajenas** → recuperación de todo lo vencido (no solo lo reciente), `ILogger` por ciclo, healthcheck del host y tope por lote; prueba de reinicio en mitad de una ventana vencida.
- **Sesión abierta que nadie cierra** → el mapa cuenta `enUso` solo dentro de la franja y reporta `sesionesSinCerrar`, de modo que el administrador la detecta sin que la ocupación quede inflada; el titular puede cerrarla a cualquier hora.
- **Carrera por el remanente liberado** → la transacción serializable del alta y el índice único filtrado garantizan un solo `201`; prueba de integración de dos solicitudes simultáneas sobre `10:45-11:00`.
- **`BarcodeDetector` ausente o permiso denegado** → entrada manual siempre visible y mensaje accionable; prueba de componente con detector simulado y con permiso rechazado.
- **Desfase de relojes** → la ventana se evalúa solo con `IRelojSistema`; el cliente no envía hora.
- **Índice único recreado** → la migración lo elimina y lo crea en la misma transacción; el rollback restaura el filtro `[Estado] = 1` (seguro si no quedan reservas `EnUso`).
- **Lote grande en horas pico** → tope de 200 por ciclo; con 5 minutos de intervalo el atraso se drena sin bloquear la tabla.
- **Tolerancia mal configurada** → `OpcionesReservas.Validar()` falla el arranque si `VentanaToleranciaCheckInMinutos` no está entre 1 y 720; prueba de arranque con `0`.

## Migration Plan

1. Migración `CheckInYNoShow`: añadir `FechaCheckIn` y `FechaCheckOut` (`datetimeoffset NULL`), `FinPlanificado` (`datetimeoffset NOT NULL` rellena con `Fin`), recrear `IX_Reservas_PuestoId_Inicio_Activa` con filtro `[Estado] IN (1, 4)` y crear `IX_Reservas_Estado_Inicio`.
2. Añadir `Reservas:VentanaToleranciaCheckInMinutos` al `appsettings.json` de cada entorno (15 por omisión) y confirmar que el arranque falla con `0`.
3. Desplegar la API con `CheckIn:Habilitado = false`; validar migración y `GET /api/monitoreo/ocupacion-actual` en solo lectura.
4. Activar `CheckIn:Habilitado = true` y verificar un ciclo de barrido en logs (`Liberadas = 0`) antes de abrir el escáner en la SPA.
5. Desplegar la SPA con `/checkin` y `/administracion/monitoreo`.
6. Rollback: desactivar la SPA, poner `Habilitado = false`, revertir la migración (columnas nuevas y filtro `[Estado] = 1`) tras confirmar que no hay reservas `EnUso`; los `Fin` acortados se reconstruyen desde `FinPlanificado` si se requiere el histórico original.
