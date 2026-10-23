# Design

## Context

Ver `proposal.md` — Why. Estado actual relevante:

- `Backend.Domain` ya expone `Reserva` (por puesto), `FranjaHoraria` con semántica `[inicio, fin)`, `PoliticaReserva` y `MotorDisponibilidad.GenerarFranjas` / `HaySolapamiento`; no existe ninguna noción de recurrencia ni de área ocupada.
- `Backend.Application` trabaja con puertos (`IReservasRepository`, `IConsultaReservas`, `IAlmacenArchivos`, `IRelojSistema`, `IUsuarioActual`) y `Resultado<T>`; la prueba de arquitectura prohíbe EF Core, ASP.NET Core y `Microsoft.Extensions` en esa capa.
- `Backend.Infrastructure` ya implementa almacenamiento local en `Almacen:RutaBase` (`/var/lib/reservas`, volumen `media`), sirve solo `espacios/` bajo `/media` y usa SQL Server 2022 con `RowVersion`.
- `Espacio` es raíz de agregado con `TipoAprobacion` (`Automatica`, `Docente`, `Administrativo`) y `Estado`, y sus puestos cuelgan del mismo agregado; no existe ninguna noción de modalidad de préstamo ni de elemento prestado.
- La única referencia temporal es la configuración de jornada (`HoraApertura`/`HoraCierre`); no hay periodo académico ni calendario de no operación persistidos.
- La SPA usa Angular 20 con Standalone Components, Signals, `authGuard`, `roleGuard`, `jwt.interceptor` y `problem-details.interceptor`; `features/solicitudes-estudiante` ya cubre el préstamo directo y `features/administracion` agrupa las pantallas administrativas.

Restricciones que condicionan el diseño:

- El dominio sigue en C# puro: sin `TimeZoneInfo` desde DI, sin ORM y sin `Microsoft.Extensions`.
- La aplicación no puede hospedar `BackgroundService` ni MailKit: el despacho de correo vive en `Backend.Infrastructure`.
- `IApplicationDbContext` solo expone `SaveChangesAsync`; toda lectura y bloqueo nuevos entran por puertos.
- Un área completa y sus puestos comparten espacio físico: la agenda del área y la agenda de puestos deben consultarse juntas al validar colisiones.
- SQL Server no almacena binarios: solo metadatos y ruta relativa del PDF.

## Goals / Non-Goals

**Goals:**

- Una serie semestral que se genera y valida como un todo: si una ocurrencia colisiona, no se persiste ninguna.
- Detección de colisiones en tiempo casi lineal sobre el semestre completo, no N comparaciones por ocurrencia.
- Una única agenda de áreas consultable que reúna ocurrencias semestrales, solicitudes aprobadas y reservas puntuales de los puestos del área.
- Un flujo de solicitud con PDF validado por tamaño y firma, estado auditable y decisión restringida por competencia.
- Notificación por correo desacoplada del ciclo HTTP, con reintentos y sin perder la decisión si el SMTP falla.
- Un calendario institucional persistido (periodo académico activo y franjas no operativas) que gobierne toda reserva, sin depender de reiniciar la API.
- Una parametrización por área que distinga el recinto integral de sus sub-elementos y determine qué tipo de préstamo se admite.
- Cinco pantallas Angular autocontenidas (series, bandeja, radicación, periodos y franjas), sin dependencias npm nuevas.

**Non-Goals:**

- Cancelar, reprogramar o aprobar parcialmente una serie; la serie es atómica y de solo alta.
- Franjas de mantenimiento por área o bloqueos de un recurso concreto, *check-in* por QR, *no-show* y sanciones.
- Más de un periodo académico activo simultáneo o periodos solapados.
- Excepciones por área al calendario global: las franjas no operativas aplican a todo el campus.
- Edición del pre-informe después de radicado, versionado de adjuntos o firma digital.
- Antivirus o conversión de PDF: se valida tamaño y firma, no contenido malicioso.
- Notificaciones al docente, recordatorios programados o plantillas editables por el usuario.
- Sincronización en tiempo real de la agenda: la frescura se obtiene al consultar.

## Decisions

### 1. `ReservaRecurrente` como cabecera de serie y `ReservaEspacio` como agenda única del área

```
ReservaRecurrente (raiz de agregado)
+-------------------------------------------------------------+
| Id  EspacioId  DocenteId                                    |
| Patron : PatronRecurrencia (VO)                             |
|    Tipo: Semanal | Bisemanal | FechasFijas                  |
|    DiaSemana?  FechasFijas?  HoraInicio  HoraFin            |
| Periodo : PeriodoSemestral (VO: Inicio, Fin)                |
| CreadaEn  RowVersion                                        |
+-------------------------------------------------------------+
        | GenerarOcurrencias(patron, periodo, feriados, zona)
        v
  FranjaHoraria[]  --->  ReservaEspacio (agenda del area)
+-------------------------------------------------------------+
| Id EspacioId TitularId                                      |
| Origen: Recurrente | Solicitud   OrigenId                   |
| Inicio  Fin (datetimeoffset)   Estado: Activa | Cancelada   |
| CreadaEn  RowVersion                                        |
+-------------------------------------------------------------+
        ^                                   ^
        | aprobacion de solicitud           | lectura de colisiones
        |                                   |
  SolicitudPrestamo                 MotorDisponibilidad
```

Motivo: la cabecera conserva el patrón y el periodo (lo que el docente declaró) y las ocurrencias se materializan como filas de agenda, de modo que una serie, una solicitud aprobada y una reserva puntual compiten por el mismo recurso con un solo índice y una sola consulta. Guardar las ocurrencias dentro del agregado como colección propia obligaría a dos fuentes de ocupación y a duplicar el barrido.

Alternativa descartada: extender `Reserva` con un `EspacioId` opcional. Rompería las invariantes del préstamo directo (puesto obligatorio, cupo por usuario, cancelación por titular) y obligaría a modificar la capacidad `reservas/prestamo-directo`.

### 2. `PatronRecurrencia` genera fechas; el motor solo conoce franjas

