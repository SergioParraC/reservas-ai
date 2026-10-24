# 🏛️ Sistema de Gestión de Reservas de Espacios Universitarios

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular 20](https://img.shields.io/badge/Angular-20-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![SQL Server 2022](https://img.shields.io/badge/SQL%20Server-2022-CC292B?logo=microsoft-sql-server&logoColor=white)](https://www.microsoft.com/sql-server/)
[![Docker](https://img.shields.io/badge/Docker-Enabled-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20Onion-success)](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures)
[![Methodology](https://img.shields.io/badge/Methodology-SDD%20with%20OpenSpec-blueviolet)](https://github.com/)

> **Plataforma web corporativa de alto rendimiento para la centralización, parametrización y reserva inteligente de infraestructura física universitaria (laboratorios, salas de cómputo, cubículos y auditorios), con control presencial mediante códigos QR estáticos y liberación automática por inasistencia (*no-show*).**

---

<p align="center">
  <a href="https://workout-cooked-ingredients-clicking.trycloudflare.com/">🌐 Visitar Aplicación en producción</a>
</p>

---

## 📌 Tabla de Contenidos

1. [El Problema](#-el-problema)
2. [Nuestra Solución](#-nuestra-solución)
3. [Actores del Sistema](#-actores-del-sistema)
4. [Características Principales](#-características-principales)
5. [Arquitectura del Sistema](#-arquitectura-del-sistema)
6. [Stack Tecnológico](#-stack-tecnológico)
7. [Estructura del Proyecto](#-estructura-del-proyecto)
8. [Estrategia de Calidad y Mitigación de Riesgos](#-estrategia-de-calidad-y-mitigación-de-riesgos)
9. [Metodología de Desarrollo (SDD & IA)](#-metodología-de-desarrollo-sdd--ia)
10. [Instalación y Despliegue](#-instalación-y-despliegue)

---

## ⚠️ El Problema

Actualmente, muchas instituciones educativas coordinan la asignación de recintos físicos mediante hojas de cálculo, llamadas y correos electrónicos informales. Esto ocasiona:

- **Sobre-reservas (*Double-booking*):** Solicitudes concurrentes y cruces de horarios en laboratorios y auditorios.
- **Asimetría de Información:** Desconocimiento de la disponibilidad real de espacios y equipos en tiempo real.
- **Inasistencia No Controlada (*No-Show*):** Espacios reservados que permanecen vacíos, impidiendo que otros usuarios los aprovechen.
- **Sobrecarga Administrativa:** Tiempos excesivos invertidos en cuadrar agendas y autorizaciones manuales.

---

## 💡 Nuestra Solución

Un sistema web integral diseñado bajo **Clean Architecture** y principios de consistencia transaccional ACID:

- **Gestión y parametrización granular:** Configuración por área de aforos, políticas de aprobación, ventanas de antelación y franjas de mantenimiento.
- **Flujos de aprobación multinivel:** Auto-confirmación inmediata para puestos individuales, aprobación docente para laboratorios con pre-informe PDF y aprobación administrativa para auditorios.
- **Control presencial estático por QR:** Registro de asistencia (*Check-in*) y salida (*Check-out*) mediante escáner web en dispositivos móviles.
- **Liberación automatizada por inasistencia:** Servicio en segundo plano (*BackgroundService*) que cancela la reserva y libera el recurso si se supera el margen de tolerancia configurado.
- **Reservas recurrentes semestrales:** Asignación planificada para docentes (semanal, quincenal o por calendario específico).

---

## 👥 Actores del Sistema

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
│• Alta de áreas │             │• Planificación │             │• Autogestión   │
│• Políticas     │             │  semestral     │             │  puestos       │
│• Auditorios    │             │• Validación de │             │• Solicitud lab │
│• Mantenimiento │             │  prácticas     │             │  (adjunto PDF) │
│• Analítica     │             │• Bandeja doc.  │             │• Check-in QR   │
└────────────────┘             └────────────────┘             └────────────────┘
```

- **Administrativos:** Parametrizan el campus, aprueban auditorios, configuran franjas de aseo/soporte y gestionan sanciones.
- **Profesores:** Planifican reservas recurrentes para sus asignaturas semestrales y aprueban solicitudes de laboratorio revisando pre-informes técnicos.
- **Estudiantes:** Reservan puestos individuales al instante, radican solicitudes de laboratorio adjuntando justificaciones en PDF y validan su asistencia escaneando el QR del recinto.

---

## ✨ Características Principales

| Característica | Descripción |
| :--- | :--- |
| **Autenticación Híbrida** | Acceso institucional vía **Microsoft Entra ID (SSO)** para estudiantes y docentes, complementado con **ASP.NET Core Identity** local para administración y soporte. |
| **Prevención de Conflictos** | Motor transaccional con tokens de concurrencia optimista (`RowVersion`) para garantizar cero colisiones horarias. |
| **Gestión Documental PDF** | Carga y validación obligatoria de pre-informes técnicos con firma mágica de cabecera (*Magic Numbers*). |
| **Control Presencial QR** | Escáner integrado en navegador (`@zxing/ngx-scanner`) contra códigos QR estáticos inmutables generados vectorialmente. |
| **Liberación No-Show** | Proceso en segundo plano que revoca la reserva y notifica al usuario si no realiza *check-in* en el tiempo de tolerancia. |
| **Módulo de Sanciones** | Suspensión preventiva temporal del perfil tras incurrir en inasistencias injustificadas reiteradas. |
| **Tablero de Analítica** | Indicadores directivos de porcentaje de ocupación, horas pico y aprovechamiento de infraestructura. |

---

## 🏗️ Arquitectura del Sistema

El backend está concebido como un **Monolito Modular** estructurado en **Clean Architecture (Onion)**, garantizando que el núcleo de dominio permanezca 100% puro y aislado de librerías externas o detalles de persistencia.

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

### Reglas de Dependencias
- **Domain:** Núcleo puro en C# sin dependencias externas. Contiene entidades, reglas de negocio y contratos de servicios de dominio.
- **Application:** Orquesta los casos de uso (Commands/Queries), DTOs y define interfaces de servicios (Puertos). Depende únicamente de *Domain*.
- **Infrastructure:** Implementa persistencia (EF Core 10), identidad híbrida, envío de correos (MailKit) y tareas en segundo plano. Depende de *Application* y *Domain*.
- **Presentation:** Endpoints HTTP RESTful, middlewares y filtros de excepción. Depende de *Application* e *Infrastructure* (para inyección de dependencias).

---

## 💻 Stack Tecnológico

| Componente | Tecnología | Propósito |
| :--- | :--- | :--- |
| **Frontend SPA** | **Angular 20** + **TypeScript** | Framework empresarial reactivo con *Standalone Components* y *Signals*. |
| **Escáner QR** | **@zxing/ngx-scanner** | Acceso nativo por cámara web/móvil para lectura de QR sin app instalada. |
| **Backend REST API** | **.NET 10 (ASP.NET Core)** | API de alto desempeño con inyección de dependencias nativa y tareas asíncronas. |
| **Persistencia & ORM** | **SQL Server 2022** + **EF Core 10** | Consistencia ACID, migraciones *Code-First* y concurrencia optimista. |
| **Autenticación** | **Microsoft Entra ID** + **JWT / Identity** | Autenticación híbrida para acceso corporativo y cuentas administrativas locales. |
| **Generador de QR** | **QRCoder** | Renderizado vectorial (SVG/PNG) inmutable para etiquetas físicas de espacios. |
| **Mensajería** | **MailKit & MimeKit** | Notificaciones asíncronas y alertas por correo vía SMTP. |
| **Testing** | **xUnit**, **Moq**, **FluentAssertions**, **Testcontainers** | Suite completa de pruebas unitarias y de integración sobre contenedores reales. |
| **Despliegue** | **Docker** & **Docker Compose** + **Nginx** | Empaquetamiento contenerizado y proxy inverso con soporte SSL. |

---

## 📂 Estructura del Proyecto

```text
├── src/
│   ├── Backend.sln                    # Solución .NET 10 con las cuatro capas y las pruebas de arquitectura
│   ├── Backend.Domain/                # Entidades, Value Objects, Excepciones y Motor de Disponibilidad
│   ├── Backend.Application/           # Casos de uso, DTOs, Mapeos, Interfaces y Validaciones
│   ├── Backend.Infrastructure/        # EF Core DbContext, Migraciones, Identity, Correo y BackgroundJobs
│   └── Backend.Presentation.Api/      # Controladores REST, Middlewares, Filtros y Program.cs
│
├── frontend/                          # Cliente SPA Angular 20
│   └── src/app/
│       ├── core/                      # Guards, Interceptores JWT y Servicios Globales
│       ├── shared/                    # Componentes UI reutilizables, Modales y Pipes
│       └── features/                  # Módulos: auth, areas, reservas, aprobaciones, asistencia
│
├── tests/
│   ├── Backend.ArchitectureTests/     # NetArchTest: regla de dependencias y pureza del dominio
│   ├── Backend.UnitTests/             # Pruebas unitarias de dominio y casos de uso
│   └── Backend.IntegrationTests/      # Pruebas de integración con Testcontainers (SQL Server)
│
├── Directory.Build.props               # Nullable, ImplicitUsings y TreatWarningsAsErrors comunes
├── docker-compose.yml                 # Orquestación de servicios (API, SPA, SQL Server, Nginx)
└── PLAN_INGENIERIA.md                 # Documento de Planificación e Ingeniería Integral
```

---

## 🛡️ Estrategia de Calidad y Mitigación de Riesgos

- **Condiciones de Carrera (Doble Reserva):** Transacciones atómicas con aislamiento estricto y token de concurrencia optimista (`RowVersion`).
- **Resiliencia de Autenticación:** El modelo híbrido garantiza la operatividad administrativa ante caídas o desincronizaciones de Microsoft Entra ID.
- **Prevención de Fraude en Check-in:** La lectura del QR estático solo es aprobada si existe una reserva activa para ese usuario en la franja horaria y ventana de tolerancia exacta.
- **Seguridad en Carga de Archivos:** Validación de tamaño máximo (5 MB) e inspección de números mágicos de cabecera PDF en el servidor.
- **Pirámide de Pruebas:** Pruebas unitarias sobre las reglas de negocio y pruebas de integración utilizando contenedores efímeros de base de datos vía `Testcontainers`.

---

## 📐 Metodología de Desarrollo (SDD & IA)

El proyecto adopta **Spec-Driven Development (SDD)** con el soporte de **OpenSpec**:
1. **Especificación Formal:** Requisitos, esquemas y criterios de aceptación BDD (*Given/When/Then*) se definen como la única fuente de verdad antes de codificar.
2. **Validación de Consenso:** Uso del CLI `openspec validate` para garantizar coherencia arquitectónica.
3. **Implementación Asistida por IA (DeepSeek – modelo DeepSeek Flash):** Generación acotada de código estándar y pruebas unitarias supervisadas por el desarrollador técnico.
4. **Cierre y Definición de Terminado (DoD):** Cobertura de pruebas al 100%, respeto estricto de capas Clean Architecture y commits atómicos.

---

## 🚀 Instalación y Despliegue

### Prerrequisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js 22+](https://nodejs.org/) y npm
- [Docker Desktop](https://www.docker.com/)

### Compilación y Pruebas del Backend

```bash
# Restaurar los paquetes NuGet de la solución
dotnet restore src/Backend.sln

# Compilar la solución completa (cualquier advertencia rompe la compilación)
dotnet build src/Backend.sln

# Ejecutar todas las pruebas de la solución (arquitectura + unitarias)
dotnet test src/Backend.sln
```

`Directory.Build.props` habilita `Nullable`, `ImplicitUsings` y `TreatWarningsAsErrors=true` en todos los proyectos, por lo que una advertencia del compilador se reporta como error.

La persistencia de identidad (EF Core 10 + SQL Server 2022), los comandos de migración y la
política de seed están documentados en [`docs/persistencia.md`](docs/persistencia.md).
La autenticación híbrida (JWT local + Entra ID), las variables de configuración y el catálogo de
errores RFC 7807 están en [`docs/autenticacion.md`](docs/autenticacion.md).

### Verificación de la Regla de Dependencias

```bash
dotnet list src/Backend.Domain reference
dotnet list src/Backend.Application reference
dotnet list src/Backend.Infrastructure reference
dotnet list src/Backend.Presentation.Api reference
dotnet list src/Backend.Domain package
```

Resultado esperado: `Backend.Domain` sin referencias de proyecto ni paquetes, `Backend.Application` → `Backend.Domain`, `Backend.Infrastructure` → `Backend.Application` y `Backend.Domain`, y `Backend.Presentation.Api` → `Backend.Application` y `Backend.Infrastructure`.

### Ejecución Rápida con Docker Compose

1. Clona el repositorio:
   ```bash
   git clone https://github.com/tu-usuario/gestion-reservas-universitarias.git
   cd gestion-reservas-universitarias
   ```

2. Levanta la infraestructura completa (Base de datos, Backend API y Frontend SPA):
   ```bash
   docker-compose up --build -d
   ```

3. Accede a los servicios:
   - **Frontend Web SPA:** `http://localhost:4200`
   - **Documentación Swagger / OpenAPI:** `http://localhost:5000/swagger`
   - **SQL Server 2022:** `localhost,1433`

---

## 📄 Licencia

Este proyecto se distribuye bajo la licencia **MIT**. Consulta el archivo `LICENSE` para más información.
