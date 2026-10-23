# Tasks

## 1. Bloque 1 â€” Backend: dominio, aplicaciÃ³n, EF Core, PDF y SMTP

- [x] 1.1 Crear `PeriodoAcademico`, `FranjaNoOperativa` y `CalendarioInstitucional` (ventana activa, expansiÃ³n de bloqueos, evaluaciÃ³n de motivo); verificar con pruebas unitarias de ventana, dÃ­a completo, fecha especÃ­fica, sÃ¡bado parcial y duplicados.
- [x] 1.2 AÃ±adir `TipoModalidadPrestamo` y `TipoElementoPrestamo` con la matriz de combinaciones en `Espacio.Crear`/`Actualizar`; verificar con pruebas unitarias de combinaciÃ³n vÃ¡lida, incoherente y cambio de modalidad con ocupaciÃ³n.
- [x] 1.3 Crear `PatronRecurrencia`, `PeriodoSemestral`, `ReservaRecurrente`, `ReservaEspacio`, `EstadoSolicitud`, `SolicitudPrestamo` y `MensajesSolicitud`; verificar con pruebas unitarias de generaciÃ³n semanal (16 con feriado), bisemanal (8), fechas fijas (3) y fuera de periodo (`400`).
- [x] 1.4 Implementar `GenerarOcurrencias` (omisiÃ³n de dÃ­a no operativo, rechazo parcial, `serie-sin-ocurrencias`) y `DetectarColisiones` (barrido, tope 20); verificar con pruebas de colisiÃ³n, contigÃ¼idad, agenda vacÃ­a y serie completamente en domingo.
- [x] 1.5 Implementar `SolicitudPrestamo` (`TomarEnRevision`, `Aprobar`, `Rechazar`, terminalidad, motivo â‰¥ 10); verificar con pruebas de transiciÃ³n vÃ¡lida, salto de estado y motivo ausente.
- [x] 1.6 AÃ±adir `OpcionesSolicitudes`, `OpcionesSemestre`, `OpcionesCalendario` y `OpcionesSmtp` con `Validar()`, registrarlas en `DependencyInjection` y `appsettings.json`; verificar con `dotnet build` y arranque con TTL invÃ¡lido y puerto SMTP invÃ¡lido.
- [x] 1.7 Declarar puertos `IConsultaCalendario`, `IPeriodosAcademicosRepository`, `IFranjasNoOperativasRepository`, `IReservasRecurrentesRepository`, `IConsultaOcupacionEspacio`, `ISolicitudesRepository`, `IConsultaSolicitudes`, `IAlmacenDocumentos` y `IColaCorreos` con sus DTOs; verificar con `dotnet test tests/Backend.ArchitectureTests`.
- [x] 1.8 Implementar los handlers de calendario (listar, crear, editar y activar periodos; CRUD de franjas; calendario vigente) con validaciÃ³n de solapes y ocupaciÃ³n futura; verificar con pruebas unitarias `201`, `204`, `400`, `403`, `404`, `409 periodo-solapado`, `409 franja-duplicada` y `409 periodo-con-ocupacion`.
- [x] 1.9 Implementar `CrearSerieHandler` y `ConsultarSeriesHandler` con calendario y modalidad `AreaCompleta`; verificar con pruebas unitarias `201`, `400`, `403`, `404`, `409 colision-serie`, `409 franja-no-operativa`, `409 fuera-de-periodo-academico` y `409 modalidad-incompatible`.
- [x] 1.10 Implementar `RadicarSolicitudHandler`, `ConsultarSolicitudesHandler`, `DescargarPreinformeHandler`, `ConsultarBandejaHandler` y `CambiarEstadoSolicitudHandler` con revalidaciÃ³n de calendario; verificar con pruebas unitarias cada cÃ³digo `400`, `403`, `404`, `409`, `413` y `415`.
- [x] 1.11 Configurar Fluent API, `DbSet`s y las migraciones `ReservasSemestralesYAprobaciones` y `CalendarioInstitucionalYModalidad` (tablas, columnas de `Espacios`, Ã­ndices Ãºnicos filtrados y relleno heurÃ­stico de modalidad); verificar con `dotnet ef database update`, `sys.indexes` y consulta de Ã¡reas migradas.
- [x] 1.12 Implementar `CalendarioRepository`/`ConsultaCalendario` con cachÃ© por TTL, invalidaciÃ³n al mutar y activaciÃ³n transaccional del periodo; verificar con Testcontainers dos activaciones concurrentes (un solo activo) y `409 periodo-con-ocupacion`.
- [x] 1.13 Implementar `ReservasRecurrentesRepository`, `ConsultaOcupacionEspacio` y `SolicitudesRepository` con transacciÃ³n serializable, `UPDLOCK/HOLDLOCK` y revalidaciÃ³n calendario â†’ agenda; verificar con Testcontainers dos series concurrentes, serie contra reserva puntual, aprobaciÃ³n concurrente y reserva fuera de periodo.
- [x] 1.14 Implementar `FileStorageService` (`%PDF-`, `%%EOF`, 5 MB, nombre generado, borrado compensatorio) y registrarlo como `IAlmacenDocumentos`; verificar con pruebas de PDF vÃ¡lido, truncado, sobredimensionado y tipo declarado falso.
- [x] 1.15 Componer `PlantillaCorreoSolicitud`, `MailKitEmailService` y `DespachadorCorreos`, aÃ±adir `MailKit` a `Backend.Infrastructure` y `mailhog` a `docker-compose.yml`; verificar con prueba de plantilla y del despachador con remitente falso (Ã©xito, fallo, reintento).
- [x] 1.16 Exponer `PeriodosAcademicosController`, `FranjasNoOperativasController`, `CalendarioController`, `ReservasRecurrentesController`, `SolicitudesController` y `BandejaAprobacionesController`, y ampliar `EspaciosController` con modalidad y elemento; verificar con pruebas de integraciÃ³n `200`, `201`, `204`, `400`, `401`, `403`, `404`, `409`, `413`, `415` y RFC 7807.
- [x] 1.17 Documentar endpoints, reglas de calendario y modalidad, configuraciÃ³n y migraciones en `docs/reservas-semestrales-y-aprobaciones.md`; verificar cada comando y URL contra la API local y ejecutar `dotnet test src/Backend.sln` en verde.