```
Semanal     : primera fecha con el diaSemana >= Periodo.Inicio, paso 7 dias
Bisemanal   : misma ancla, paso 14 dias
FechasFijas : lista ordenada y sin duplicados del cuerpo de la solicitud

para cada fecha:
  si fuera de [Periodo.Inicio, Periodo.Fin]            -> 400 fecha-fuera-de-periodo
  si la franja cae completa en una FranjaNoOperativa   -> omitir (no es error)
  si la franja se cruza parcialmente con una FranjaNoOperativa -> 409 franja-no-operativa
  franja = FranjaHoraria.Desde(fecha + HoraInicio, fecha + HoraFin)
  si ocurrencias.Count = Semestre:SemanasMaximas       -> 400 periodo-excedido

si ocurrencias.Count = 0                               -> 400 serie-sin-ocurrencias
```

La generación es una función pura del dominio (`ReservaRecurrente.GenerarOcurrencias`) que recibe zona horaria, los bloqueos del calendario institucional ya proyectados a intervalos y el máximo de semanas; el motor de colisiones nunca ve el patrón. Ventaja: la vista previa del frontend y la validación del backend usan el mismo algoritmo, y las pruebas no necesitan base de datos. El ancla bisemanal es la primera ocurrencia, no el lunes de la semana ISO, para que el docente controle su calendario.

`PeriodoSemestral` acota el rango y `FranjaHoraria.Desde` reutiliza la semántica semiabierta ya probada en el préstamo directo; la antelación mínima y la vigencia del periodo académico se validan una sola vez contra la primera ocurrencia y contra el rango completo.

### 3. Detección masiva de colisiones por barrido ordenado

`MotorDisponibilidad.DetectarColisiones` recibe las ocurrencias candidatas y la ocupación vigente del área en el mismo rango y devuelve solo las que chocan:

```
entrada : candidatas[n], ocupadas[m]
1. bloques <- fusionar(ordenar(ocupadas por Inicio))   // intervalos solapados o contiguos
2. orden    <- ordenar(candidatas por Inicio)
3. i = 0; j = 0
4. mientras i < n y j < k:
     si orden[i].Fin    <= bloques[j].Inicio -> i++
     sino si bloques[j].Fin <= orden[i].Inicio -> j++
     sino -> colisiones += orden[i]; i++
5. devolver colisiones (máximo MaximoColisionesReportadas = 20)
```

Complejidad `O((n + m) log(n + m))` en tiempo y `O(n + m)` en memoria, frente a `O(n x m)` del doble bucle. Con 20 semanas y una ocupación acotada por área, el barrido resuelve el semestre completo en una sola pasada; la fusión de bloques evita recorrer ocupaciones repetidas cuando varias series comparten horario.

La ocupación se lee en una sola consulta a `IConsultaOcupacionEspacio.ObtenerAsync(espacioId, desde, hasta)` con `.AsNoTracking()` y proyección `IntervaloOcupado`, que une:

1. `ReservasEspacio` activas (`Origen = Recurrente | Solicitud`),
2. `Reservas` confirmadas de todos los puestos del área (un laboratorio reservado no puede tener puestos ocupados en la misma franja).

`HaySolapamiento` se conserva para la validación puntual; el barrido es el camino de la serie.

### 3-bis. El calendario se evalúa antes que la agenda

`MotorDisponibilidad` recibe dos colecciones distintas y las trata por separado para poder informar la causa:

```
EvaluarCalendario(franja, ventanaAcademica, bloqueos) -> MotivoCalendario?
   si ventanaAcademica es null                        -> SinPeriodoActivo
   si franja.Inicio < ventana.Inicio o franja.Fin > ventana.Fin -> FueraDePeriodo
   si franja cae completa o parcialmente en un bloqueo -> NoOperativa (con la franja bloqueada)

DetectarColisiones(candidatas, ocupadas)  -> colisiones de agenda (409 colision-serie)
```

Reglas de composición:

1. Una ocurrencia **totalmente** cubierta por un bloqueo no operativo se omite al generar la serie (domingo, feriado de día completo).
2. Una ocurrencia **parcialmente** cubierta por un bloqueo se rechaza con `409 franja-no-operativa`, porque acortar la clase cambiaría lo que el docente pidió.
3. Si tras omitir no queda ninguna ocurrencia, responde `400 serie-sin-ocurrencias`.
4. La consulta de disponibilidad marca cada franja con `fuera-de-operacion` o `fuera-de-periodo-academico`, y las reservas puntuales y solicitudes rechazan con `409`.
5. El orden es siempre calendario → agenda → cupo, de modo que un mensaje nunca atribuya a una colisión lo que en realidad es un cierre institucional.

### 4. Confirmación de la serie en una transacción serializable

```
POST /api/reservas/recurrentes
  |
  +-> 1. Autorizacion (rol Docente) y zona/politica desde OpcionesReservas
  +-> 2. GenerarOcurrencias (puro, sin BD)
  +-> 3. Serializar colisiones (puro) contra la agenda leida en 4
  |
  +--> TRANSACCION Serializable
         +-- 3a. SELECT Espacio WITH (UPDLOCK, HOLDLOCK)  // serializa por area
         +-- 3b. IConsultaOcupacionEspacio.ObtenerAsync(espacioId, primera, ultima)
         +-- 3c. DetectarColisiones -> si hay, ROLLBACK + 409 colision-serie
         +-- 3d. INSERT ReservaRecurrente + N ReservaEspacio (una transaccion)
         +-- 3e. COMMIT; si el indice unico filtrado rechaza -> reintento unico -> 409
  |
  +-> 5. 201 Created con serie y resumen de ocurrencias
```

El bloqueo de la fila del área (`UPDLOCK, HOLDLOCK`) serializa las altas de serie de una misma área sin bloquear consultas; la re-verificación dentro de la transacción cubre el caso de una reserva puntual creada entre la lectura y la escritura; el índice único filtrado `IX_ReservasEspacio_EspacioId_Inicio_Activa` (`WHERE [Estado] = 1`) es la última barrera ante procesos concurrentes. Se aplica el mismo patrón ya validado en `ReservasRepository.GuardarSiDisponibleAsync`.

