# architecture/clean-architecture-baseline Specification

## Purpose
Define la fundación compilable del backend como una solución en cuatro capas con dependencias unidireccionales hacia el dominio, de modo que las reglas del negocio universitario permanezcan aisladas de frameworks, del ORM y de los detalles de despliegue.

## Requirements

### Requirement: Compilación de la solución en cuatro capas

El sistema SHALL compilarse como una solución .NET que produce cuatro ensamblados independientes y desplegables en conjunto: dominio, aplicación, infraestructura y presentación (API REST).

#### Scenario: Compilación limpia de la solución base

- **GIVEN** el repositorio recién clonado con el SDK de .NET 10 instalado
- **WHEN** se compila la solución completa en configuración Release
- **THEN** se generan los cuatro ensamblados (dominio, aplicación, infraestructura y API)
- **AND** la compilación finaliza con código de salida 0 y sin advertencias del compilador

#### Scenario: Compilación del cliente web base

- **GIVEN** las dependencias del cliente instaladas con el gestor de paquetes de Node
- **WHEN** se ejecuta la compilación de producción de la aplicación Angular 20
- **THEN** se genera el paquete estático de la SPA sin errores
- **AND** la compilación no reporta componentes declarados fuera del estilo Standalone Components

### Requirement: Dependencias unidireccionales hacia el núcleo

Cada capa SHALL referenciar únicamente capas interiores: presentación referencia aplicación e infraestructura; infraestructura referencia aplicación y dominio; aplicación referencia dominio.

#### Scenario: Referencias permitidas entre capas

- **GIVEN** la solución compilada
- **WHEN** se inspeccionan las referencias de compilación declaradas por cada proyecto
- **THEN** la capa de aplicación referencia únicamente al dominio
- **AND** la capa de infraestructura referencia únicamente a aplicación y dominio
- **AND** la capa de presentación referencia únicamente a aplicación e infraestructura

#### Scenario: Intento de referencia desde el dominio hacia una capa exterior

- **GIVEN** un desarrollador que agrega una referencia del proyecto de dominio hacia el proyecto de infraestructura
- **WHEN** se compila la solución
- **THEN** la compilación se rechaza o la verificación de arquitectura falla
- **AND** el mensaje identifica el dominio como capa que no admite dependencias salientes

### Requirement: Núcleo de dominio libre de dependencias tecnológicas

El proyecto de dominio SHALL permanecer en C# puro, sin paquetes de persistencia, framework web, proveedor de identidad ni bibliotecas de terceros.

#### Scenario: Dominio sin dependencias tecnológicas

- **GIVEN** el proyecto de dominio
- **WHEN** se revisan sus dependencias declaradas y los tipos importados por sus entidades y reglas
- **THEN** no declara paquetes de ORM, web, identidad ni utilidades de terceros
- **AND** ninguna entidad, objeto de valor o servicio de dominio importa tipos de esas tecnologías

#### Scenario: Publicación de contratos de persistencia fuera del dominio

- **GIVEN** la necesidad de exponer la colección de usuarios y roles a la capa de aplicación
- **WHEN** se define el contrato de acceso a datos
- **THEN** el contrato reside en la capa de aplicación como puerto
- **AND** el dominio no expone ni referencia el contexto de base de datos ni tipos del ORM

### Requirement: Verificación automatizada de la regla de dependencias

La solución SHALL incluir una prueba automatizada de arquitectura que falle ante cualquier dependencia prohibida y que se ejecute como parte de la validación de integración continua.

#### Scenario: Detección de una dependencia prohibida

- **GIVEN** una referencia de compilación desde el dominio hacia la infraestructura
- **WHEN** se ejecuta la suite de pruebas de arquitectura
- **THEN** la prueba falla indicando el par de capas en conflicto
- **AND** el pipeline de integración continua no aprueba el cambio

#### Scenario: Línea base de arquitectura aprobada

- **GIVEN** la solución sin violaciones de la regla de dependencias
- **WHEN** se ejecuta la suite de pruebas de arquitectura junto con las pruebas unitarias de la etapa
- **THEN** todas las pruebas finalizan exitosamente
- **AND** el reporte de la suite confirma la ausencia de referencias cruzadas entre capas exteriores

### Requirement: Prohibición de exponer entidades del ORM en la capa de presentación

La capa de presentación SHALL intercambiar únicamente DTOs de entrada y salida, sin exponer ni recibir tipos del modelo de persistencia o entidades del ORM.

#### Scenario: Respuesta de la API con entidad de persistencia

- **GIVEN** un endpoint que obtiene un usuario desde el contexto de datos
- **WHEN** se construye la respuesta HTTP
- **THEN** el cuerpo se compone a partir de un DTO de la capa de aplicación
- **AND** ningún tipo del ensamblado de infraestructura aparece en las firmas públicas de los controladores

#### Scenario: Verificación de firmas de la capa de presentación

- **GIVEN** la solución compilada
- **WHEN** se ejecuta la prueba de arquitectura sobre los controladores y filtros de la API
- **THEN** las firmas solo referencian tipos de presentación y de aplicación
- **AND** la prueba falla si se detecta un tipo de infraestructura o de ORM en una firma pública
