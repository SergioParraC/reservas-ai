# Documento de Planificación e Ingeniería Inicial: Sistema de Gestión de Reservas de Espacios Universitarios

## 1) Análisis del problema
### Identificación y delimitación de la problemática
Actualmente, la institución educativa gestiona la asignación y préstamo de sus espacios físicos académicos —incluyendo laboratorios especializados, salas de cómputo, cubículos y auditorios de gran capacidad— mediante procesos manuales basados en correos electrónicos institucionales, solicitudes verbales y hojas de cálculo compartidas. Este modelo operativo descentralizado e informal genera:
1. **Duplicidad de solicitudes y sobre-reservas (*double-booking*):** Múltiples docentes o grupos estudiantiles intentan utilizar las mismas áreas o equipos en franjas horarias superpuestas.
2. **Falta de visibilidad y asimetría de información:** Dificultad para que la comunidad académica consulte en tiempo real la disponibilidad efectiva de los recursos institucionales.
3. **Altas tasas de inasistencia no controlada (*no-show*):** Espacios reservados que permanecen vacíos sin penalización ni liberación para otros usuarios.
4. **Sobrecarga administrativa:** El personal a cargo invierte tiempo excesivo en la conciliación manual de agendas en lugar de supervisar el uso eficiente de las instalaciones.

Para resolver esta situación, se requiere un aplicativo web corporativo dotado de un esquema de acceso híbrido seguro, trazabilidad de reservas, parametrización granular de reglas por espacio y control operativo presencial.

### Actores involucrados.

* **Administrativos (Personal de Coordinación y Supervisión):** Poseen el máximo nivel de privilegios dentro del sistema. Son responsables de dar de alta la infraestructura física, clasificar y configurar las políticas de uso de cada área (aforos, reglas de aprobación, ventanas de antelación y franjas de mantenimiento), autorizar o rechazar solicitudes de espacios de alta jerarquía (auditorios) y gestionar perfiles de usuarios.
* **Profesores (Personal Docente):** Planifican el uso académico de instalaciones para sus cursos. Cuentan con facultades para emitir solicitudes de reserva de áreas completas (laboratorios, salas de sistemas o auditorios) con recurrencia semestral (semanal, quincenal o por calendario preconfigurado). Actúan como evaluadores académicos autorizando o rechazando solicitudes de laboratorio radicadas por estudiantes para el desarrollo de prácticas dirigidas.
* **Estudiantes (Comunidad Estudiantil):** Usuarios finales del servicio. Pueden realizar préstamos individuales de autogestión con aprobación inmediata sobre puestos o equipos específicos dentro de salas compartidas. Para espacios técnicos (laboratorios), deben tramitar una solicitud contingente adjuntando la justificación académica (materia, docente responsable y pre-informe técnico en formato PDF). Para auditorios, pueden radicar solicitudes institucionales sujetas al flujo formal de revisión administrativa (`Radicado`, `En revisión`, `Aprobado`, `Rechazado`).

### Alcance y limitaciones del sistema

#### Alcance del aplicativo
1. **Gestión de catálogo de infraestructura:** Alta y configuración de áreas y puestos con ficha técnica (capacidad, fotos, equipamiento, ubicación por bloque/piso y reglamento de uso).
2. **Motor de disponibilidad y reglas de negocio:** Consulta interactiva en tiempo real; aplicación automática de ventanas de antelación, tiempos máximos de préstamo y prevención estricta de cruces de horarios.
3. **Flujos de aprobación multinivel:** Aprobación automática e inmediata para puestos de cómputo individuales y aprobación jerárquica obligatoria (docente o administrativa) para áreas integrales.
4. **Control operativo y validación presencial por QR:** Registro de asistencia (*Check-in*) y culminación de uso (*Check-out*) mediante lectura óptica de códigos QR estáticos impresos en cada espacio.
5. **Liberación automatizada por inasistencia:** Cancelación automática y reincorporación del espacio a la disponibilidad pública ante el vencimiento del margen de tolerancia sin *check-in* registrado.
6. **Módulo de sanciones y reportes:** Registro de inasistencias injustificadas con suspensión temporal automatizada del perfil y tablero de indicadores de ocupación física.

#### Limitaciones explícitas del sistema
1. **Hardware e Infraestructura Física (Sin IoT):** El sistema no acciona actuadores físicos, cerraduras electromagnéticas ni torniquetes de acceso; no integra sensores volumétricos ni cámaras para el conteo automatizado de aforo presencial.
2. **Operación y Mantenimiento Físico:** El software no gestiona inventario en tiempo real de consumibles ni supervisa las tareas del personal de servicios generales; únicamente inhabilita las franjas horarias en la agenda para tales fines.
3. **Comercial y Académico Externo:** No procesa pasarelas de pago ni cobro de alquileres comerciales a terceros (el uso es exclusivamente académico e interno). No reemplaza el sistema de admisiones ni la matrícula universitaria.
4. **Canal Tecnológico:** Operatividad exclusivamente basada en aplicación web (SPA + REST API); requiere conectividad activa a la red corporativa/internet para tramitar solicitudes.

---

## 2) Propuesta de solución

La solución consiste en el diseño e implementación de un **Sistema Web de Gestión y Reserva de Espacios Universitarios**, que centraliza la infraestructura de la universidad y parametriza sus políticas de préstamo en función de la naturaleza de cada instalación.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       SISTEMA DE GESTIÓN DE ESPACIOS                        │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
        ┌──────────────────────────────┼──────────────────────────────┐
        │                              │                              │