El resultado del puerto es explícito: `Guardada`, `Colision(ocurrencias)`, `Conflicto`; la aplicación traduce a `201`, `409 colision-serie` y `409` genérico sin conocer SQL Server.

### 5. `SolicitudPrestamo` con máquina de estados explícita

```
                        tomar                  aprobar
   [Radicado] -------------------> [EnRevision] -----------> [Aprobado]
       |                                  |
       |                                  | rechazar(motivo)
       |                                  v
       |                            [Rechazado]
       |
       +-- decidir sin tomar --> 409 transicion-invalida
       +-- cambiar desde un estado final --> 409 transicion-invalida
```

```
SolicitudPrestamo (raiz de agregado)
+----------------------------------------------------------------+
| Id  SolicitanteId  EspacioId  (TipoAprobacion leido del area)  |
| Inicio  Fin  Materia  DocenteResponsable  Justificacion        |
| ArchivoRuta  ArchivoNombreOriginal  ArchivoTamanoBytes         |
| Estado : EstadoSolicitud                                       |
| RadicadaEn  RevisadaEn?  RevisorId?  MotivoRechazo?            |
| RowVersion                                                     |
+----------------------------------------------------------------+
```

Reglas del agregado: `TomarEnRevision(revisorId, momento)` solo desde `Radicado`; `Aprobar(revisorId, momento)` y `Rechazar(revisorId, motivo, momento)` solo desde `EnRevision`; `Rechazado` exige motivo de 10 a 500 caracteres; ambos estados finales son inmutables. `EstadoSolicitud` se persiste como `int` (`Radicado = 1`, `EnRevision = 2`, `Aprobado = 3`, `Rechazado = 4`), coherente con `EstadoReserva`.

La competencia del revisor no vive en el agregado (depende del área): el handler `CambiarEstadoSolicitudHandler` la resuelve con `IConsultaSolicitudes` comparando `TipoAprobacion` con el rol efectivo y rechaza al solicitante como revisor de su propia solicitud. Aprobar crea la fila `ReservaEspacio` con `Origen = Solicitud` en la misma transacción y con la misma re-verificación de colisiones que una serie de una sola ocurrencia.

### 6. `FileStorageService` para pre-informes PDF en el volumen de Docker

```
/var/lib/reservas                        <- Almacen:RutaBase (volumen media)
+-- espacios/{espacioId}/{guid}.jpg      <- fotos, publicas en /media
+-- solicitudes/{solicitudId}/{guid}.pdf <- solo por GET /api/solicitudes/{id}/preinforme
```

Puerto nuevo en aplicación `IAlmacenDocumentos`: `GuardarPdfAsync(solicitudId, Stream, nombreOriginal, longitud)`, `AbrirAsync(rutaRelativa)` y `EliminarAsync(rutaRelativa)`. Implementación `FileStorageService` en `Backend.Infrastructure/Services`, registrada como `IAlmacenDocumentos`, reutilizando `OpcionesAlmacen:RutaBase` para no añadir un volumen nuevo.

Validación en dos pasos y en este orden:

1. **Tamaño**: `longitud` declarada y bytes reales `<= Solicitudes:TamanoMaximoPdfBytes` (5 MB) → si excede, `413 archivo-demasiado-grande` sin escribir.
2. **Firma**: la cabecera debe empezar por `%PDF-` (`25 50 44 46 2D`) y el archivo debe contener `%%EOF` en sus últimos 1 024 bytes → si no, `415 archivo-no-valido` y se descarta el archivo temporal.

El nombre lo genera el servidor (`{Guid:N}.pdf`), la ruta relativa se guarda en `SolicitudPrestamo.ArchivoRuta` y el nombre original solo como metadato. El PDF nunca se sirve por `/media` (evita enumeración y acceso anónimo); la descarga pasa por el caso de uso que autoriza a autor y revisores y devuelve `Resultado<ArchivoPdf>` con `Content-Type: application/pdf`, `Content-Disposition: attachment` y `Cache-Control: no-store`. Si la persistencia falla después de guardar, el handler elimina el archivo (borrado compensatorio, igual que `SubirFotografiaHandler`).

### 7. Cola de salida y despacho con MailKit

```
PUT /api/solicitudes/{id}/estado
  |
  +--> TRANSACCION
         +-- UPDATE SolicitudesPrestamo (estado, revisor, motivo)
         +-- UPDATE/INSERT ReservaEspacio si Aprobado
         +-- INSERT CorreosPendientes (plantilla HTML ya compuesta)
         +--> COMMIT  ==> 200 OK   (no espera al SMTP)

DespachadorCorreos : BackgroundService (Infrastructure)
  cada Smtp:IntervaloDespachoSegundos
     +-- tomar 20 pendientes con ProximoIntentoEn <= ahora (UPDLOCK, READPAST)
     +-- MailKitEmailService.EnviarAsync  (SmtpClient + MimeKit)
     +-- exito  -> Estado=Enviado, EnviadoEn=ahora
     +-- fallo  -> Intentos++, ProximoIntentoEn = ahora + min(2^intentos * 30s, 1h)
                   Intentos >= 5 -> Estado=Fallido (se conserva para inspeccion)
```

```
CorreosPendientes
+------------------------------------------------------------------+
| Id  Destinatario  Asunto  CuerpoHtml  ReferenciaId (solicitud)   |
| Estado: Pendiente | Enviado | Fallido   Intentos                 |
| ProximoIntentoEn  CreadoEn  EnviadoEn?  UltimoError?             |
+------------------------------------------------------------------+
```

Motivos: (a) la aplicación no puede referenciar `Microsoft.Extensions` ni MailKit, así que el puerto `IColaCorreos.EncolarAsync` vive en aplicación y el `BackgroundService` en infraestructura; (b) escribir la cola en la misma transacción garantiza que no exista decisión sin notificación ni notificación sin decisión; (c) `MailKitEmailService` compone con `MimeKit` (`BodyBuilder.HtmlBody` + `TextBody`) y la plantilla HTML se construye en `Backend.Application/Notificaciones/PlantillaCorreoSolicitud` con `WebUtility.HtmlEncode` sobre todo dato del usuario, de modo que el correo es seguro y la librería de envío es sustituible.

