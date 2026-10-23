# Motor de disponibilidad y préstamo directo de puestos

Documento de la etapa `03-direct-booking-core`: consulta de franjas, reserva autoconfirmada e
inmediata sobre puestos de cómputo, cupo por cuenta y cancelación con antelación.

## Alcance

Incluye:

- Consulta autenticada de la disponibilidad de un puesto por fecha y jornada.
- Reserva autoconfirmada e inmediata de un puesto activo de un área activa con
  `TipoAprobacion = Automatica`.
- Prevención de traslapes entre reservas vigentes, incluso con solicitudes simultáneas.
- Agenda propia del titular y cancelación dentro del plazo configurado.

No incluye: reservas semestrales, aprobaciones con PDF, franjas de mantenimiento,
*check-in* por QR, liberación por inasistencia, sanciones ni notificaciones.

## Configuración

Sección `Reservas` de `appsettings.json`:

| Clave | Valor por defecto | Uso |
| :--- | :--- | :--- |
| `Reservas:AntelacionMinimaMinutos` | `30` | Antelación mínima para reservar |
| `Reservas:AntelacionMaximaDias` | `14` | Horizonte de consulta y alta |
| `Reservas:DuracionMinimaMinutos` | `30` | Duración mínima de una franja |
| `Reservas:DuracionMaximaMinutos` | `240` | Duración máxima de una franja |
| `Reservas:IntervaloMinutos` | `15` | Alineación obligatoria del inicio |
| `Reservas:AntelacionCancelacionMinutos` | `30` | Margen mínimo para cancelar |
| `Reservas:MaxReservasActivasPorUsuario` | `2` | Cupo de reservas confirmadas por cuenta |
| `Reservas:HoraApertura` / `Reservas:HoraCierre` | `07:00` / `21:00` | Jornada institucional |
| `Reservas:ZonaHoraria` | `America/Bogota` | Zona para construir y mostrar franjas |

La API valida esta configuración al arrancar: un rango inválido o una zona horaria
desconocida impiden el inicio en lugar de desplazar las franjas en silencio.

## Endpoints

Todos exigen `Authorization: Bearer <token>` (JWT local o federado). Los errores usan
`application/problem+json` con `type`, `title`, `status`, `detail`, `instance` y `traceId`.

### `GET /api/puestos/{id}/disponibilidad`

Parámetros: `fecha` obligatoria (`yyyy-MM-dd`), `desde`/`hasta` opcionales (`HH:mm`) y
`duracionMinutos` opcional (por omisión la duración mínima).

```powershell
curl.exe -H "Authorization: Bearer $token" `
  "http://localhost:5080/api/puestos/$puestoId/disponibilidad?fecha=2026-11-03&duracionMinutos=60"
```

```json
{
  "puestoId": "…",
  "identificador": "PC-01",
  "espacioId": "…",
  "espacioNombre": "Sala de Cómputo 1",
  "zonaHoraria": "America/Bogota",
  "franjas": [
    { "inicio": "2026-11-03T10:00:00-05:00", "fin": "2026-11-03T11:00:00-05:00", "disponible": true, "motivo": null },
    { "inicio": "2026-11-03T11:00:00-05:00", "fin": "2026-11-03T12:00:00-05:00", "disponible": false, "motivo": "Ocupada" }
  ]
}
```

Estados: `200`, `400` (fecha fuera del horizonte o formato inválido), `401`, `404`
(puesto inexistente), `409` (`puesto-no-reservable`).

### `POST /api/reservas`

```json
{ "puestoId": "…", "inicio": "2026-11-03T10:00:00-05:00", "fin": "2026-11-03T11:00:00-05:00" }
```

Respuesta `201 Created` con `Location: /api/reservas/{id}` y el detalle de la reserva en
estado `Confirmada`. Errores: `400` (duración, alineación, jornada, antelación u horizonte),
`401`, `404`, `409` con `type` `franja-no-disponible`, `puesto-no-reservable` o
`limite-reservas-alcanzado`.

### `GET /api/reservas/me`

Parámetros opcionales: `desde`, `hasta`, `estado` (`Confirmada`/`Cancelada`), `pagina`,
`tamano`. Devuelve `PaginaResultado<ReservaResumen>` ordenado por inicio ascendente; cada
elemento incluye `cancelable` calculado con el margen vigente. Solo devuelve reservas del
titular del token.

### `DELETE /api/reservas/{id}`

Responde `204 No Content`. Errores: `401`, `403` (reserva ajena), `404`, `409` con `type`
`fuera-de-plazo` o conflicto de estado.

## Reglas de negocio

- **Intervalo semiabierto:** la franja es `[inicio, fin)`. Reservas contiguas
  (`08:00-09:00` y `09:00-10:00`) no se consideran traslapadas.
- **Solo préstamo directo:** el puesto y el área deben estar activos y el área debe tener
  aprobación automática; en cualquier otro caso la API responde `409`.
- **Cupo:** máximo `Reservas:MaxReservasActivasPorUsuario` reservas `Confirmada` por cuenta;
  una reserva cancelada libera cupo de inmediato.
- **Cancelación:** solo el titular y solo si faltan al menos
  `Reservas:AntelacionCancelacionMinutos` para el inicio.
- **Zona horaria:** las franjas se generan y devuelven con el desplazamiento de
  `Reservas:ZonaHoraria`; el cliente reenvía el instante recibido.
- **Código QR inmutable:** cancelar cambia el estado de la reserva a `Cancelada`. El
  `CodigoQR` del puesto identifica al recurso físico, no se regenera ni se reasigna.

## Concurrencia

1. Transacción `Serializable` con `SELECT … WITH (UPDLOCK, HOLDLOCK)` sobre la fila del
   puesto, que serializa las altas concurrentes del mismo equipo.
2. Re-verificación de solapamientos con `MotorDisponibilidad.HaySolapamiento` dentro de la
   transacción.
3. Conteo del cupo del titular con el mismo bloqueo serializable.
4. Índice único filtrado `IX_Reservas_PuestoId_Inicio_Activa` (`WHERE [Estado] = 1`) y
   `RowVersion` (`rowversion`) para las transiciones de estado.

## Migración

```powershell
dotnet ef migrations add Reservas --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api
sqlcmd -S "<servidor>" -d ReservasEspaciosDb -E -Q "SELECT name, is_unique, filter_definition FROM sys.indexes WHERE OBJECT_ID('Reservas') = object_id;"
```

Rollback: `dotnet ef database update AreasYPuestos --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api`;
el `Down` elimina solo la tabla `Reservas` y sus índices.

## Pruebas

```powershell
dotnet test tests/Backend.UnitTests/Backend.UnitTests.csproj
dotnet test tests/Backend.IntegrationTests/Backend.IntegrationTests.csproj --filter "FullyQualifiedName~ReservasEndpointsTests"
```

Cubren solapamiento contiguo y parcial, duración, alineación, jornada, horizonte, cupo,
titularidad, cancelación fuera de plazo, dos altas simultáneas sobre la misma franja y la
liberación de la franja tras cancelar.

## Verificación de la SPA (Etapa 2)

```powershell
cd src/frontend
npm run build
npm test
```

Resultado esperado: compilación de producción sin errores y suite de Karma en verde.
Las pruebas cubren el cliente de disponibilidad y reservas, el store con los conflictos
`franja-no-disponible`, `limite-reservas-alcanzado` y `fuera-de-plazo`, la lista de franjas
con las ocupadas deshabilitadas, la confirmación del `201`, la tarjeta de reserva con su
botón de cancelación y el enlace por puesto solo en áreas con aprobación automática.

