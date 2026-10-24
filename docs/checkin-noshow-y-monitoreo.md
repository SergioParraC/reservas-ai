# Check-in con QR, liberación por inasistencia y mapa de ocupación

Documento de la etapa `05-checkin-noshow-and-monitoring`: confirmación de presencia con la placa
QR del puesto, salida anticipada con liberación del remanente, barrido automático de reservas
vencidas y tablero administrativo de ocupación.

## Alcance

Incluye:

- Check-in con el código QR estático del puesto dentro de la ventana de tolerancia configurada.
- Check-out en cualquier instante desde `EnUso`, acortando el fin de la franja.
- Barrido periódico que libera reservas `Confirmada` cuya ventana venció (`NoShow`).
- Mapa de ocupación por área y en totales, con sesiones en uso y sesiones sin cerrar.

No incluye: QR dinámico, geolocalización, notificaciones al ausente, sanciones, tiempo real por
WebSocket ni check-in sobre `ReservaEspacio` (series semestrales y solicitudes aprobadas).

## Estados de la reserva

| Valor | Estado | Ocupa la franja | Transiciones |
| :--- | :--- | :--- | :--- |
| `1` | `Confirmada` | Sí | `EnUso` (check-in), `Cancelada`, `NoShow` |
| `2` | `Cancelada` | No | terminal |
| `3` | `NoShow` | No | terminal (barrido) |
| `4` | `EnUso` | Sí | `Finalizada` (check-out) |
| `5` | `Finalizada` | No | terminal |

`FinPlanificado` se fija en el alta y nunca cambia. `Fin` se acorta al registrar la salida
(`max(inicio, min(finPlanificado, ahora))`) y jamás se extiende.

## Configuración

`src/Backend.Presentation.Api/appsettings.json`:

```json
"Reservas": {
  "VentanaToleranciaCheckInMinutos": 15
},
"CheckIn": {
  "MinutosIntervaloBarrido": 5,
  "MaximoReservasPorBarrido": 200,
  "Habilitado": true
}
```

| Clave | Valor por defecto | Uso |
| :--- | :--- | :--- |
| `Reservas:VentanaToleranciaCheckInMinutos` | `15` | Ventana simétrica `inicio ± tol` del check-in; única fuente (HTTP y barrido) |
| `CheckIn:MinutosIntervaloBarrido` | `5` | Cadencia del barrido de inasistencias |
| `CheckIn:MaximoReservasPorBarrido` | `200` | Tope de reservas liberadas por ciclo |
| `CheckIn:Habilitado` | `true` | Interruptor del `BackgroundService` |

`OpcionesReservas.Validar()` falla el arranque si la tolerancia no está entre 1 y 720 minutos;
`OpcionesCheckIn.Validar()` exige una cadencia de 1 a 1440 minutos y un lote de 1 a 1000.

La SPA replica el valor en `src/frontend/src/environments/environment*.ts`
(`checkIn.ventanaToleranciaMinutos`) solo para dibujar la ventana admitida: la API valida siempre
con su propia configuración.

## Endpoints

Todos exigen `Authorization: Bearer <token>`; los errores usan `application/problem+json` con
`type`, `title`, `status`, `detail`, `instance` y `traceId`.

### `POST /api/reservas/{id}/check-in`

```json
{ "codigoQr": "QR-7T3K9M2XQ4ZP8V6BN1C5HJ0WRS" }
```

```powershell
curl.exe -X POST -H "Authorization: Bearer $token" -H "Content-Type: application/json" `
  -d '{\"codigoQr\":\"QR-7T3K9M2XQ4ZP8V6BN1C5HJ0WRS\"}' `
  "http://localhost:5080/api/reservas/$reservaId/check-in"
```

`200 OK` con el `ReservaDetalle` en estado `EnUso`:

```json
{
  "id": "…", "estado": 4,
  "inicio": "2026-11-03T10:00:00-05:00",
  "fin": "2026-11-03T11:00:00-05:00",
  "finPlanificado": "2026-11-03T11:00:00-05:00",
  "fechaCheckIn": "2026-11-03T10:02:00-05:00",
  "fechaCheckOut": null
}
```

Errores: `400` (falta `codigoQr`), `401`, `403` (`reserva-ajena`), `404`
(`recurso-no-encontrado`), `409` con `type` `check-in-fuera-de-ventana` (el `detail` indica el
rango admitido), `check-in-codigo-invalido` o `transicion-invalida`.

### `POST /api/reservas/{id}/check-out`

Sin cuerpo. Exige estado `EnUso` y no aplica ventana horaria. `200 OK` con `estado` `5`,
`fechaCheckOut` y `fin` acortado; `finPlanificado` conserva el valor pactado.

```powershell
curl.exe -X POST -H "Authorization: Bearer $token" "http://localhost:5080/api/reservas/$reservaId/check-out"
```

Errores: `401`, `403` (`reserva-ajena`), `404`, `409` (`transicion-invalida`).

### `GET /api/monitoreo/ocupacion-actual`

Rol `Administrativo`. Devuelve `generadoEn`, `toleranciaMinutos`, `totales` y `areas[]` con
`aforo`, `puestosActivos`, `enUso`, `pendientesCheckIn`, `sesionesSinCerrar`, `libres`,
`porcentajeOcupacion`, `proximaLiberacion` y `enCurso[]`.