La entrega es *at-least-once*: si el proceso cae después de enviar y antes de marcar, el correo se reenvía. Se acepta porque la decisión es idempotente en su contenido y el volumen es bajo; se evita el envío duplicado normal con `UPDLOCK, READPAST` y una sola instancia activa del despachador. Cuando `Smtp:Habilitado` es `false` el despachador no envía ni falla: deja la cola intacta (permite correr pruebas y desarrollo sin servidor de correo). `docker-compose.yml` añade un servicio `mailhog` (`1025` SMTP, `8025` UI) solo para desarrollo.

### 8. Contratos REST

| Método y ruta | Rol exigido | Entrada | Éxito | Fallos |
| :--- | :--- | :--- | :--- | :--- |
| `POST /api/reservas/recurrentes` | `Docente` | `espacioId`, `patron{tipo, diaSemana?, fechasFijas?, horaInicio, horaFin}`, `periodo{fechaInicio, fechaFin}`, `materia` | `201` serie + ocurrencias | `400`, `401`, `403`, `404`, `409 colision-serie`, `409 franja-no-operativa`, `409 fuera-de-periodo-academico`, `409 modalidad-incompatible`, `409 espacio-no-disponible`, `400 serie-sin-ocurrencias` |
| `GET /api/reservas/recurrentes/me` | `Docente` | `pagina`, `tamano` | `200` página de series | `401`, `403` |
| `POST /api/solicitudes` | `Estudiante`, `Docente` | `multipart/form-data`: `espacioId`, `inicio`, `fin`, `materia`, `docenteResponsable`, `justificacion`, `archivo` | `201` solicitud radicada | `400`, `401`, `409 espacio-no-solicitable`, `409 modalidad-incompatible`, `409 fuera-de-periodo-academico`, `413 archivo-demasiado-grande`, `415 archivo-no-valido` |
| `GET /api/solicitudes/me` | autenticado | `estado?`, `pagina`, `tamano` | `200` página propia | `400`, `401` |
| `GET /api/solicitudes/{id}/preinforme` | autor o revisor competente | — | `200 application/pdf` | `401`, `403 solicitud-ajena`, `404` |
| `GET /api/bandeja-aprobaciones` | `Docente`, `Administrativo` | `estado?`, `espacioId?`, `desde?`, `hasta?`, `pagina`, `tamano` | `200` página filtrada | `400`, `401`, `403` |
| `PUT /api/solicitudes/{id}/estado` | `Docente`, `Administrativo` | `{ "estado": "EnRevision\|Aprobado\|Rechazado", "motivo?": "..." }` | `200` solicitud actualizada | `400`, `401`, `403`, `404`, `409 transicion-invalida`, `409 franja-no-disponible`, `409 franja-no-operativa`, `409 fuera-de-periodo-academico` |

`ProblemasSolicitud` extiende el patrón de `ProblemasReserva` con slugs estables: `colision-serie`, `espacio-no-disponible`, `espacio-no-solicitable`, `archivo-no-valido`, `archivo-demasiado-grande`, `transicion-invalida`, `solicitud-ajena`, `franja-no-disponible`. La aplicación expone DTOs (`SerieResumen`, `SolicitudDetalle`, `SolicitudBandejaItem`, `PaginaResultado<T>`) y la presentación nunca serializa entidades.

### 9. Componentes Angular 20

```
src/app
+-- core/solicitudes/solicitudes.api.ts          <- multipart, bandeja, cambio de estado (compartido)
+-- features/reservas-semestrales
|   +-- reservas-semestrales.routes.ts
|   +-- models/reserva-recurrente.models.ts
|   +-- services/reservas-recurrentes.api.ts
|   +-- estado/series.store.ts                   <- Signals: series, ocurrencias, colisiones, cargando, error
|   +-- pages/nueva-serie.page.ts                <- patron, periodo, vista previa
|   +-- pages/mis-series.page.ts
|   +-- components/patron-form.ts
|   +-- components/ocurrencias-preview.ts
+-- features/bandeja-aprobaciones
|   +-- bandeja.routes.ts
|   +-- models/solicitud.models.ts
|   +-- estado/bandeja.store.ts                  <- Signals: filtros, pagina, seleccion, cargando, error
|   +-- pages/bandeja.page.ts
|   +-- pages/solicitud-detalle.page.ts
|   +-- components/solicitud-fila.ts
|   +-- components/dialogo-rechazo.ts
+-- features/solicitudes-estudiante
|   +-- pages/radicar-solicitud.page.ts          <- formulario + adjunto PDF + validacion local
+-- features/administracion/configuracion
    +-- configuracion.routes.ts
    +-- pages/configuracion.page.ts              <- periodos academicos + franjas no operativas
    +-- services/calendario.api.ts
```

Rutas: `/reservas-semestrales` y `/bandeja-aprobaciones` bajo `authGuard` + `roleGuard([Docente, Administrativo])`; `/solicitudes/radicar/:espacioId` bajo `authGuard`; `/administracion/configuracion` bajo `authGuard` + `roleGuard([Administrativo])`; enlaces nuevos en `shared/shell` visibles según rol, en `administracion.page.ts` y en el detalle del área solo cuando `tipoAprobacion` no es `Automatica`. El formulario de radicación valida en cliente tamaño y extensión, pero la decisión final es del backend; `problem-details.interceptor` ya presenta los `type` estables, y el store los traduce a mensajes por campo (`archivo`, `inicio`, `motivo`). No se añaden dependencias npm: `HttpClient` con `FormData` y `Signals` cubren el flujo.

### 10. Persistencia, índices y configuración

