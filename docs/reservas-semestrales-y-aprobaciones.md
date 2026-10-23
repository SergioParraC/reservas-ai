# Reservas semestrales, calendario institucional y aprobaciones

Documento del cambio `04-semester-bookings-and-approvals`: series semestrales de área completa,
calendario institucional (periodo académico y franjas no operativas), solicitudes con pre-informe
PDF, bandeja de aprobaciones y notificación por correo.

## Alcance

Incluye:

- Administración del periodo académico vigente y de las franjas no operativas globales.
- Series semestrales (`Semanal`, `Bisemanal`, `FechasFijas`) sobre áreas con modalidad
  `AreaCompleta`, con omisión de los días no operativos y rechazo íntegro ante cualquier colisión.
- Radicación de solicitudes con pre-informe PDF en el volumen privado, descarga restringida al
  autor y a los revisores competentes.
- Bandeja de aprobaciones filtrada por competencia y transiciones auditables de estado con motivo
  obligatorio de rechazo.
- Notificación HTML encolada en la misma transacción de la decisión y despachada en segundo plano
  con reintentos y retroceso exponencial.
- Parametrización de modalidad (`AreaCompleta`, `PuestoIndividual`, `SubZonaCompartida`) y elemento
  (`EquipoComputo`, `LaboratorioEspecializado`, `ZonaDescansoJuegos`, `Auditorio`) por área.

No incluye: cancelación o reprogramación de series, franjas de mantenimiento por área, aprobación
parcial, edición del pre-informe después de radicado, recordatorios programados ni *check-in* por QR.

## Reglas del calendario institucional

1. Solo puede existir **un periodo académico activo**. Activarlo desactiva al vigente en la misma
   transacción serializable y `IX_PeriodosAcademicos_Activo` (único filtrado sobre `EstadoActivo = 1`)
   impide dos activos incluso ante escrituras concurrentes.
2. Los periodos no se solapan: `POST` y `PUT /api/periodos-academicos` responden `409 periodo-solapado`.
3. Toda reserva, serie o solicitud debe quedar **completamente** dentro de la ventana del periodo
   activo; de lo contrario responde `409 fuera-de-periodo-academico` o `409 sin-periodo-activo`.
4. Las franjas no operativas aplican a **todo el campus** y se definen por día de la semana o por
   fecha específica, nunca por ambos criterios. `00:00`–`23:59` representa el día completo.
5. Una ocurrencia **totalmente** cubierta por un bloqueo se omite; una cubierta **parcialmente**
   rechaza la operación con `409 franja-no-operativa` indicando la fecha y el motivo.
6. Editar las fechas de un periodo que dejaría reservas, series o solicitudes fuera de la nueva
   ventana responde `409 periodo-con-ocupacion`.
7. La consulta pública de disponibilidad informa `FueraDePeriodoAcademico` y `FueraDeOperacion`
   antes que la ocupación, de modo que la SPA deshabilita las franjas no operativas.

### Matriz de modalidad y elemento de préstamo

| Modalidad | Elementos admitidos | Tipo de reserva |
| :--- | :--- | :--- |
| `AreaCompleta` | `LaboratorioEspecializado`, `Auditorio`, `ZonaDescansoJuegos` | Series y solicitudes |
| `PuestoIndividual` | `EquipoComputo` | Préstamo directo de puestos |
| `SubZonaCompartida` | `EquipoComputo`, `ZonaDescansoJuegos` | Préstamo directo de puestos |

- Una combinación incoherente responde `400` señalando `tipoElementoPrestamo`.
- `POST /api/reservas` sobre un puesto de un área `AreaCompleta` responde `409 modalidad-incompatible`.
- Una serie o solicitud sobre un área que no es `AreaCompleta` responde `409 modalidad-incompatible`.
- Cambiar la modalidad de un área con ocupación futura responde `409 modalidad-con-ocupacion`; el
  cambio sin ocupación conserva el código QR del área y de sus puestos.

## Endpoints

Todos exigen `Authorization: Bearer <token>`. Los errores usan `application/problem+json` con `type`
estable; `traceId` viaja en `extensions`.

### Calendario (rol `Administrativo`, salvo la consulta vigente)

| Método y ruta | Uso | Respuestas |
| :--- | :--- | :--- |
| `GET /api/periodos-academicos` | Lista periodos activos e inactivos | `200` |
| `POST /api/periodos-academicos` | Registra un periodo inactivo | `201`, `400`, `403`, `409 periodo-solapado` |
| `PUT /api/periodos-academicos/{id}` | Edita nombre y rango | `200`, `400`, `404`, `409 periodo-solapado`, `409 periodo-con-ocupacion` |
| `PUT /api/periodos-academicos/{id}/activar` | Activa y desactiva al vigente | `200`, `404`, `409` |
| `GET /api/franjas-no-operativas` | Lista franjas vigentes | `200` |
| `POST /api/franjas-no-operativas` | Registra una franja global | `201`, `400`, `409 franja-duplicada` |
| `PUT /api/franjas-no-operativas/{id}` | Edita una franja | `200`, `400`, `404`, `409 franja-duplicada` |
| `DELETE /api/franjas-no-operativas/{id}` | Baja lógica | `204`, `404` |
| `GET /api/calendario/vigente` | Periodo activo y franjas (cualquier rol) | `200`, `401` |