```powershell
curl.exe -H "Authorization: Bearer $tokenAdmin" "http://localhost:5080/api/monitoreo/ocupacion-actual"
```

Reglas del cálculo: `enUso` cuenta solo reservas `EnUso` cuya franja contiene el instante
consultado; `pendientesCheckIn` las `Confirmada` dentro de su ventana; `sesionesSinCerrar` las
`EnUso` que ya pasaron su `Fin`; `libres = puestosActivos - enUso - pendientesCheckIn` (nunca
negativo). Las áreas sin puestos activos aparecen con ceros.

Errores: `401` sin sesión, `403` con rol distinto de `Administrativo` (RFC 7807).

## Flujo de escaneo y salida (SPA)

1. En **Mis reservas**, la tarjeta de una reserva `Confirmada` ofrece **Escanear para ingresar** y
   navega a `/checkin/{reservaId}`.
2. La pantalla muestra la franja, el fin planificado y la ventana admitida (`inicio ± tolerancia`).
3. El componente `camara-escaner` pide la cámara trasera con
   `getUserMedia({ video: { facingMode: 'environment' } })` y lee con `BarcodeDetector('qr_code')`.
   - Si el navegador no expone `BarcodeDetector`, si el contexto no es seguro o si el permiso se
     deniega, se explica el motivo y el campo de **código manual** sigue disponible.
   - Al salir de la página se detienen las pistas del `MediaStream` y se cancela el bucle de
     lectura con `takeUntilDestroyed`.
4. El código se envía al servidor; la comparación con el QR del puesto ocurre solo en la API.
5. Con la reserva en `EnUso` la página muestra **Registrar salida** siempre habilitado (sin cuenta
   atrás ni ventana horaria) y, tras la salida, el aviso del remanente liberado con enlace a la
   disponibilidad del puesto.
6. Los rechazos se traducen a mensajes accionables: el interceptor RFC 7807 conoce
   `check-in-fuera-de-ventana`, `check-in-codigo-invalido` y `reserva-ajena`; el store del
   check-in usa además el `detail` del servidor para `transicion-invalida`.

El tablero **Administración → Monitoreo** sondea `GET /api/monitoreo/ocupacion-actual` con
`timer(0, 15000)` + `switchMap`, expone `cargando`, `error` y `actualizadoEn`, y pinta por área la
leyenda **Libre / En uso / Pendiente / No-show**, la barra de ocupación y las sesiones en curso.

## Persistencia y migración

Migración `CheckInYNoShow` (`src/Backend.Infrastructure/Persistence/Migrations`):

- `Reservas.FechaCheckIn` y `Reservas.FechaCheckOut`: `datetimeoffset NULL`.
- `Reservas.FinPlanificado`: `datetimeoffset NOT NULL`; las filas existentes se rellenan con `Fin`
  antes de aplicar la restricción.
- `IX_Reservas_PuestoId_Inicio_Activa` se recrea con filtro `[Estado] IN (1, 4)`.
- Nuevo índice `IX_Reservas_Estado_Inicio` para el barrido.

```powershell
dotnet ef migrations add CheckInYNoShow --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api
dotnet ef database update --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api
sqlcmd -S "<servidor>" -d ReservasEspaciosDb -E -Q "SELECT name, is_unique, filter_definition FROM sys.indexes WHERE object_id = OBJECT_ID('Reservas');"
```

Rollback: `dotnet ef database update CalendarioInstitucionalYModalidad --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api`
(el `Down` restaura el filtro `[Estado] = 1`, seguro si no quedan reservas `EnUso`).

## Pruebas

```powershell
dotnet test src/Backend.sln
cd src/frontend
npm test
npm run build
openspec validate 05-checkin-noshow-and-monitoring --strict
```

Cubren: mapeo numérico de estados y `EstaVigente`; ventana con tolerancia 10/15/20 y transiciones
inválidas; handlers de check-in/check-out con `200/400/401/403/404/409`; barrido idempotente,
sesión en uso intacta y lote fallido; servicio en segundo plano (primer ciclo inmediato, fallo que
no detiene el host, `Habilitado = false`); agregación del mapa; endpoints con RFC 7807; índice
único con `(1, 4)` y concurrencia sobre una franja en uso y sobre el remanente `10:45-11:00`; SPA
con escáner simulado, API ausente, permiso denegado, store del tablero y rutas protegidas.

## Humo integral

Secuencia verificada contra la API local sobre SQL Server en contenedor:

1. Alta de área y puesto, activación del periodo académico y reserva de una franja.
2. `POST /check-in` con el QR del puesto → `EnUso` y `GET /api/monitoreo/ocupacion-actual` con
   `enUso` 1.
3. `POST /check-out` → `Finalizada`, `fin` acortado y franja disponible otra vez.
4. Barrido forzado (`CheckIn:MinutosIntervaloBarrido` corto o reinicio del host con
   `CheckIn:Habilitado = true`) sobre una reserva vencida sembrada → `NoShow`.
5. Mapa final sin ocupación y con la franja libre.



