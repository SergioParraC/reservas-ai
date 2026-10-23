# Tasks

## 1. Bloque 1 — Backend: dominio, aplicación, EF Core, PDF y SMTP

- [x] 1.1 Crear `PeriodoAcademico`, `FranjaNoOperativa` y `CalendarioInstitucional` (ventana activa, expansión de bloqueos, evaluación de motivo); verificar con pruebas unitarias de ventana, día completo, fecha específica, sábado parcial y duplicados.
- [x] 1.2 Añadir `TipoModalidadPrestamo` y `TipoElementoPrestamo` con la matriz de combinaciones en `Espacio.Crear`/`Actualizar`; verificar con pruebas unitarias de combinación válida, incoherente y cambio de modalidad con ocupación.
- [ ] 1.3 Crear `PatronRecurrencia`, `PeriodoSemestral`, `ReservaRecurrente`, `ReservaEspacio`, `EstadoSolicitud`, `SolicitudPrestamo` y `MensajesSolicitud`; verificar con pruebas unitarias de generación semanal (16 con feriado), bisemanal (8), fechas fijas (3) y fuera de periodo (`400`).
- [ ] 1.4 Implementar `GenerarOcurrencias` (omisión de día no operativo, rechazo parcial, `serie-sin-ocurrencias`) y `DetectarColisiones` (barrido, tope 20); verificar con pruebas de colisión, contigüidad, agenda vacía y serie completamente en domingo.
- [ ] 1.5 Implementar `SolicitudPrestamo` (`TomarEnRevision`, `Aprobar`, `Rechazar`, terminalidad, motivo ≥ 10); verificar con pruebas de transición válida, salto de estado y motivo ausente.
- [ ] 1.6 Añadir `OpcionesSolicitudes`, `OpcionesSemestre`, `OpcionesCalendario` y `OpcionesSmtp` con `Validar()`, registrarlas en `DependencyInjection` y `appsettings.json`; verificar con `dotnet build` y arranque con TTL inválido y puerto SMTP inválido.
- [ ] 1.7 Declarar puertos `IConsultaCalendario`, `IPeriodosAcademicosRepository`, `IFranjasNoOperativasRepository`, `IReservasRecurrentesRepository`, `IConsultaOcupacionEspacio`, `ISolicitudesRepository`, `IConsultaSolicitudes`, `IAlmacenDocumentos` y `IColaCorreos` con sus DTOs; verificar con `dotnet test tests/Backend.ArchitectureTests`.
- [x] 1.8 Implementar los handlers de calendario (listar, crear, editar y activar periodos; CRUD de franjas; calendario vigente) con validación de solapes y ocupación futura; verificar con pruebas unitarias `201`, `204`, `400`, `403`, `404`, `409 periodo-solapado`, `409 franja-duplicada` y `409 periodo-con-ocupacion`.
- [ ] 1.9 Implementar `CrearSerieHandler` y `ConsultarSeriesHandler` con calendario y modalidad `AreaCompleta`; verificar con pruebas unitarias `201`, `400`, `403`, `404`, `409 colision-serie`, `409 franja-no-operativa`, `409 fuera-de-periodo-academico` y `409 modalidad-incompatible`.
- [ ] 1.10 Implementar `RadicarSolicitudHandler`, `ConsultarSolicitudesHandler`, `DescargarPreinformeHandler`, `ConsultarBandejaHandler` y `CambiarEstadoSolicitudHandler` con revalidación de calendario; verificar con pruebas unitarias cada código `400`, `403`, `404`, `409`, `413` y `415`.
- [ ] 1.11 Configurar Fluent API, `DbSet`s y las migraciones `ReservasSemestralesYAprobaciones` y `CalendarioInstitucionalYModalidad` (tablas, columnas de `Espacios`, índices únicos filtrados y relleno heurístico de modalidad); verificar con `dotnet ef database update`, `sys.indexes` y consulta de áreas migradas.
- [x] 1.12 Implementar `CalendarioRepository`/`ConsultaCalendario` con caché por TTL, invalidación al mutar y activación transaccional del periodo; verificar con Testcontainers dos activaciones concurrentes (un solo activo) y `409 periodo-con-ocupacion`.
- [ ] 1.13 Implementar `ReservasRecurrentesRepository`, `ConsultaOcupacionEspacio` y `SolicitudesRepository` con transacción serializable, `UPDLOCK/HOLDLOCK` y revalidación calendario → agenda; verificar con Testcontainers dos series concurrentes, serie contra reserva puntual, aprobación concurrente y reserva fuera de periodo.
- [ ] 1.14 Implementar `FileStorageService` (`%PDF-`, `%%EOF`, 5 MB, nombre generado, borrado compensatorio) y registrarlo como `IAlmacenDocumentos`; verificar con pruebas de PDF válido, truncado, sobredimensionado y tipo declarado falso.
- [ ] 1.15 Componer `PlantillaCorreoSolicitud`, `MailKitEmailService` y `DespachadorCorreos`, añadir `MailKit` a `Backend.Infrastructure` y `mailhog` a `docker-compose.yml`; verificar con prueba de plantilla y del despachador con remitente falso (éxito, fallo, reintento).
- [ ] 1.16 Exponer `PeriodosAcademicosController`, `FranjasNoOperativasController`, `CalendarioController`, `ReservasRecurrentesController`, `SolicitudesController` y `BandejaAprobacionesController`, y ampliar `EspaciosController` con modalidad y elemento; verificar con pruebas de integración `200`, `201`, `204`, `400`, `401`, `403`, `404`, `409`, `413`, `415` y RFC 7807.
- [ ] 1.17 Documentar endpoints, reglas de calendario y modalidad, configuración y migraciones en `docs/reservas-semestrales-y-aprobaciones.md`; verificar cada comando y URL contra la API local y ejecutar `dotnet test src/Backend.sln` en verde.