### Series semestrales (rol `Docente`)

| Método y ruta | Uso | Respuestas |
| :--- | :--- | :--- |
| `POST /api/reservas/recurrentes` | Radica la serie completa | `201`, `400`, `403`, `404`, `409 colision-serie`, `409 franja-no-operativa`, `409 fuera-de-periodo-academico`, `409 modalidad-incompatible` |
| `GET /api/reservas/recurrentes/me` | Series propias, más reciente primero | `200`, `401` |

Cuerpo de alta:

```json
{
  "espacioId": "6f1c1f2e-...",
  "tipoRecurrencia": "Semanal",
  "diaSemana": "Tuesday",
  "horaInicio": "14:00",
  "horaFin": "16:00",
  "periodoInicio": "2026-08-04",
  "periodoFin": "2026-11-24",
  "materia": "Cálculo II"
}
```

`fechasFijas` (`["2026-09-07", "2026-10-19"]`) sustituye a `diaSemana` cuando
`tipoRecurrencia = "FechasFijas"`. La respuesta incluye `totalOcurrencias` y cada ocurrencia con
`omitida = true` cuando el calendario la excluyó.

### Solicitudes y bandeja

| Método y ruta | Roles | Uso | Respuestas |
| :--- | :--- | :--- | :--- |
| `POST /api/solicitudes` | `Estudiante`, `Docente` | Radica con pre-informe (`multipart/form-data`) | `201`, `400`, `401`, `404`, `409`, `413 archivo-demasiado-grande`, `415 archivo-no-valido` |
| `GET /api/solicitudes/me` | `Estudiante`, `Docente` | Historial propio | `200`, `400`, `401` |
| `GET /api/solicitudes/{id}/preinforme` | Autor o revisor competente | Descarga el PDF sin caché | `200`, `401`, `403 solicitud-ajena`, `404` |
| `GET /api/bandeja-aprobaciones` | `Docente`, `Administrativo` | Bandeja por competencia con filtros | `200`, `400`, `401`, `403` |
| `PUT /api/solicitudes/{id}/estado` | `Docente`, `Administrativo` | `EnRevision`, `Aprobado` o `Rechazado` | `200`, `400`, `403`, `404`, `409 transicion-invalida`, `409 franja-no-disponible` |

Partes del `multipart/form-data`: `espacioId`, `inicio`, `fin` (ISO 8601 con desplazamiento),
`materia`, `docenteResponsable`, `justificacion` y `archivo` (PDF de hasta 5 MB). El servidor genera
el nombre del archivo y guarda solo la ruta relativa en la base de datos.

Filtros de la bandeja: `estado`, `espacioId`, `desde`, `hasta`, `pagina` y `tamano`. El docente solo
ve áreas con `TipoAprobacion = Docente`; el administrativo ve todas y nunca puede resolver su propia
solicitud.

## Configuración

| Sección | Clave | Valor por defecto | Uso |
| :--- | :--- | :--- | :--- |
| `Solicitudes` | `TamanoMaximoPdfBytes` | `5242880` | Tamaño máximo del pre-informe |
| `Solicitudes` | `MaxSolicitudesActivasPorUsuario` | `3` | Cupo en `Radicado`/`EnRevision` |
| `Solicitudes` | `LongitudMinimaMotivoRechazo` | `10` | Mínimo del motivo de rechazo |
| `Solicitudes` | `AntelacionMinimaMinutos` | `30` | Antelación para radicar |
| `Semestre` | `SemanasMaximasPorSerie` | `20` | Tope de la ventana semestral |
| `Semestre` | `AntelacionMinimaDias` | `3` | Antelación de la primera ocurrencia |
| `Calendario` | `PeriodoActivoRequerido` | `true` | Exigir periodo activo |
| `Calendario` | `MinutosCacheCalendario` | `1` | TTL de la caché del calendario |
| `Calendario` | `FeriadosNacionales` | `[]` | Feriados opcionales (`yyyy-MM-dd`) |
| `Smtp` | `Host` / `Puerto` / `UsarSsl` | `localhost` / `1025` / `false` | Servidor SMTP |
| `Smtp` | `Usuario` / `Contrasena` | vacío | Autenticación opcional |
| `Smtp` | `RemitenteCorreo` / `RemitenteNombre` | institucional | Remitente |
| `Smtp` | `Habilitado` | `false` | Interruptor del despachador |
| `Smtp` | `IntervaloDespachoSegundos` | `15` | Cadencia del barrido |