```
ReservasRecurrentes                      ReservasEspacio
+------------------------------+         +----------------------------------------+
| Id (PK)                      |         | Id (PK)                                |
| EspacioId (FK Espacios)      |         | EspacioId (FK Espacios)                |
| DocenteId (FK AspNetUsers)   |  1..n   | TitularId (FK AspNetUsers)             |
| TipoRecurrencia (int)        |<--------| Origen (int)  OrigenId (Guid)          |
| DiaSemana?  FechasFijasJson? |         | Inicio  Fin (datetimeoffset)           |
| PeriodoInicio  PeriodoFin    |         | Estado (int)  CreadaEn  RowVersion     |
| HoraInicio  HoraFin (time)   |         +----------------------------------------+
| Materia  CreadaEn  RowVersion|
+------------------------------+         IX_ReservasEspacio_EspacioId_Inicio_Activa
                                         UNIQUE (EspacioId, Inicio) WHERE Estado = 1
SolicitudesPrestamo                      IX_ReservasEspacio_EspacioId_Ventana
+----------------------------------+     IX_ReservasEspacio_Origen_OrigenId
| Id  SolicitanteId  EspacioId     |
| Inicio  Fin  Materia             |     CorreosPendientes
| DocenteResponsable  Justificacion|     +--------------------------------------+
| ArchivoRuta  ArchivoNombreOriginal|    | Id  Destinatario  Asunto  CuerpoHtml |
| ArchivoTamanoBytes  Estado (int) |     | ReferenciaId  Estado (int)  Intentos |
| RadicadaEn  RevisadaEn?          |     | ProximoIntentoEn  CreadoEn           |
| RevisorId?  MotivoRechazo?       |     | EnviadoEn?  UltimoError?             |
| RowVersion                       |     +--------------------------------------+
+----------------------------------+     IX_CorreosPendientes_Estado_ProximoIntento
IX_Solicitudes_Estado_EspacioId
IX_Solicitudes_SolicitanteId_RadicadaEn

PeriodosAcademicos                       FranjasNoOperativas
+---------------------------------+      +--------------------------------------+
| Id  Nombre (<=40)               |      | Id  DiaSemana? (int)  FechaEspecifica|
| FechaInicio (date)              |      | HoraInicio (time)  HoraFin (time)    |
| FechaFin (date)                 |      | EsFeriado (bit)  Motivo (<=120)      |
| EstadoActivo (bit)              |      | Activa (bit)  CreadoEn  RowVersion   |
| CreadoEn  ActualizadoEn         |      +--------------------------------------+
| RowVersion                      |      IX_FranjasNoOperativas_Dia_Fecha_Horas
+---------------------------------+
IX_PeriodosAcademicos_Activo
UNIQUE (EstadoActivo) WHERE EstadoActivo = 1
```

`Espacios` gana `TipoModalidadPrestamo` y `TipoElementoPrestamo` como `int` con valor por defecto en la migración (`AreaCompleta`/`LaboratorioEspecializado`) más el relleno heurístico descrito en Riesgos. `IX_ReservasEspacio_*` y `IX_Solicitudes_*` se mantienen; se añade `IX_PeriodosAcademicos_Fechas` para la validación de solapes y `IX_FranjasNoOperativas_Dia_Fecha_Horas` para la expansión del calendario.

Configuración nueva (valores por defecto):

| Clave | Valor | Uso |
| :--- | :--- | :--- |
| `Solicitudes:TamanoMaximoPdfBytes` | `5242880` | Límite del pre-informe |
| `Solicitudes:MaxSolicitudesActivasPorUsuario` | `3` | Solicitudes simultáneas en `Radicado`/`EnRevision` por cuenta |
| `Solicitudes:LongitudMinimaMotivoRechazo` | `10` | Motivo obligatorio del rechazo |
| `Semestre:SemanasMaximasPorSerie` | `20` | Tope de generación por serie |
| `Semestre:AntelacionMinimaDias` | `3` | Antelación de la primera ocurrencia |
| `Calendario:PeriodoActivoRequerido` | `true` | Exige periodo activo para cualquier reserva |
| `Calendario:MinutosCacheCalendario` | `1` | TTL de la caché de periodo y franjas |
| `Calendario:FeriadosNacionales` | `[]` | Semilla opcional de feriados al iniciar (solo si la tabla está vacía) |
| `Smtp:Habilitado` | `false` (dev), `true` (compose con MailHog) | Interruptor del despachador |
| `Smtp:Host` / `Smtp:Puerto` / `Smtp:UsarSsl` | `localhost` / `1025` / `false` | Conexión SMTP |
| `Smtp:Usuario` / `Smtp:Contrasena` | vacío | Autenticación opcional |
| `Smtp:RemitenteCorreo` / `Smtp:RemitenteNombre` | `reservas@uninpahu.edu.co` / `Reservas de Espacios` | Remitente |
| `Smtp:IntervaloDespachoSegundos` | `15` | Cadencia del despachador |

`OpcionesSolicitudes`, `OpcionesSemestre`, `OpcionesCalendario` y `OpcionesSmtp` se registran como singletons en `DependencyInjection` con `Validar()` al arranque (rangos de tamaño, semanas máximas, TTL positivo, puerto SMTP válido), siguiendo `OpcionesReservas`. El paquete `MailKit` (con `MimeKit`) se añade solo a `Backend.Infrastructure`; la prueba de arquitectura sigue verificando que aplicación y dominio no lo referencian.

### 11. `PeriodoAcademico`: ventana única y activación transaccional

```
PeriodoAcademico (raiz de agregado)
+-------------------------------------------------------+
| Id  Nombre (<=40)                                     |
| FechaInicio (date)  FechaFin (date)                   |
| EstadoActivo (bool)  CreadoEn  ActualizadoEn          |
| RowVersion                                            |
+-------------------------------------------------------+
        ^
        | lectura cacheada (Calendario:MinutosCacheCalendario)
        |
  IConsultaCalendario.VentanaActivaAsync()
```

Invariantes: `FechaFin >= FechaInicio`; dos periodos no pueden solaparse (validación en el handler contra el repositorio y `409 periodo-solapado`); como máximo uno activo. La activación (`PUT /api/periodos-academicos/{id}/activar`) desactiva al vigente y activa el nuevo **en la misma transacción serializable** con `UPDLOCK, HOLDLOCK` sobre la tabla, de modo que dos administrativos concurrentes no dejan dos activos. Un índice único filtrado `IX_PeriodosAcademicos_Activo` (`WHERE [EstadoActivo] = 1`) es la barrera final.