┌───────▼────────┐             ┌───────▼────────┐             ┌───────▼────────┐
│ ADMINISTRACIÓN │             │   DOCENTES     │             │  ESTUDIANTES   │
├────────────────┤             ├────────────────┤             ├────────────────┤
│• Catálogo      │             │• Planificación │             │• Autogestión   │
│• Parametrizar  │             │  Semestral     │             │  Puestos       │
│• Moderación    │             │• Validación de │             │• Solicitud Lab │
│• Mantenimiento │             │  Prácticas     │             │• Check-in QR   │
└────────────────┘             └────────────────┘             └────────────────┘
```

### 1. Parametrización granular por área
El sistema otorga a la administración la facultad de personalizar las políticas operativas de cada recinto de forma aislada:
* **Políticas de Aprobación:** Clasificación del espacio según requiera:
  * *Auto-confirmación:* Reserva directa e inmediata al verificar disponibilidad horaria (puestos de cómputo individuales).
  * *Aprobación Docente:* Requiere validación técnica del profesor seleccionado y revisión del pre-informe adjunto (laboratorios).
  * *Aprobación Administrativa:* Requiere autorización previa del personal directivo/coordinación (auditorios y eventos masivos).
* **Ventanas de Antelación y Duración:** Definición del horizonte temporal mínimo y máximo para admitir solicitudes de préstamo.
* **Bloqueos y Mantenimiento:** Programación de franjas horarias obligatorias e indisponibles para desinfección, aseo preventivo o soporte técnico.

### 2. Reservas recurrentes para periodos semestrales (Docencia)
Flujo especializado que faculta al cuerpo docente a asegurar la disponibilidad de instalaciones para el ciclo académico antes de la apertura de solicitudes estudiantiles:
* **Modalidad Semanal/Bisemanal:** Fijación de días y horas recurrentes durante la vigencia del semestre (ej. martes de 08:00 a 10:00).
* **Modalidad Quincenal:** Alternancia estricta de uso cada catorce días.
* **Calendario Preconfigurado:** Selección explícita y manual de fechas específicas (ej. sustentaciones o exámenes parciales).

---

### 3. Control presencial y disponibilidad operativa
* **Matriz de Detección de Conflictos en Tiempo Real:** Evaluación transaccional que impide cruces entre reservas semestrales, franjas de mantenimiento y reservas puntuales.
* **Control Operativo por QR Estático:** Mecanismo de validación en el que cada aula o equipo posee una placa con un código QR único e inmutable (asociado a un identificador seguro). Al presentarse físicamente, el usuario escanea el código con la cámara de su dispositivo móvil. El backend valida la identidad, la correspondencia del espacio y que la hora del escaneo coincida con la ventana de tolerancia de la reserva.
* **Liberación Automatizada por Inasistencia (*No-Show*):** Si transcurre el margen de tolerancia parametrizado (ej. 15 minutos tras el inicio) sin registrarse el *Check-in*, la reserva pasa a estado cancelado por inasistencia y el recurso se libera automáticamente en el calendario público.

### Funcionalidades prioritarias

* **Prioridad Crítica (P1 - MVP):**
  * Autenticación híbrida (Microsoft Entra ID institucional y ASP.NET Core Identity local).
  * Creación, parametrización y catálogo de áreas e infraestructura.
  * Generación de identificadores QR únicos e inmutables con plantilla de impresión.
  * Préstamo individual de aprobación inmediata para puestos de cómputo.
  * Solicitud de laboratorios con carga obligatoria de pre-informes PDF.
* **Prioridad Media (P2):**
  * Módulo de reservas recurrentes semestrales para docentes.
  * Bandeja de trámites y moderación jerárquica para docentes y administradores.
  * Notificaciones automatizadas por correo electrónico vía SMTP.
* **Prioridad Secundaria (P3):**
  * Validación presencial de asistencia vía escáner web de QR (*Check-in* / *Check-out*).
  * Tarea en segundo plano para liberación automática por inasistencia (*No-Show*).
  * Módulo de sanciones por ausencias reiteradas con bloqueo temporal de reservas.
* **Prioridad Baja (P4):**
  * Tablero (*dashboard*) de analítica, horas pico y porcentaje de ocupación física.
  * Módulo de registro de incidencias y reporte de daños en instalaciones.

### Organización modular del sistema
* **Módulo 1: Gestión de Áreas y Parametrización:** Mantenimiento de espacios, sub-puestos, aforos, calendarios de aseo y generación de códigos QR vectoriales.
* **Módulo 2: Reservas Semestrales Docentes:** Motor de recurrencia académica y validación masiva de colisiones de agenda.
* **Módulo 3: Préstamos y Solicitudes Estudiantiles:** Catálogo público de consulta, reserva directa de puestos individuales y radicación de solicitudes para laboratorios y auditorios.
* **Módulo 4: Bandeja de Aprobaciones y Trámites:** Moderación jerárquica con filtros de gestión y motor de mensajería asíncrona.
* **Módulo 5: Asistencia Presencial y Analítica:** Validación de *Check-in/out*, monitoreo del margen de tolerancia y auditoría de indicadores de uso del campus.

### Estrategia para desarrollar el proyecto progresivamente

Para mitigar el riesgo operativo y validar el software tempranamente con la comunidad institucional, el desarrollo funcional se estructura en cuatro fases incrementales orientadas a entregables de negocio:

#### Fase 1: MVP — Núcleo Administrativo y Préstamo Directo (Sprints 1 y 2)
* **Objetivo Funcional:** Disponer de la base operativa que permita a los administradores registrar la infraestructura física y a los estudiantes autogestionar reservas inmediatas de puestos/equipos.
* **Módulos Involucrados:**
  * **Módulo 1 (Parcial):** Alta de áreas y puestos con datos obligatorios, fotos, designación de puestos individuales y generación de la plantilla de impresión de QR estáticos.
  * **Módulo 3 (Parcial):** Catálogo visual con filtros y flujo de préstamo inmediato autoconfirmado para puestos de cómputo.
* **Entregable Validable:** El administrador registra un área e imprime sus códigos QR físicos; el estudiante ingresa y reserva un equipo informático con confirmación instantánea.

---

#### Fase 2: Planificación Semestral y Moderación Jerárquica (Sprints 3 y 4)
* **Objetivo Funcional:** Incorporar las reglas complejas de planificación académica para docentes y el flujo de autorizaciones previas para áreas especializadas.
* **Módulos Involucrados:**
  * **Módulo 1 (Completo):** Parametrización de ventanas de antelación, tiempos máximos y franjas de aseo/mantenimiento.
  * **Módulo 2 (Completo):** Programación periódica para docentes (semanal, quincenal, fechas clave) con detección de colisiones horarias.
  * **Módulo 4 (Completo):** Bandejas de tramitación para docentes (laboratorios con revisión de pre-informe PDF) y administrativos (auditorios), con notificaciones por correo electrónico.
* **Entregable Validable:** Docentes aseguran los espacios para sus asignaturas de todo el semestre; estudiantes radican solicitudes de laboratorio adjuntando PDFs y reciben notificación de aprobación/rechazo del docente.

---

#### Fase 3: Validación Presencial QR y Control de Asistencia (Sprints 5 y 6)
* **Objetivo Funcional:** Vincular el sistema con la presencia física en el campus y automatizar la recuperación de espacios subutilizados sin requerir personal de supervisión.
* **Módulos Involucrados:**
  * **Módulo 3 (Completo):** Habilitación de áreas de uso múltiple (zonas de descanso/juegos) con préstamos por sub-puestos o área completa.
  * **Módulo 5 (Parcial):** Escáner web de QR desde el móvil, registro de *Check-in* y *Check-out*, y servicio en segundo plano para liberación automática por inasistencia (*No-Show* tras vencer la tolerancia).
* **Entregable Validable:** Estudiantes confirman su llegada escaneando el QR pegado en el puesto; si no asisten en el margen tolerado, el sistema cancela la reserva y la reabre al público automáticamente.

---

#### Fase 4: Analítica, Sanciones y Cierre Operativo (Sprint 7)
* **Objetivo Funcional:** Proporcionar herramientas de auditoría y métricas de uso a la dirección universitaria, garantizando la sostenibilidad y el cumplimiento de normas.
* **Módulos Involucrados:**
  * **Módulo 5 (Completo):** Módulo de sanciones con suspensión temporal automática a perfiles por ausencias reiteradas (*No-Shows*), tablero (*dashboard*) de ocupación y horas pico, y despliegue final en producción.
* **Entregable Validable:** Panel directivo con estadísticas de aprovechamiento de espacios y bloqueo preventivo del sistema a usuarios infractores.

## 3) Decisiones de arquitectura y tecnología

### Arquitectura seleccionada y justificación

Se adopta una **Arquitectura en Capas Basada en Principios Clean / Onion Architecture** para el backend (estructurado como un Monolito Modular) junto con una aplicación de página única (**Angular 20 SPA**) en el frontend.

```
┌────────────────────────────────────────────────────────┐
│                  Capa de Presentación                  │
│       (ASP.NET Core Web API 10 / Angular 20 SPA)       │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│                   Capa de Aplicación                   │
│   (Casos de Uso, Orquestación, DTOs, Puertos/Service)  │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│                    Capa de Dominio                     │
│  (Entidades, Objetos de Valor, Motor de Disponibilidad)│
└───────────────────────────▲────────────────────────────┘
                            │