## 2. Bloque 2 â€” Frontend Angular 20 e integraciÃ³n

- [x] 2.1 Crear `features/administracion/configuracion` (modelos, `calendario.api.ts`, `configuracion.routes.ts`) y `core/solicitudes/solicitudes.api.ts`; verificar con `npm run build` y pruebas de servicio con `HttpTestingController` de URLs, cuerpos y `FormData`.
- [x] 2.2 Implementar `calendario.store.ts` con Signals (`periodoActivo`, `franjas`, `cargando`, `error`, `guardando`); verificar con pruebas unitarias la activaciÃ³n de periodo, el `409 periodo-solapado` y la invalidaciÃ³n tras guardar una franja.
- [x] 2.3 Implementar `configuracion.page`, `periodo-form.page`, `franja-form.page`, `periodo-card` y `franja-tabla`; verificar con pruebas de componente el alta del domingo completo, el sÃ¡bado de `13:00` a `21:00` y el rechazo por duplicado.
- [x] 2.4 Implementar `calendario-resumen` (prÃ³xima semana no operativa) y su uso en `nueva-serie.page` y `radicar-solicitud.page`; verificar con pruebas de componente que los dÃ­as cerrados aparecen deshabilitados.
- [x] 2.5 Crear `features/reservas-semestrales` (modelos, rutas, `reservas-recurrentes.api.ts`) y `series.store.ts`; verificar con pruebas de servicio y store la vista previa de ocurrencias y el `409 colision-serie`.
- [x] 2.6 Implementar `nueva-serie.page`, `patron-form` y `ocurrencias-preview` con modalidad y calendario; verificar con pruebas de componente que el domingo se omite, la serie vacÃ­a se bloquea y los conflictos muestran fecha y causa.
- [x] 2.7 Implementar `mis-series.page` con estados de carga, error y vacÃ­o; verificar con pruebas de componente el orden descendente y la ausencia de series ajenas.
- [x] 2.8 Crear `features/bandeja-aprobaciones` con store, `bandeja.page`, `solicitud-detalle.page` y `dialogo-rechazo`; verificar con pruebas de componente los filtros, la apertura del PDF y el rechazo que exige motivo.
- [x] 2.9 Ampliar `features/solicitudes-estudiante` con `radicar-solicitud.page` (validaciÃ³n local de PDF, envÃ­o `FormData`) y el enlace desde el detalle del Ã¡rea segÃºn modalidad y aprobaciÃ³n; verificar con pruebas de componente los errores `409`, `413` y `415`.
- [x] 2.10 Ampliar `espacio-form.page.ts` con los selectores de modalidad y elemento, la matriz de combinaciones vÃ¡lidas y el mensaje de `409 modalidad-con-ocupacion`; verificar con pruebas de componente el alta vÃ¡lida y la combinaciÃ³n rechazada.
- [x] 2.11 Registrar `/administracion/configuracion`, `/reservas-semestrales` y `/bandeja-aprobaciones` con sus guardas y aÃ±adir los enlaces por rol en `shared/shell` y `administracion.page.ts`; verificar con `npm start` que un estudiante no accede y que sin sesiÃ³n redirige al acceso.
- [x] 2.12 Ejecutar `openspec validate 04-semester-bookings-and-approvals --strict`, `npm test`, `npm run build` y el humo integral (activar periodo â†’ franja no operativa â†’ serie â†’ conflicto â†’ radicaciÃ³n con PDF â†’ bandeja â†’ aprobaciÃ³n â†’ correo en MailHog), registrando el resultado en el reporte de la etapa.

## Workflow follow-up

- Ejecutar `openspec validate 04-semester-bookings-and-approvals --strict` y corregir advertencias antes de la revisiÃ³n.
- Archivar el cambio con el flujo de OpenSpec una vez aprobado el commit atÃ³mico.