No se elimina un periodo con ocupación futura: editar sus fechas o desactivarlo responde `409 periodo-con-ocupacion` si deja reservas, series o solicitudes fuera de la nueva ventana. Esto evita que un cambio de calendario invalide agendas ya confirmadas de forma silenciosa.

### 12. `FranjaNoOperativa`: calendario global por día o por fecha

```
FranjaNoOperativa (raiz de agregado)
+---------------------------------------------------------------+
| Id  DiaSemana? (0..6)  FechaEspecifica? (date)                |
| HoraInicio (time)  HoraFin (time)                              |
| EsFeriado (bool)  Motivo (<=120)  Activa (bool)               |
| CreadoEn  RowVersion                                          |
+---------------------------------------------------------------+
   (DiaSemana XOR FechaEspecifica) y HoraInicio < HoraFin
```

Interpretación: una franja con `DiaSemana` aplica a **todas** las fechas de ese día dentro de la ventana consultada; una con `FechaEspecifica` aplica solo a esa fecha. `00:00`–`23:59` cierra el día completo; `13:00`–`21:00` cierra la tarde. La entidad no guarda un rango de fechas: la vigencia la da el periodo académico activo y, si se necesita un feriado, se registra con `EsFeriado = true` y su fecha.

Expansión y evaluación:

```
IConsultaCalendario.BloqueosAsync(desde, hasta)
  -> expande cada FranjaNoOperativa a intervalos concretos [fecha + HoraInicio, fecha + HoraFin)
  -> devuelve IntervaloBloqueado(Inicio, Fin, Motivo, EsFeriado)

ConfigurarFranjaNoOperativa  -> 200/201 y purga la caché
```

Reglas de validación: exactamente uno de `DiaSemana`/`FechaEspecifica` (`400`), `HoraFin > HoraInicio` (`400`), duplicado exacto por día o fecha y rango (`409 franja-duplicada`), solape con otra franja del mismo día (`409 franja-solapada`). La caché en memoria (`IMemoryCache` en infraestructura, TTL `Calendario:MinutosCacheCalendario`) se invalida al crear, editar o eliminar una franja y al activar un periodo, para que el cambio aplique sin reiniciar la API.

### 13. Modalidad y elemento: parametrización por área, no por categoría

```
TipoModalidadPrestamo          TipoElementoPrestamo
  AreaCompleta = 1               EquipoComputo = 1
  PuestoIndividual = 2           LaboratorioEspecializado = 2
  SubZonaCompartida = 3          ZonaDescansoJuegos = 3
                                 Auditorio = 4

combinaciones validas
+----------------------+---------------------------------------------------+
| AreaCompleta         | LaboratorioEspecializado | Auditorio | Descanso   |
| PuestoIndividual     | EquipoComputo                                     |
| SubZonaCompartida    | EquipoComputo | ZonaDescansoJuegos                |
+----------------------+---------------------------------------------------+
```

`Espacio.Crear` y `Espacio.Actualizar` reciben ambos enums, los validan con `Enum.IsDefined` y aplican la matriz (`DomainException` con mensaje por campo); la configuración Fluent API los persiste como `int`, igual que `CategoriaEspacio` y `TipoAprobacion`.

Aplicación al préstamo, resuelta en los handlers con el `Espacio` ya cargado:

| Modalidad | `POST /api/reservas` (puesto) | `POST /api/reservas/recurrentes` | `POST /api/solicitudes` |
| :--- | :--- | :--- | :--- |
| `PuestoIndividual` | `201` | `409 modalidad-incompatible` | `409 modalidad-incompatible` |
| `SubZonaCompartida` | `201` | `409 modalidad-incompatible` | `409 modalidad-incompatible` |
| `AreaCompleta` | `409 modalidad-incompatible` | `201` | `201` |

Motivo: la modalidad es una decisión administrativa del área y no puede inferirse de la categoría, porque un `Laboratorio` puede prestarse por puesto o como recinto completo. Separar `TipoElementoPrestamo` permite rotular el catálogo (equipos, laboratorio especializado, zona de descanso, auditorio) sin multiplicar categorías.

Cambiar la modalidad con ocupación futura se rechaza con `409 modalidad-con-ocupacion`: de lo contrario una serie de área completa podría dejar puestos reservados dentro de un recinto bloqueado. El alta de puestos en un área `AreaCompleta` también se rechaza con `400`, porque esa modalidad no administra sub-unidades.

### 14. Contratos administrativos del calendario y del área

| Método y ruta | Rol | Entrada | Éxito | Fallos |
| :--- | :--- | :--- | :--- | :--- |
| `GET /api/periodos-academicos` | `Administrativo` | `pagina`, `tamano`, `soloActivos?` | `200` página | `401`, `403` |
| `POST /api/periodos-academicos` | `Administrativo` | `nombre`, `fechaInicio`, `fechaFin` | `201` periodo inactivo | `400`, `401`, `403`, `409 periodo-solapado` |
| `PUT /api/periodos-academicos/{id}` | `Administrativo` | `nombre`, `fechaInicio`, `fechaFin`, `rowVersion` | `200` periodo | `400`, `403`, `404`, `409 periodo-solapado`, `409 periodo-con-ocupacion` |
| `PUT /api/periodos-academicos/{id}/activar` | `Administrativo` | — | `200` periodo activo | `403`, `404`, `409 periodo-con-ocupacion` |
| `GET /api/franjas-no-operativas` | `Administrativo` | `diaSemana?`, `desde?`, `hasta?` | `200` listado | `400`, `401`, `403` |
| `POST /api/franjas-no-operativas` | `Administrativo` | `diaSemana?`, `fechaEspecifica?`, `horaInicio`, `horaFin`, `esFeriado`, `motivo` | `201` franja | `400`, `403`, `409 franja-duplicada`, `409 franja-solapada` |
| `DELETE /api/franjas-no-operativas/{id}` | `Administrativo` | — | `204` | `403`, `404` |
| `GET /api/calendario/vigente` | autenticado | — | `200` periodo activo + franjas | `401` |
| `POST/PUT /api/espacios` | `Administrativo` | + `tipoModalidadPrestamo`, `tipoElementoPrestamo` | `201`/`200` | `400`, `403`, `409 modalidad-con-ocupacion` |