┌───────────────────────────┴────────────────────────────┐
│                 Capa de Infraestructura                │
│    (EF Core 10.x, SQL Server 2022, Entra ID, Identity) │
└────────────────────────────────────────────────────────┘
```

#### Justificación del patrón Clean Architecture
1. **Aislamiento del Dominio Universitario:** La matriz de detección de conflictos, las reglas de recurrencia semestral y la evaluación de sanciones residen en el núcleo (`Domain`), sin acoplamiento a librerías externas ni a la persistencia relacional.
2. **Capacidad de Mantenimiento:** La integración con Microsoft Entra ID, el almacenamiento físico de archivos PDF o el motor de correo pueden modificarse o sustituirse en `Infrastructure` sin alterar la lógica de negocio.
3. **Facilidad de Pruebas Unitarias:** Al regirse por la Inversión de Dependencias (DIP), la capa de aplicación interactúa mediante interfaces, permitiendo simular el comportamiento de bases de datos y servicios en los casos de uso.

### Estructura de capas del sistema

El sistema backend se organiza en cuatro capas concéntricas con regla de dependencia unidireccional hacia el centro:

#### 1. Capa de Presentación (Presentation / Web API)
* **Responsabilidad:** Exponer los endpoints HTTP/REST consumidos por el cliente web (Angular 18). Se encarga de la recepción de solicitudes, validación sintáctica de DTOs de entrada, serialización JSON, gestión de códigos de estado HTTP y middlewares transversales (autenticación JWT/Entra ID, manejo global de excepciones y CORS).
* **Componentes clave:** `Controllers`, `Middlewares`, `Filters`, `DTOs` de entrada/salida.

#### 2. Capa de Aplicación (Application)
* **Responsabilidad:** Orquestar los flujos de trabajo del sistema y representar los casos de uso específicos del negocio (ej. *CrearReservaRecurrenteSemestral*, *AprobarSolicitudLaboratorio*, *ProcesarCheckInQR*, *LiberarReservasVencidas*). Define las interfaces de los servicios que la infraestructura debe implementar (Puertos/Interfaces).
* **Componentes clave:** `Application Services` / `Use Cases`, `Interfaces` de repositorios y servicios externos, `Validators` de negocio, `Mappers`.

#### 3. Capa de Dominio (Domain)
* **Responsabilidad:** Núcleo central de la aplicación. Contiene el modelo de datos del negocio, agregados, entidades, objetos de valor (*Value Objects*), excepciones de dominio y servicios de dominio con algoritmos puros. Aquí residen la matriz de conflictos de horarios, el cálculo de tolerancias para liberación por *no-show*, y las reglas de sanciones por incumplimiento.
* **Componentes clave:** `Entities` (*Reserva*, *Espacio*, *Docente*, *Puesto*, *Sancion*), `Value Objects` (*FranjaHoraria*, *CodigoQR*), `Domain Events`, `Domain Services` (*DisponibilidadEngine*).

#### 4. Capa de Infraestructura (Infrastructure)
* **Responsabilidad:** Proporcionar las implementaciones concretas para las interfaces definidas en la Capa de Aplicación y Dominio. Gestiona la comunicación con la base de datos relacional, proveedores de identidad externos y servicios del sistema operativo/archivos.
* **Componentes clave:**
  * **Persistencia:** `ApplicationDbContext` (EF Core 8), Configuración de Fluent API, Migraciones y Repositorios.
  * **Autenticación y Seguridad:** Integración con Microsoft Entra ID (SSO / OAuth2 / OpenID Connect).
  * **Servicios Externos:** Servicio de generación de imágenes QR, almacenamiento de archivos adjuntos (Pre-informes PDF) e integración con servidor SMTP para notificaciones por correo electrónico.

### Stack tecnológico

| Componente / Capa | Tecnología Seleccionada | Justificación Técnica |
| :--- | :--- | :--- |
| **Frontend (SPA)** | **Angular 20** + **TypeScript** | Framework empresarial estructurado con componentes autónomos (*Standalone Components*), control de reactividad eficiente con *Signals* y soporte tipado estricto. |
| **Escáner QR Web** | **@zxing/ngx-scanner** | Permite el acceso directo a la cámara del dispositivo móvil desde el navegador web para leer códigos QR sin necesidad de compilar aplicaciones móviles nativas. |
| **Backend Web API** | **.NET 10 (ASP.NET Core)** | Ecosistema de alto rendimiento para APIs REST, con inyección nativa de dependencias, tipado estático robusto y soporte eficiente para tareas en segundo plano. |
| **ORM y Persistencia** | **EF Core 10.x** + **SQL Server 2022** | Modelado *Code-First*, transaccionalidad estricta (cumplimiento ACID) para prevenir sobre-reservas concurrentes y consultas optimizadas mediante LINQ. |
| **Autenticación (Híbrida)** | **Microsoft Entra ID** + **ASP.NET Core Identity** | Acceso SSO mediante OAuth2/OIDC para la comunidad institucional (docentes y estudiantes) y autenticación local segura con JWT para cuentas administrativas y de soporte. |
| **Generación QR** | **QRCoder** | Librería pura en C# para renderizar códigos QR vectoriales (SVG/PNG) inmutables asignados a cada espacio físico. |
| **Notificaciones** | **MailKit & MimeKit** | Solución para la composición y despacho asíncrono de correos electrónicos bajo estándares MIME y SMTP seguro. |
| **Pruebas y Calidad** | **xUnit**, **Moq**, **FluentAssertions**, **Testcontainers** | Suite de pruebas unitarias y de integración que utiliza contenedores efímeros reales de SQL Server en Docker para validar consultas y transacciones. |
| **Despliegue y Ops** | **Docker**, **Docker Compose**, **Nginx** | Empaquetamiento de la aplicación (SPA + Web API + SQL Server) y proxy reverso con terminación SSL/TLS y enrutamiento `/api`. |

### Estrategia de persistencia de datos
* **Control de Concurrencia y Bloqueos de Transacción:** Las operaciones de asignación de reservas utilizan transacciones con nivel de aislamiento estricto y tokens de concurrencia optimista (`RowVersion` en EF Core) para mitigar condiciones de carrera (*race conditions*) en solicitudes simultáneas.
* **Almacenamiento de Archivos Adjuntos (Pre-informes e Imágenes):** La base de datos SQL Server no almacena archivos binarios (`BLOBs`). Los pre-informes PDF y las fotografías de espacios se persisten en un volumen administrado de Docker mediante `FileStorageService`, guardando en base de datos únicamente metadatos y rutas relativas.
* **Optimización de Lecturas:** Consultas de disponibilidad y catálogos de consulta general implementan `.AsNoTracking()` y proyecciones explícitas con `.Select()` para reducir la memoria requerida en el backend.

### Organización de componentes

El sistema se estructura mediante un diseño desacoplado que organiza los componentes tanto en el Backend (.NET 10 Web API) como en el Frontend (Angular 20 SPA), garantizando que las fronteras arquitectónicas se reflejen de forma física en el repositorio de código.

┌───────────────────────────────────────┐       ┌────────────────────────────────────────┐
│ FRONTEND (Angular 20)                 │       │ BACKEND (.NET 10)                      │
├───────────────────────────────────────┤       ├────────────────────────────────────────┤
│ Core (Auth, Guards, Interceptors)     │ ────► │ Presentation (Controllers, Filters)    │
│ Shared (UI Components, Modals, Pipes) │       │ Application (Use Cases, DTOs, Handlers)│
│ Features (Areas, Reservas, Check-in)  │       │ Domain (Entities, Value Objects, Rules)│
│ State Management (Signals & RxJS)     │       │ Infrastructure (EF Core, Identity, QR) │
└───────────────────────────────────────┘       └────────────────────────────────────────┘

#### 1. Estructura física de la solución Backend (.NET 10 Web API)
El backend se organiza bajo un esquema de **Monolito Modular** segregado en cuatro proyectos `.csproj` independientes que hacen cumplir la regla de dependencias a nivel de compilación:

```text
src/
├── Backend.Domain/                # Capa de Dominio (Núcleo puro C#, 0 dependencias externas)
│   ├── Entities/                  # Espacio, Puesto, Reserva, Usuario, Sancion, Incidencia
│   ├── ValueObjects/              # FranjaHoraria, RangoFechas, CodigoQR
│   ├── Enums/                     # EstadoReserva, CategoriaEspacio, TipoAprobacion
│   ├── Exceptions/                # DomainException, ConflictoHorarioException
│   └── Services/                  # MotorDisponibilidad, CalculadorTolerancia
│
├── Backend.Application/           # Capa de Aplicación (Casos de uso y orquestación)
│   ├── Interfaces/                # Puertos: IApplicationDbContext, IEmailService, IQrGenerator
│   ├── Modules/                   # Casos de uso agrupados por capacidad de negocio:
│   │   ├── Areas/                 # Commands, Queries, DTOs y Mappings de Áreas y Puestos
│   │   ├── Reservas/              # Lógica de Reservas Semestrales y Préstamo Directo
│   │   ├── Aprobaciones/          # Tramitación para Docentes y Administrativos
│   │   ├── Asistencia/            # Casos de uso de Check-in/out por QR y No-Show
│   │   └── Autenticacion/         # Interfaces de autenticación híbrida y perfiles
│   └── Common/                    # Resultados estandarizados (Result<T>), Paginación
│
├── Backend.Infrastructure/        # Capa de Infraestructura (Adaptadores y detalles tecnológicos)
│   ├── Persistence/               # ApplicationDbContext (EF Core 10), Fluent API Configurations, Migrations
│   ├── Identity/                  # ASP.NET Core Identity DbContext, Tokens JWT, Adaptador Entra ID
│   ├── Services/                  # QRCoderService, MailKitEmailService, LocalFileStorageService
│   └── BackgroundJobs/            # LiberacionNoShowHostedService (BackgroundService en segundo plano)
│
└── Backend.Presentation.Api/      # Capa de Presentación (Punto de entrada Web API REST)
    ├── Controllers/               # AreasController, ReservasController, AuthController, etc.
    ├── Middlewares/               # ExceptionHandlingMiddleware (RFC 7807), JwtAuthMiddleware
    ├── Filters/                   # ValidationFilter, AuthorizeRolesAttribute
    └── Program.cs                 # Contenedor de Inversión de Control (IoC) y pipeline HTTP