El arranque falla con `InvalidOperationException` si `Calendario:MinutosCacheCalendario` es negativo,
`Smtp:Puerto` está fuera de `1..65535`, `Smtp:IntervaloDespachoSegundos` es menor que 1,
`Solicitudes:LongitudMinimaMotivoRechazo` es menor que 10 o `Semestre:SemanasMaximasPorSerie` está
fuera de `1..52`.

En desarrollo `docker compose up` levanta **MailHog** (`http://localhost:8025`) y la API con
`Smtp:Habilitado = true` apuntando a `mailhog:1025`.

## Persistencia y migraciones

Tablas nuevas: `PeriodosAcademicos`, `FranjasNoOperativas`, `ReservasRecurrentes`,
`ReservasEspacio`, `SolicitudesPrestamo` y `CorreosPendientes`. Columnas nuevas en `Espacios`:
`TipoModalidadPrestamo` y `TipoElementoPrestamo`.

```powershell
dotnet ef migrations add ReservasSemestralesYAprobaciones --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api --output-dir Persistence/Migrations
dotnet ef migrations add CalendarioInstitucionalYModalidad --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Backend.Infrastructure --startup-project src/Backend.Presentation.Api
```

La segunda migración rellena las áreas existentes con una heurística: `SalaComputo` pasa a
`PuestoIndividual/EquipoComputo`, `Auditorio` a `AreaCompleta/Auditorio`, `Laboratorio` a
`AreaCompleta/LaboratorioEspecializado`, `Deportivo` a `SubZonaCompartida/ZonaDescansoJuegos` y
cualquier área con puestos activos a `PuestoIndividual/EquipoComputo`.

Índices que sostienen las garantías:

- `IX_PeriodosAcademicos_Activo`: único filtrado por `EstadoActivo = 1`.
- `IX_ReservasEspacio_EspacioId_Inicio_Activa`: único filtrado por `Estado = 1`.
- `IX_ReservasRecurrentes_DocenteId_CreadaEn` y `IX_CorreosPendientes_Estado_ProximoIntento`.

## Concurrencia

- **Series**: transacción `Serializable` con `UPDLOCK, HOLDLOCK` sobre la fila del área, re-verificación
  del barrido de colisiones y escritura de todas las ocurrencias. Dos series simultáneas dejan
  exactamente una confirmada.
- **Aprobaciones**: la decisión, la ocupación del área y el correo se confirman en una sola
  transacción con el mismo bloqueo; una segunda aprobación sobre la franja responde
  `409 franja-no-disponible` y la solicitud permanece `EnRevision`.
- **Periodos**: la activación bloquea la fila del periodo y desactiva al vigente antes de confirmar.
- **Correo**: la respuesta HTTP no depende del SMTP. Un fallo incrementa `Intentos`, programa el
  siguiente intento con retroceso exponencial y conserva la decisión del revisor.

## Verificación

```powershell
dotnet build src/Backend.sln
dotnet test tests/Backend.UnitTests
dotnet test tests/Backend.ArchitectureTests
dotnet test tests/Backend.IntegrationTests
```

Humo integral con MailHog: activar un periodo → registrar una franja no operativa → radicar una serie
que la omita → provocar una colisión → radicar una solicitud con PDF → abrir la bandeja → aprobar →
comprobar el correo en `http://localhost:8025` → intentar una reserva fuera del periodo (`409`).

## Humo integral del bloque de interfaz

Ejecutado el 23/10/2026 contra la API local (`http://localhost:5085`, SQLEXPRESS) y MailHog
(`http://localhost:8025`), con un usuario administrativo y uno docente creados para la prueba.

| Paso | Resultado |
| --- | --- |
| Crear y activar el periodo `2026-2` (01/08/2026 – 15/12/2026) | `201` + `estadoActivo=true` |
| Registrar la franja no operativa del sábado de 13:00 a 21:00 | `201`, `diaSemana=6` |
| Crear un área completa de auditorio con aprobación docente | `201`, modalidad `AreaCompleta` |
| Serie semanal de martes 14:00–16:00 | `201` con 7 ocurrencias |
| Serie solapada de martes 15:00–17:00 | `409 colision-serie` (3 ocurrencias) |
| Serie de sábado 14:00–16:00 | `400` sin ocurrencias operativas (franja no operativa) |
| Radicar una solicitud con pre-informe PDF | `201`, estado `Radicado`, PDF de 68 bytes |
| Bandeja del administrativo | `1` solicitud visible |
| Tomar en revisión y aprobar | estados `2` y `3` con revisor asignado |
| Correos en MailHog | `2` mensajes: «en revisión» y «fue aprobada» al correo del docente |

Verificación del cliente Angular:

```powershell
npm --prefix src/frontend test   # 132 pruebas, 0 fallos
npm --prefix src/frontend run build
```

Las guardas de rol y de sesión se cubren con `role.guard.spec.ts`, `auth.guard.spec.ts` y las pruebas
de componente de cada módulo; la navegación por rol se valida en `shared/shell` y en el panel de
administración.