`ProblemasCalendario` añade los slugs `fuera-de-periodo-academico`, `sin-periodo-activo`, `franja-no-operativa`, `periodo-solapado`, `periodo-con-ocupacion`, `franja-duplicada`, `franja-solapada`, `serie-sin-ocurrencias` y `modalidad-incompatible`, reutilizando `CatalogoProblemas.DesdeResultado` y el mapeo por mensaje de dominio de `ProblemasReserva`.

### 15. Configuración administrativa en Angular 20

```
src/app/features/administracion/configuracion
+-- configuracion.routes.ts
+-- models/calendario.models.ts        <- PeriodoAcademico, FranjaNoOperativa, CalendarioVigente
+-- services/calendario.api.ts         <- CRUD de periodos y franjas + calendario vigente
+-- estado/calendario.store.ts         <- Signals: periodoActivo, franjas, cargando, error, guardando
+-- pages/configuracion.page.ts        <- dos paneles: periodo academico y franjas no operativas
+-- pages/periodo-form.page.ts
+-- pages/franja-form.page.ts
+-- components/periodo-card.ts
+-- components/franja-tabla.ts
+-- components/calendario-resumen.ts
```

Ruta `/administracion/configuracion` bajo `authGuard` + `roleGuard([Administrativo])`, enlazada desde `features/administracion/administracion.page.ts` y desde `shared/shell`. El store calcula en cliente el resumen de la próxima semana (días y horas no operativas) y deshabilita la activación cuando el backend reporta ocupación futura. El formulario de área (`espacio-form.page.ts`) incorpora dos selectores obligatorios y muestra la matriz de combinaciones válidas antes de enviar.

## Risks / Trade-offs

- **Crecimiento de la agenda de áreas** → cada serie escribe una fila por ocurrencia (máximo 20 por serie). Con 100 series por semestre son ~2 000 filas, muy por debajo del volumen de `Reservas`; el índice `(EspacioId, Inicio, Fin)` mantiene las lecturas del barrido acotadas.
- **Bloqueo por área durante el alta** → `UPDLOCK, HOLDLOCK` serializa solo las altas de serie de la misma área y la transacción no incluye llamadas de red; el bloqueo dura milisegundos.
- **Lectura completa de la ventana semestral** → el barrido exige cargar la ocupación de todo el periodo. Se acota con `Semestre:SemanasMaximasPorSerie`, proyección de solo tres columnas y `AsNoTracking`; el costo es menor que N consultas por ocurrencia.
- **Diferencia entre la franja local y UTC** → las ocurrencias se construyen con `MotorDisponibilidad.InicioEnZona` y se persisten en `datetimeoffset`; una zona con DST puede producir ocurrencias de duración distinta si el salto cae dentro de la franja (no aplica a `America/Bogota`).
- **Correo *at-least-once*** → un fallo entre el envío y el marcado reenvía la notificación. Aceptable: el mensaje es informativo e idempotente; se evita el duplicado normal con bloqueo de la fila y una sola instancia del despachador.
- **PDF sin análisis de contenido** → se valida tamaño y firma, no malware; se mitiga sirviendo el archivo solo autenticado con `Content-Disposition: attachment` y `no-store`. Un antivirus queda fuera de alcance y anotado como deuda.
- **Competencia del revisor resuelta en aplicación** → el agregado no conoce roles; si el `TipoAprobacion` del área cambia después de radicar, la bandeja usa el valor vigente. Se acepta porque el área se parametriza rara vez y el cambio queda auditado en el catálogo.
- **Correo no enviado en desarrollo** → con `Smtp:Habilitado=false` la cola se acumula; se inspecciona por base de datos y MailHog cubre el flujo completo en compose.
- **Caché del calendario desactualizada** → el TTL (`Calendario:MinutosCacheCalendario`, por omisión 1) acota la ventana de inconsistencia y cada mutación de periodo o franja purga la caché; un cambio de calendario puede dejar reservas ya confirmadas fuera de la nueva ventana, y por eso editar o desactivar un periodo con ocupación futura responde `409 periodo-con-ocupacion`.
- **Carrera al activar periodos** → dos activaciones concurrentes se serializan con `UPDLOCK, HOLDLOCK` y el índice único filtrado `IX_PeriodosAcademicos_Activo`; la segunda transacción reintenta y responde `409` si el resultado deja dos activos.
- **Relleno de modalidad en áreas existentes** → la migración aplica una heurística (con puestos → `PuestoIndividual` + `EquipoComputo`; `Auditorio` → `AreaCompleta` + `Auditorio`; `Laboratorio` → `AreaCompleta` + `LaboratorioEspecializado`; `SalaComputo` → `PuestoIndividual` + `EquipoComputo`; `Deportivo` → `SubZonaCompartida` + `ZonaDescansoJuegos`; resto → `AreaCompleta` + `LaboratorioEspecializado`). El administrador puede corregirla después; el `Down` elimina las columnas y no requiere reversión de datos.
- **Franjas no operativas mal parametrizadas** → una franja con rango invertido o con `DiaSemana` y `FechaEspecifica` a la vez se rechaza en el alta; el riesgo residual es una franja válida pero inconveniente, mitigado porque la pantalla muestra el resumen semanal antes de guardar y el endpoint de calendario vigente permite auditarlo.

## Migration Plan