```
#### 2. Estructura modular del Frontend (Angular 20 SPA)
El cliente web adopta la convención basada en Standalone Components, eliminando NgModules innecesarios y optimizando el manejo de estado reactivo mediante Signals:

El frontend se organiza mediante la arquitectura modular oficial de Angular basada en **Standalone Components** y estructurada por **Feature Modules** (Módulos por Funcionalidad), optimizada con **Signals** para un manejo reactivo del estado:

```text
src/app/
├── core/                          # Servicios singleton globales y configuración transversal
│   ├── guards/                    # AuthGuard, RoleGuard (Admin, Docente, Estudiante)
│   ├── interceptors/              # JwtInterceptor (Bearer Token), ErrorInterceptor
│   ├── services/                  # AuthService, EntraIdMsalService, NotificationService
│   └── models/                    # Modelos comunes, contratos globales de respuesta
│
├── shared/                        # Componentes y utilidades reutilizables
│   ├── components/                # QrScannerModal (@zxing), CalendarPicker, Navbar, ModalConfirmacion
│   ├── directives/                # HasRoleDirective
│   └── pipes/                     # EstadoReservaPipe, FormatoFechaPipe
│
└── features/                      # Módulos de funcionalidad (Lazy Loaded por ruta)
    ├── auth/                      # Login local, Callback SSO Entra ID
    ├── areas/                     # Administración de áreas (Admin) y Catálogo visual (Estudiante)
    ├── reservas-semestrales/      # Interfaz de programación periódica de asignaturas (Docentes)
    ├── solicitudes-estudiante/    # Formulario de préstamo de puestos y radicación de laboratorios (PDF)
    ├── bandeja-aprobaciones/      # Panel de revisión/ajuste de solicitudes para Docente y Admin
    ├── control-asistencia/        # Módulo móvil de escaneo QR para Check-in y Check-out presencial
    └── dashboard-analitica/       # Gráficas de ocupación e indicadores para dirección