## 2. Bloque 2 — Frontend Angular 20 e integración

- [ ] 2.1 Crear `features/administracion/configuracion` (modelos, `calendario.api.ts`, `configuracion.routes.ts`) y `core/solicitudes/solicitudes.api.ts`; verificar con `npm run build` y pruebas de servicio con `HttpTestingController` de URLs, cuerpos y `FormData`.
- [ ] 2.2 Implementar `calendario.store.ts` con Signals (`periodoActivo`, `franjas`, `cargando`, `error`, `guardando`); verificar con pruebas unitarias la activación de periodo, el `409 periodo-solapado` y la invalidación tras guardar una franja.
- [ ] 2.3 Implementar `configuracion.page`, `periodo-form.page`, `franja-form.page`, `periodo-card` y `franja-tabla`; verificar con pruebas de componente el alta del domingo completo, el sábado de `13:00` a `21:00` y el rechazo por duplicado.
- [ ] 2.4 Implementar `calendario-resumen` (próxima semana no operativa) y su uso en `nueva-serie.page` y `radicar-solicitud.page`; verificar con pruebas de componente que los días cerrados aparecen deshabilitados.
- [ ] 2.5 Crear `features/reservas-semestrales` (modelos, rutas, `reservas-recurrentes.api.ts`) y `series.store.ts`; verificar con pruebas de servicio y store la vista previa de ocurrencias y el `409 colision-serie`.
- [ ] 2.6 Implementar `nueva-serie.page`, `patron-form` y `ocurrencias-preview` con modalidad y calendario; verificar con pruebas de componente que el domingo se omite, la serie vacía se bloquea y los conflictos muestran fecha y causa.
- [ ] 2.7 Implementar `mis-series.page` con estados de carga, error y vacío; verificar con pruebas de componente el orden descendente y la ausencia de series ajenas.
- [ ] 2.8 Crear `features/bandeja-aprobaciones` con store, `bandeja.page`, `solicitud-detalle.page` y `dialogo-rechazo`; verificar con pruebas de componente los filtros, la apertura del PDF y el rechazo que exige motivo.
- [ ] 2.9 Ampliar `features/solicitudes-estudiante` con `radicar-solicitud.page` (validación local de PDF, envío `FormData`) y el enlace desde el detalle del área según modalidad y aprobación; verificar con pruebas de componente los errores `409`, `413` y `415`.
- [ ] 2.10 Ampliar `espacio-form.page.ts` con los selectores de modalidad y elemento, la matriz de combinaciones válidas y el mensaje de `409 modalidad-con-ocupacion`; verificar con pruebas de componente el alta válida y la combinación rechazada.
- [ ] 2.11 Registrar `/administracion/configuracion`, `/reservas-semestrales` y `/bandeja-aprobaciones` con sus guardas y añadir los enlaces por rol en `shared/shell` y `administracion.page.ts`; verificar con `npm start` que un estudiante no accede y que sin sesión redirige al acceso.
- [ ] 2.12 Ejecutar `openspec validate 04-semester-bookings-and-approvals --strict`, `npm test`, `npm run build` y el humo integral (activar periodo → franja no operativa → serie → conflicto → radicación con PDF → bandeja → aprobación → correo en MailHog), registrando el resultado en el reporte de la etapa.

## Workflow follow-up

- Ejecutar `openspec validate 04-semester-bookings-and-approvals --strict` y corregir advertencias antes de la revisión.
- Archivar el cambio con el flujo de OpenSpec una vez aprobado el commit atómico.