1. Dominio del calendario: `PeriodoAcademico`, `FranjaNoOperativa`, `CalendarioInstitucional` (expansión de bloqueos y evaluación), `TipoModalidadPrestamo`/`TipoElementoPrestamo` y validaciones de `Espacio`; pruebas unitarias de ventana académica, expansión de días y fechas, duplicados y matriz de modalidad.
2. Dominio de reservas y solicitudes: `PatronRecurrencia`, `PeriodoSemestral`, `ReservaRecurrente`, `ReservaEspacio`, `EstadoSolicitud`, `SolicitudPrestamo`, `MensajesSolicitud`, `MotorDisponibilidad.DetectarColisiones` y `GenerarOcurrencias` con omisión y rechazo por calendario; pruebas unitarias de generación (semanal, bisemanal, fechas fijas, feriados, fuera de periodo, serie sin ocurrencias) y de barrido.
3. Opciones `Solicitudes`, `Semestre`, `Calendario` y `Smtp` con `Validar()`, puertos `IConsultaCalendario`, `IPeriodosAcademicosRepository`, `IFranjasNoOperativasRepository`, `IReservasRecurrentesRepository`, `IConsultaOcupacionEspacio`, `IConsultaSolicitudes`, `ISolicitudesRepository`, `IAlmacenDocumentos` y `IColaCorreos`; DTOs y handlers de calendario, series, radicación, consulta, descarga, bandeja y cambio de estado. La suite de arquitectura sigue en verde.
4. Configuraciones Fluent API, `DbSet`s y migraciones `ReservasSemestralesYAprobaciones` y `CalendarioInstitucionalYModalidad` (tablas, columnas de `Espacios`, índices únicos filtrados del periodo activo y de la agenda); verificar con `dotnet ef database update` y una consulta a `sys.indexes`.
5. `CalendarioRepository`/`ConsultaCalendario` con caché por TTL e invalidación, activación transaccional del periodo y validación de solapes; pruebas de integración de activación concurrente y de rechazo `periodo-con-ocupacion`.
6. `ReservasRecurrentesRepository`, `ConsultaOcupacionEspacio` y `SolicitudesRepository` con transacción serializable, `UPDLOCK/HOLDLOCK`, re-verificación de calendario y agenda; pruebas con Testcontainers (dos series concurrentes, serie contra reserva puntual, aprobación concurrente, reserva fuera de periodo).
7. `FileStorageService`, `MailKitEmailService`, `PlantillaCorreoSolicitud`, `DespachadorCorreos` y servicio `mailhog` en `docker-compose.yml`; pruebas de almacén y del despachador con remitente falso.
8. Controladores `PeriodosAcademicosController`, `FranjasNoOperativasController`, `CalendarioController`, `ReservasRecurrentesController`, `SolicitudesController` y `BandejaAprobacionesController` con `ProblemasCalendario` y `ProblemasSolicitud`, y ampliación de `EspaciosController` con los nuevos campos; pruebas HTTP de `200`, `201`, `204`, `400`, `401`, `403`, `404`, `409`, `413` y `415`.
9. SPA: `features/administracion/configuracion`, `features/reservas-semestrales`, `features/bandeja-aprobaciones`, `radicar-solicitud.page`, `core/solicitudes/solicitudes.api.ts`, ampliación de `espacio-form.page`, rutas, guardas y navegación; pruebas de servicio, store y componentes.
10. Cierre: `openspec validate 04-semester-bookings-and-approvals --strict`, `dotnet test src/Backend.sln`, `npm test`, `npm run build` y humo integral (activar periodo → registrar franja → serie omitiendo domingo → conflicto en la agenda → radicación con PDF → bandeja → aprobación → correo en MailHog → intento de reserva fuera de periodo rechazado).

**Rollback:** el cambio es aditivo. Revertir la rama y, si las migraciones ya se aplicaron, ejecutar `dotnet ef database update Reservas`; los `Down` eliminan las tablas y columnas nuevas (`ReservasRecurrentes`, `ReservasEspacio`, `SolicitudesPrestamo`, `CorreosPendientes`, `PeriodosAcademicos`, `FranjasNoOperativas`, `Espacios.TipoModalidadPrestamo`/`TipoElementoPrestamo`) sin tocar datos previos del catálogo. Los archivos PDF del volumen quedan huérfanos (borrado manual) y la configuración nueva puede retirarse sin afectar a la identidad ni al préstamo directo.

## Decisiones de Diseño Adoptadas

1. **Serie todo o nada.** No se ofrece confirmación parcial ni lista de fechas alternativas; el docente ajusta el patrón y reintenta. Simplifica la transacción y evita agendas a medias.
2. **Agenda única de área.** Las ocurrencias y las solicitudes aprobadas comparten `ReservasEspacio`, con `Origen`/`OrigenId` para trazabilidad. Las reservas de puestos se consultan en la misma lectura sin migrarse.
3. **Competencia por tipo de aprobación.** `Docente` resuelve áreas con `TipoAprobacion.Docente`; `Administrativo` resuelve cualquier área, incluidas las de docente (supervisión). El solicitante nunca decide su propia solicitud.
4. **Rechazo con motivo obligatorio.** Mínimo `Solicitudes:LongitudMinimaMotivoRechazo` (10) caracteres, visible para el solicitante y en el correo.
5. **Notificación encolada, no en línea.** La respuesta HTTP no depende del SMTP; el correo se compone antes de confirmar la transacción y se despacha en segundo plano con reintentos.
6. **PDF fuera del almacén estático.** Se guarda en el mismo volumen bajo `solicitudes/` y solo se entrega por endpoint autenticado; `/media` sigue sirviendo únicamente fotografías.
7. **Sin dependencias nuevas en la SPA.** `FormData`, `HttpClient` y Signals cubren carga, bandeja y cambio de estado.
8. **Un solo periodo académico activo.** Activar un periodo desactiva al vigente en la misma transacción serializable; no se admiten periodos solapados ni reservas fuera de la ventana activa.
9. **Calendario global sin excepciones por área.** Las franjas no operativas aplican a todo el campus y el mantenimiento por área queda fuera de alcance; así la regla es una sola y auditable.
10. **Modalidad explícita por área.** Se parametriza y no se infiere de la categoría; determina qué endpoints de reserva admite el área y bloquea cambios con ocupación futura.
11. **Error con causa identificable.** El backend distingue `fuera-de-periodo-academico`, `franja-no-operativa`, `serie-sin-ocurrencias` y `colision-serie` con slugs estables, y la SPA los presenta sin traducción adicional.