```

### Comparación de decisiones técnicas con alternativas descartadas

### Comparación con alternativas descartadas

#### Decisión 1: Backend (.NET 10 vs. Python/Django)
* **Necesidad:** Ejecutar algoritmos de concurrencia, matriz de disponibilidad de horarios y tareas programadas de liberación por inasistencia en segundo plano.
* **Alternativa descartada:** Python con Django REST Framework.
* **Comparativa:** Django simplifica el prototipado rápido, pero su concurrencia asíncrona depende de trabajadores externos (Celery + Redis), y el tipado dinámico incrementa la propensión a errores en refactorizaciones de lógica compleja. .NET 10 ofrece tipado estático nativo, paralelismo eficiente y tareas integradas (`BackgroundService`).
* **Justificación:** Se seleccionó .NET 10 por la solidez de sus contratos tipados, la facilidad para implementar Clean Architecture y la integración nativa de tareas en segundo plano.

#### Decisión 2: Persistencia (SQL Server 2022 vs. MongoDB)
* **Necesidad:** Garantizar consistencia ACID absoluta y prevenir doble-reserva horaria sobre un mismo espacio físico.
* **Alternativa descartada:** MongoDB (Base de datos NoSQL documental).
* **Comparativa:** MongoDB maneja con flexibilidad esquemas dinámicos, pero no provee bloqueos relacionales nativos en cruces de múltiples entidades (Espacio, Puesto, Horario, Usuario), trasladando la responsabilidad de la consistencia a nivel de aplicación.
* **Justificación:** El problema es intrínsecamente relacional. SQL Server garantiza integridad referencial y aislamiento transaccional a nivel de motor de base de datos.

#### Decisión 3: Seguridad (Esquema Híbrido vs. Solo SSO Entra ID)
* **Necesidad:** Compatibilizar el acceso ágil de la comunidad institucional con mecanismos de contingencia para administración y soporte.
* **Alternativa descartada:** Proveedor exclusivo de identidad externa (Microsoft Entra ID únicamente).
* **Comparativa:** Depender al 100% de Entra ID paraliza la operación administrativa en caso de fallos de red en el inquilino (*tenant*) o problemas de sincronización de licencias.
* **Justificación:** El esquema híbrido preserva la comodidad del SSO para estudiantes y docentes mediante credenciales institucionales, manteniendo un acceso local aislado con ASP.NET Core Identity para el personal administrativo y de soporte técnico.

## 4) Estrategia de desarrollo

### Metodología de trabajo: SDD con OpenSpec
El proyecto adopta **Spec-Driven Development (SDD)** con el soporte de **OpenSpec**. La especificación de requisitos formalizada es la **fuente única de verdad** (*Single Source of Truth*). Ninguna funcionalidad se codifica sin una propuesta autorizada en el repositorio.

```
┌─────────────────────────────────────────────────────────┐
│ 1. Especificación (OpenSpec CLI / Formato BDD)          │
│    Diseño de contratos OpenAPI, esquemas EF Core y G/W/T│
└───────────────────────────┬─────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────┐
│ 2. Validación de Consenso Humano                        │
│    Revisión y validación formal: `openspec validate`    │
└───────────────────────────┬─────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────┐
│ 3. Implementación Asistida por IA (Gemini CLI)          │
│    Generación acotada de código conforme a la spec      │
└───────────────────────────┬─────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────┐
│ 4. Verificación Automatizada y Cierre                   │
│    Ejecución de pruebas unitarias / Commit atómico      │
└─────────────────────────────────────────────────────────┘
```

### Etapas de implementación y entregables

```
   Sprint 1 - 2          Sprint 3 - 4          Sprint 5 - 6             Sprint 7
┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐
│     ETAPA 1     │   │     ETAPA 2     │   │     ETAPA 3     │   │     ETAPA 4     │
│ MVP Catálogo,   ├──►│ Semestrales,    ├──►│ Operación QR,   ├──►│ Sanciones,      │
│ Auth Híbrida y  │   │ Conflictos y    │   │ Asistencia y    │   │ Analítica y     │
│ Préstamo Puesto │   │ Aprobaciones    │   │ Liberación      │   │ Despliegue      │
└─────────────────┘   └─────────────────┘   └─────────────────┘   └─────────────────┘
```

* **Etapa 1: MVP — Núcleo de Infraestructura, Autenticación y Catálogo Base (Sprints 1 y 2):**
  * *Entregable:* Solución base en .NET 10 (Clean Architecture), Docker Compose funcional, autenticación híbrida (Entra ID + Identity con JWT), catálogo visual en Angular 20, módulo de alta de espacios con generación de QR e interfaces para préstamo individual directo de puestos de cómputo.
* **Etapa 2: Reservas Semestrales y Flujos de Aprobación (Sprints 3 y 4):**
  * *Entregable:* Algoritmo de detección de conflictos en el dominio, reserva periódica para docentes, bandeja de aprobación de solicitudes con carga de pre-informes PDF y módulo de mensajería SMTP con MailKit.
* **Etapa 3: Control Operativo QR, Asistencia y Tareas en Segundo Plano (Sprints 5 y 6):**
  * *Entregable:* Lector web QR con `@zxing/ngx-scanner`, validación presencial de *Check-in/out* contra identificador estático del aula, y servicio hospedado (`BackgroundService`) para liberación automática por inasistencia (*No-Show*).
* **Etapa 4: Sanciones, Analítica y Despliegue en Producción (Sprint 7):**
  * *Entregable:* Cálculo automatizado de suspensiones a usuarios por ausencias recurrentes, panel de analítica de ocupación institucional y despliegue final contenerizado con Nginx.


### Orden de desarrollo técnico de componentes
1. **Infraestructura Base y Seguridad:** `ApplicationDbContext`, configuración de migraciones, ASP.NET Core Identity y autenticación JWT / Entra ID.
2. **Entidades del Espacio Físico y Códigos QR:** Creación de entidades `Espacio`, `Puesto`, parametrización de políticas y servicio `QRCoder`.
3. **Motor Base de Disponibilidad y Préstamo Directo:** Servicio de dominio para consultas horarias libres de colisiones y reservas automáticas de puestos.
4. **Algoritmo de Reservas Recurrentes Semestrales:** Procesamiento masivo de frecuencias semanales y quincenales para asignaturas de docentes.
5. **Bandeja de Moderación y Carga de Archivos:** Flujo contingente de estados (`Pendiente`, `Aprobado`, `Rechazado`), subida de PDFs y notificaciones SMTP.
6. **Módulo de Asistencia QR en Frontend:** Componente de lectura de cámara en Angular 20 y validación de presencia física en el servidor.
7. **Motor de Tareas en Segundo Plano:** `BackgroundService` para liberación automática y cálculo de penalizaciones por inasistencia.
8. **Analítica y Empaquetamiento de Producción:** Endpoints de agregación de métricas, tablero directivo y orquestación con Docker Compose / Nginx.

### Dependencias entre componentes

Para garantizar el desacoplamiento, la mantenibilidad y la posibilidad de realizar pruebas unitarias aisladas, las dependencias entre los componentes del sistema se estructuran siguiendo la **Regla de Dependencias de Clean Architecture** en el Backend.

---

### MATRIZ DE DEPENDENCIAS DE COMPILACIÓN

### 1. Matriz de dependencias de compilación en el Backend (.NET 10)

En el backend, las dependencias entre proyectos de la solución C# son estrictamente **unidireccionales hacia el núcleo (Capa de Dominio)**. Ningún componente interno conoce los detalles de las capas externas.


│ Capa del Backend │ Depende estrictamente de: │
| --- | --- |
│ Presentation (Web API) │ Application, Infrastructure │
│ Infrastructure │ Application, Domain │
│ Application │ Domain │
│ Domain │ Ninguna (Núcleo Puro C#) │

* **Capa de Dominio (`Backend.Domain`):** No posee dependencias de ningún paquete externo, ORM ni framework. Define las entidades pura de C#, excepciones de negocio y las interfaces de los servicios de dominio (`IMotorDisponibilidad`).
* **Capa de Aplicación (`Backend.Application`):** Depende exclusivamente de `Backend.Domain`. Define las interfaces de infraestructura (puertos como `IApplicationDbContext`, `IEmailService`, `IQrService`) y los DTOs de transporte.
* **Capa de Infraestructura (`Backend.Infrastructure`):** Depende de `Backend.Application` y `Backend.Domain`. Implementa las interfaces mediante tecnologías concretas (`EF Core 10`, `MailKit`, `QRCoder`, `ASP.NET Core Identity`).
* **Capa de Presentación (`Backend.Presentation.Api`):** Depende de `Backend.Application` para invocar los casos de uso y de `Backend.Infrastructure` únicamente en el punto de entrada (`Program.cs`) para registrar las dependencias en el contenedor de IoC (*Inversion of Control*).


### Definición de Terminado (Definition of Done - DoD)
Una etapa se considerará formalmente concluida únicamente si:
1. Las especificaciones correspondientes en `.openspec/` superan la validación sintáctica (`openspec validate`) y se encuentran consolidadas.
2. El 100% de las pruebas unitarias y de integración de la etapa compilan y pasan exitosamente sin errores no controlados.
3. El código respeta el aislamiento de capas de Clean Architecture y no expone entidades directas del ORM en los controladores REST.
4. El incremento se consolida mediante un commit atómico en Git con un mensaje descriptivo y trazable hacia la especificación.

---

### 5) Estrategia de calidad

### Tipos de pruebas planificadas

```
      ▲
     / \     Pruebas E2E (Manuales / Flujo Web)
    /───\    
   /     \   Pruebas de Integración (Testcontainers + SQL Server)
  /───────\  
 /         \ Pruebas Unitarias (xUnit + Moq + FluentAssertions)
└───────────┘
```

1. **Pruebas Unitarias (Capa de Dominio y Aplicación):**
   * *Alcance:* Validación aislada de algoritmos de disponibilidad horaria, detección de traslapes en reservas semestrales, cálculo de márgenes de tolerancia para *check-in* y reglas de suspensión de usuarios por inasistencia.
   * *Herramientas:* `xUnit`, `Moq` (para sustitución de repositorios y servicios externos) y `FluentAssertions`.
2. **Pruebas de Integración (Capa de Infraestructura y Web API):**
   * *Alcance:* Validación de las transacciones en base de datos, consultas LINQ sobre EF Core 10, prevención de condiciones de carrera y verificación de controladores REST con sus filtros y middlewares.
   * *Herramientas:* `Testcontainers` (para instanciar contenedores de SQL Server 2022 en Docker) y `WebApplicationFactory`.
3. **Pruebas Funcionales y de Interfaz (Frontend Angular 20):**
   * *Alcance:* Comprobación de componentes de formulario, interceptores HTTP para el token Bearer JWT, directivas de control de roles y modal del escáner de códigos QR.
4. **Pruebas de Carga y Concurrencia:**
   * *Alcance:* Emulación de solicitudes de reserva simultáneas sobre un mismo equipo o aula física para certificar que el motor de persistencia evite el doble-apartado mediante bloqueos de concurrencia.

### Matriz de riesgos del proyecto y planes de mitigación

| ID | Riesgo Identificado | Prob. | Impacto | Estrategia de Prevención y Mitigación | Verificación de Mitigación |
| :---: | :--- | :---: | :---: | :--- | :--- |
| **R1** | **Condición de carrera (*Race Condition*) y doble-reserva simultánea de un mismo espacio** | Media | Crítico | Implementación de transacciones con aislamiento y tokens de concurrencia optimista (`RowVersion` en EF Core) en el guardado de la reserva. | Pruebas de integración concurrentes automatizadas simulando hilos paralelos sobre el mismo recurso. |
| **R2** | **Indisponibilidad o fallo de sincronización con Microsoft Entra ID institucional** | Baja | Alto | Adopción de la arquitectura híbrida que conserva credenciales locales con ASP.NET Core Identity para el personal administrativo y soporte. | Prueba de conmutación simulando fallo de conectividad externa con Entra ID y verificación del login local. |
| **R3** | **Fraude presencial por duplicación o captura fotográfica del código QR estático** | Alta | Medio | Validación combinada en el servidor: el escaneo solo es válido si la solicitud proviene de una sesión autenticada con reserva activa en ese minuto exacto (ventana de tolerancia acotada). | Intentos de escaneo fuera de la ventana horaria o sin reserva asociada deben arrojar rechazo HTTP 400/409. |
| **R4** | **Bloqueo o caída de la tarea en segundo plano para liberación por inasistencia (*No-Show*)** | Baja | Alto | Diseño del `BackgroundService` con manejo estructurado de excepciones por bloque, registro en logs (*Serilog*) y persistencia atómica por lote procesado. | Monitoreo de logs y prueba de inyección de errores para verificar que el servicio no finalice el proceso host. |
| **R5** | **Agotamiento de almacenamiento o cuello de botella por carga de pre-informes PDF** | Media | Medio | Imposición de validaciones estrictas en la API: tamaño máximo por archivo (ej. 5 MB), verificación de firma de cabecera (*Magic Numbers*) y cuotas por área. | Pruebas de carga de archivos que excedan el límite o con extensiones adulteradas esperando rechazo HTTP 413/415. |

### Mecanismos para verificar el cumplimiento de requisitos
* **Matriz de Trazabilidad:** Correspondencia biunívoca entre los casos de negocio, el artefacto OpenSpec, el controlador REST y la prueba unitaria que valida su cumplimiento.
* **Revisión de Contratos de API:** Verificación estricta de esquemas JSON de entrada y salida contra la especificación OpenAPI antes de validar cualquier integración en Angular 20.
* **Criterios de Aceptación BDD:** Cada requerimiento debe superar los escenarios de prueba redactados bajo la estructura formal:
  * *Dado que* un usuario tiene una reserva aprobada en un laboratorio a las 10:00.
  * *Cuando* transcurren 15 minutos sin registrar lectura presencial del QR.
  * *Entonces* el sistema cambia el estado a `LiberadaPorNoShow` y el laboratorio vuelve a estar disponible para el público.

---

## 6. Uso de SDD e inteligencia artificial

### Propósito de adoptar SDD y OpenSpec
El desarrollo guiado por especificaciones (**SDD**) garantiza que el desarrollo del sistema esté fundamentado en modelos formales y verificables en lugar de en la improvisación de código. **OpenSpec** actúa como el protocolo y la herramienta operativa para:
1. Evitar la ambigüedad en contratos de software y modelos de base de datos antes de escribir código.
2. Servir como el canal de comunicación estructurado y fuente de verdad técnica para las herramientas de asistencia por IA.
3. Asegurar un registro histórico y trazable de todas las modificaciones introducidas al sistema a lo largo del ciclo de vida del proyecto.

### Transformación del plan de ingeniería en especificaciones
1. **Identificación de la Capacidad:** A partir de las etapas definidas, se selecciona una funcionalidad aislada (ej. *Parametrización de Áreas*).
2. **Generación de la Propuesta OpenSpec:** Mediante la línea de comandos de OpenSpec, se inicializa un cambio que define:
   * Esquema de entidades de dominio y DTOs requeridos.
   * Contrato de endpoints HTTP (verbos, rutas, códigos de estado RFC 7807).
   * Reglas de negocio expresadas como criterios de aceptación en sintaxis Gherkin/BDD (*Given/When/Then*).
3. **Validación Formal:** Se ejecuta `openspec validate` para garantizar que la especificación no contenga contradicciones técnicas, vacíos de parámetros ni conflictos con las capas del sistema.

### Delegación de actividades a la IA y supervisión humana
El uso de inteligencia artificial se canaliza mediante la interfaz de **Gemini CLI**, estableciendo una clara división de responsabilidades:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        DIVISIÓN DE RESPONSABILIDADES                   │
├───────────────────────────────────┬────────────────────────────────────┤
│   ASISTENTE IA (Gemini CLI)       │       ESTUDIANTE (Líder Técnico)   │
├───────────────────────────────────┼────────────────────────────────────┤
│ • Generación de código estándar   │ • Validación y consenso de specs   │
│   (Controllers, Mappings, DTOs).  │   mediante `openspec validate`.    │
│ • Redacción de pruebas unitarias  │ • Supervisión y diseño del modelo  │
│   e integración basadas en BDD.   │   de dominio institucional.        │
│ • Diagnóstico de fallos en logs   │ • Aprobación manual de cambios y   │
│   y asistencia en refactorización.│   verificación de Clean Arch.      │
│ • Ejecución e interpretación de   │ • Ejecución de commits en Git      │
│   pruebas automatizadas.          │   y responsabilidad académica.     │
└───────────────────────────────────┴────────────────────────────────────┘
```

* **Actividades Delegadas a Gemini CLI:**
  1. *Generación de Plantillas y Código de Infraestructura:* Escritura de clases de mapeo de Entity Framework Core, controladores Web API con inyección de dependencias y componentes base de Angular 20 estrictamente ceñidos a la especificación activa.
  2. *Generación y Asistencia en la Ejecución de Pruebas Unitarias:* Gemini CLI formula la suite de pruebas unitarias (`xUnit`) para cada caso de uso; al terminar la implementación, ejecuta las pruebas mediante comandos de terminal, analiza la salida del compilador o ejecutor de pruebas y emite un diagnóstico evaluando si el código satisface los criterios de la especificación.
  3. *Detección de Malos Olores de Código (*Code Smells*):* Auditoría rápida para identificar violaciones al aislamiento de capas (ej. detectar si una entidad de EF Core está intentando filtrarse en un controlador de presentación).
* **Actividades Exclusivas del Estudiante (Supervisión Humana):**
  1. *Aprobación de la Especificación:* Ninguna especificación de OpenSpec se da por válida sin la revisión y decisión crítica del estudiante.
  2. *Control de Arquitectura:* Verificación de que la IA no introduzca acoplamientos innecesarios ni dependencias externas en la Capa de Dominio.
  3. *Gobierno del Repositorio y Versionado:* Autorización y ejecución formal de los commits en Git vinculados al ciclo de trabajo de OpenSpec. El estudiante asume la autoría y defensa técnica de cada componente implementado.

### Condiciones de aceptación del código generado por IA
Para que un bloque de código producido por Gemini CLI sea aceptado en el proyecto, debe satisfacer obligatoriamente los siguientes filtros:
1. **Alineación con la Especificación:** El código debe ceñirse con exactitud a los contratos definidos en el archivo `.openspec` correspondiente, sin incorporar métodos accesorios o funcionalidades inventadas fuera de alcance.
2. **Superación Exitosa de Pruebas:** Las pruebas unitarias redactadas para la funcionalidad deben compilar y pasar con un 100% de éxito en el entorno local.
3. **Limpieza Técnica:** Ausencia de advertencias (*compiler warnings*), observancia estricta del estándar de nombres en C# / TypeScript y respeto al principio de responsabilidad única (SRP).

