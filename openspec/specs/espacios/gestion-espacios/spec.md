# espacios/gestion-espacios Specification

## Purpose
Permite administrar la infraestructura física de la universidad (áreas y puestos) con un identificador QR inmutable por unidad, y publicar ese inventario en un catálogo consultable por la comunidad.

## Requirements

### Requirement: Registro de áreas con datos obligatorios

El sistema SHALL registrar un área con nombre, ubicación, categoría, aforo, tipo de aprobación y estado, y SHALL asignarle un código QR propio en el momento del alta.

#### Scenario: Alta de un área válida

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** envía a `POST /api/espacios` un nombre, una ubicación, una categoría, un aforo y un tipo de aprobación válidos
- **THEN** responde `201 Created` con la ubicación del recurso, el identificador asignado, el código QR generado y el estado activo
- **AND** el área aparece de inmediato en el listado administrativo

#### Scenario: Alta con nombre repetido en la misma ubicación

- **GIVEN** un área activa llamada "Laboratorio de Redes" en el bloque B
- **WHEN** se intenta registrar otra área con el mismo nombre y la misma ubicación
- **THEN** responde `409 Conflict` con un problema RFC 7807 que identifica el conflicto de nombre
- **AND** no se crea un segundo registro ni se consume un código QR

#### Scenario: Alta con datos obligatorios ausentes

- **GIVEN** una solicitud de alta sin nombre o sin ubicación
- **WHEN** el sistema valida el cuerpo de la solicitud
- **THEN** responde `400 Bad Request` en formato RFC 7807 con el detalle del campo faltante
- **AND** no se persiste ningún área

### Requirement: Validación del aforo declarado

El sistema SHALL rechazar un aforo menor que uno o superior al máximo institucional configurado, sin crear ni modificar el área.

#### Scenario: Aforo cero o negativo

- **GIVEN** una solicitud de alta con aforo `0` o `-5`
- **WHEN** el sistema evalúa la regla de aforo
- **THEN** responde `400 Bad Request` con el campo `aforo` señalado en el problema
- **AND** el área no se crea

#### Scenario: Aforo superior al máximo institucional

- **GIVEN** un máximo institucional configurado de 500 personas
- **WHEN** se registra un área con aforo `501`
- **THEN** responde `400 Bad Request` con un mensaje que indica el máximo permitido
- **AND** el área no se crea

#### Scenario: Aforo en el límite permitido

- **GIVEN** un máximo institucional configurado de 500 personas
- **WHEN** se registra un área con aforo `500`
- **THEN** responde `201 Created` con el aforo almacenado sin alteraciones

### Requirement: Coherencia entre aforo y puestos individuales

El sistema SHALL impedir que la cantidad de puestos activos de un área supere el aforo declarado para esa área.

#### Scenario: Alta de un puesto que excede el aforo

- **GIVEN** un área con aforo 10 y 10 puestos activos
- **WHEN** se intenta agregar un puesto adicional
- **THEN** responde `409 Conflict` con un problema RFC 7807 que explica el límite de aforo
- **AND** el puesto no se persiste

#### Scenario: Reducción del aforo por debajo de los puestos activos

- **GIVEN** un área con aforo 12 y 10 puestos activos
- **WHEN** se actualiza el aforo a 8
- **THEN** responde `409 Conflict` y el aforo conserva su valor anterior
- **AND** el mensaje indica cuántos puestos activos impiden la reducción

#### Scenario: Reducción del aforo con puestos suficientes

- **GIVEN** un área con aforo 12 y 10 puestos activos
- **WHEN** se actualiza el aforo a 10
- **THEN** responde `200 OK` con el nuevo aforo almacenado

### Requirement: Inmutabilidad del código QR en la actualización

El sistema SHALL conservar el código QR original de un área o puesto en toda actualización, ignorando cualquier valor de código enviado por el cliente.

#### Scenario: Actualización de los datos del área

- **GIVEN** un área con código QR `X`
- **WHEN** se actualizan su nombre, ubicación, categoría, aforo y tipo de aprobación mediante `PUT /api/espacios/{id}`
- **THEN** responde `200 OK` con los datos actualizados y el código QR `X` sin cambios
- **AND** una consulta posterior del código QR devuelve exactamente `X`

#### Scenario: Intento de sustituir el código QR desde la solicitud

- **GIVEN** un área con código QR `X`
- **WHEN** la solicitud de actualización incluye un campo `codigoQR` con el valor `Y`
- **THEN** el sistema descarta el valor `Y` y conserva `X`
- **AND** la respuesta no confirma ningún cambio de identificador

#### Scenario: Actualización de un puesto

- **GIVEN** un puesto con código QR `P`
- **WHEN** se modifican su identificador visible o su descripción
- **THEN** responde `200 OK` y el código QR del puesto sigue siendo `P`

### Requirement: Inmutabilidad y unicidad del código QR

El sistema SHALL garantizar que cada unidad física tenga un código QR distinto y que ninguna operación de la API permita regenerarlo o reasignarlo.

#### Scenario: Códigos distintos entre unidades

- **GIVEN** dos áreas registradas en la misma ubicación
- **WHEN** se comparan sus respuestas de alta
- **THEN** cada área tiene un código QR diferente
- **AND** el sistema rechaza la persistencia de un código ya asignado a otra unidad

#### Scenario: Ausencia de una operación de regeneración

- **GIVEN** el conjunto de endpoints de escritura de áreas y puestos
- **WHEN** se revisan las operaciones disponibles
- **THEN** ninguna permite regenerar ni reemplazar un código QR existente
- **AND** los únicos cambios admitidos sobre una unidad son sus datos descriptivos y su estado

#### Scenario: Consultas repetidas del mismo código

- **GIVEN** un área con código QR `X` servido en una respuesta anterior
- **WHEN** se repite la consulta tras recargar el cliente o reiniciar la API
- **THEN** el contenido codificado sigue siendo `X` mientras el área exista

### Requirement: Baja lógica de áreas y puestos

El sistema SHALL retirar un área o un puesto del catálogo público mediante baja lógica, conservando el registro y su código QR.

#### Scenario: Baja de un área

- **GIVEN** un área activa con código QR `X`
- **WHEN** se invoca `DELETE /api/espacios/{id}`
- **THEN** responde `204 No Content`
- **AND** el área deja de aparecer en el catálogo público
- **AND** el registro permanece consultable en el listado administrativo con estado inactivo
- **AND** el código QR `X` no se reasigna a otra unidad

#### Scenario: Baja de un puesto

- **GIVEN** un área con 10 puestos activos y aforo 10
- **WHEN** se invoca `DELETE /api/espacios/{id}/puestos/{puestoId}` sobre uno de ellos
- **THEN** responde `204 No Content`
- **AND** el aforo del área deja de contar ese puesto
- **AND** el puesto no aparece en el catálogo público

#### Scenario: Reincorporación de un área dada de baja

- **GIVEN** un área inactiva con código QR `X`
- **WHEN** se actualiza su estado a activa
- **THEN** responde `200 OK` y vuelve a aparecer en el catálogo público
- **AND** conserva el código QR `X`

### Requirement: Generación de plantilla vectorial de impresión

El sistema SHALL generar una plantilla de impresión en formato SVG con una etiqueta vectorial por unidad seleccionada, distribuida en hojas A4 según la capacidad configurada.

#### Scenario: Plantilla de un área con sus puestos

- **GIVEN** un área activa con 12 puestos activos
- **WHEN** se solicita la plantilla del área incluyendo sus puestos
- **THEN** responde `200 OK` con `Content-Type: image/svg+xml` y el documento como archivo adjunto
- **AND** el documento contiene 13 etiquetas: una del área y una por puesto
- **AND** cada etiqueta incluye el código QR, el nombre de la unidad, su ubicación y el código en texto legible

#### Scenario: Contenido vectorial sin imágenes de mapa de bits

- **GIVEN** una plantilla generada
- **WHEN** se inspecciona el contenido del documento
- **THEN** todo el dibujo se compone de elementos vectoriales del propio SVG
- **AND** el documento no incrusta imágenes de mapa de bits ni referencias a archivos externos

#### Scenario: Decodificación del código impreso

- **GIVEN** la etiqueta de un puesto dentro de la plantilla
- **WHEN** se decodifica su código QR con un lector estándar
- **THEN** el contenido decodificado coincide exactamente con el código almacenado para ese puesto

#### Scenario: Selección que excede una hoja

- **GIVEN** una selección de 25 unidades y una capacidad de 18 etiquetas por hoja
- **WHEN** se solicita la primera y la segunda hoja de la plantilla
- **THEN** la primera hoja contiene 18 etiquetas y la segunda contiene 7
- **AND** la respuesta indica cuántas hojas componen la plantilla

#### Scenario: Plantilla sin unidades seleccionadas

- **GIVEN** una solicitud de plantilla sin ninguna unidad indicada
- **WHEN** el sistema valida la solicitud
- **THEN** responde `400 Bad Request` en formato RFC 7807
- **AND** no se genera ningún documento

### Requirement: Descarga del código QR individual

El sistema SHALL servir el código QR de un área o de un puesto existente como documento SVG vectorial apto para impresión.

#### Scenario: Descarga del QR de un área

- **GIVEN** un área registrada con código QR `X`
- **WHEN** se invoca `GET /api/espacios/{id}/qr.svg`
- **THEN** responde `200 OK` con `Content-Type: image/svg+xml`
- **AND** el documento contiene un único código QR cuyo contenido decodificado es `X`

#### Scenario: Descarga del QR de un puesto

- **GIVEN** un puesto registrado dentro de un área
- **WHEN** se invoca `GET /api/puestos/{id}/qr.svg`
- **THEN** responde `200 OK` con el SVG del código QR del puesto
- **AND** el contenido codificado corresponde al puesto y no al área que lo contiene

#### Scenario: Descarga de un código inexistente

- **GIVEN** un identificador que no corresponde a ninguna unidad registrada
- **WHEN** se solicita su código QR
- **THEN** responde `404 Not Found` en formato RFC 7807

#### Scenario: Descarga del QR de una unidad dada de baja

- **GIVEN** un puesto inactivo cuya placa física sigue instalada
- **WHEN** se solicita su código QR
- **THEN** responde `200 OK` con el código original
- **AND** el código no se reasigna ni cambia de valor

### Requirement: Consulta pública del catálogo con filtros

El sistema SHALL exponer un catálogo anónimo y paginado de las áreas activas que admita filtros combinables por texto, categoría y rango de aforo.

#### Scenario: Catálogo por omisión

- **GIVEN** áreas activas y áreas dadas de baja
- **WHEN** se invoca `GET /api/catalogo/espacios` sin filtros
- **THEN** responde `200 OK` con una página de áreas activas y el total de coincidencias
- **AND** ninguna área inactiva aparece en el resultado

#### Scenario: Filtro por texto

- **GIVEN** áreas denominadas "Laboratorio de Redes" y "Auditorio Central"
- **WHEN** se filtra por el texto `redes` en minúsculas
- **THEN** el resultado incluye "Laboratorio de Redes"
- **AND** la comparación ignora mayúsculas, minúsculas y acentos

#### Scenario: Filtros combinados por categoría y aforo

- **GIVEN** áreas de categorías distintas con aforos de 10, 25 y 60
- **WHEN** se filtra por categoría de laboratorio y aforo mínimo 20
- **THEN** el resultado contiene únicamente los laboratorios con aforo mayor o igual a 20
- **AND** el total refleja el conjunto filtrado y no el catálogo completo

#### Scenario: Catálogo sin coincidencias

- **GIVEN** un filtro que no coincide con ninguna área activa
- **WHEN** se consulta el catálogo
- **THEN** responde `200 OK` con la lista vacía, el total en cero y la página solicitada

#### Scenario: Paginación del catálogo

- **GIVEN** 30 áreas activas y un tamaño de página de 10
- **WHEN** se solicita la tercera página
- **THEN** responde `200 OK` con 10 áreas y el total de 30
- **AND** una página posterior a la última responde con la lista vacía sin error

### Requirement: Exposición mínima de datos en el catálogo público

El sistema SHALL omitir del catálogo público el código QR, las rutas internas de almacenamiento y cualquier área inactiva.

#### Scenario: Respuesta pública sin identificadores de control

- **GIVEN** un área activa con código QR `X` y fotografía almacenada
- **WHEN** se consulta el catálogo público
- **THEN** la respuesta incluye nombre, categoría, ubicación, aforo, tipo de aprobación y la URL pública de la fotografía
- **AND** la respuesta no incluye el código QR ni la ruta interna del archivo

#### Scenario: Detalle de un área no publicada

- **GIVEN** un área con estado inactivo
- **WHEN** se consulta `GET /api/catalogo/espacios/{id}`
- **THEN** responde `404 Not Found` en formato RFC 7807
- **AND** no se expone ningún dato del área

### Requirement: Restricción de las mutaciones al rol administrativo

El sistema SHALL exigir el rol `Administrativo` en toda operación de escritura sobre áreas y puestos, manteniendo el catálogo público accesible sin autenticación.

#### Scenario: Escritura con rol no autorizado

- **GIVEN** una sesión autenticada con rol `Docente` o `Estudiante`
- **WHEN** intenta crear, actualizar o dar de baja un área o un puesto
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** la infraestructura permanece sin cambios

#### Scenario: Escritura sin credenciales

- **GIVEN** una solicitud sin encabezado `Authorization`
- **WHEN** intenta crear un área
- **THEN** responde `401 Unauthorized` en formato RFC 7807
- **AND** no se persiste ningún registro

#### Scenario: Consulta anónima del catálogo

- **GIVEN** un visitante sin sesión
- **WHEN** consulta el catálogo público y el detalle de un área activa
- **THEN** ambas respuestas son `200 OK`
- **AND** no se solicita autenticación para leer el catálogo

### Requirement: Parametrización de modalidad y elemento de préstamo del área

El sistema SHALL exigir, al registrar o editar un área, una modalidad de préstamo (`AreaCompleta`, `PuestoIndividual`, `SubZonaCompartida`) y un elemento de préstamo (`EquipoComputo`, `LaboratorioEspecializado`, `ZonaDescansoJuegos`, `Auditorio`) coherentes entre sí.

#### Scenario: Alta de un auditorio como área completa

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** envía a `POST /api/espacios` un área con modalidad `AreaCompleta` y elemento `Auditorio`
- **THEN** responde `201 Created` con ambos valores en la respuesta
- **AND** el área aparece con esa parametrización en el listado administrativo y en el catálogo

#### Scenario: Alta de una sala de cómputo por puesto individual

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** registra un área con modalidad `PuestoIndividual` y elemento `EquipoComputo`
- **THEN** responde `201 Created`
- **AND** el área admite préstamo directo de sus puestos

#### Scenario: Combinación incoherente de modalidad y elemento

- **GIVEN** una sesión con rol `Administrativo`
- **WHEN** intenta registrar un área con modalidad `PuestoIndividual` y elemento `Auditorio`
- **THEN** responde `400 Bad Request` en formato RFC 7807 señalando el campo `tipoElementoPrestamo`
- **AND** no se crea el área

#### Scenario: Alta sin modalidad o sin elemento

- **GIVEN** una solicitud de alta que omite `tipoModalidadPrestamo` o `tipoElementoPrestamo`
- **WHEN** el sistema valida el cuerpo
- **THEN** responde `400 Bad Request` en formato RFC 7807 con el detalle del campo faltante
- **AND** no se crea el área ni se consume un código QR

#### Scenario: Cambio de modalidad de un área sin ocupación futura

- **GIVEN** un área con modalidad `PuestoIndividual` y sin reservas futuras
- **WHEN** el administrativo envía `PUT /api/espacios/{id}` con modalidad `SubZonaCompartida` y elemento `ZonaDescansoJuegos`
- **THEN** responde `200 OK` con la nueva parametrización
- **AND** el código QR del área y de sus puestos no cambia

#### Scenario: Cambio de modalidad con ocupación futura

- **GIVEN** un área con series semestrales o reservas vigentes a futuro
- **WHEN** se intenta cambiar su modalidad de préstamo
- **THEN** responde `409 Conflict` con `type` `modalidad-con-ocupacion`
- **AND** el área conserva su modalidad y su agenda

#### Scenario: Docente intenta parametrizar un área

- **GIVEN** una sesión con rol `Docente`
- **WHEN** envía `POST /api/espacios` o `PUT /api/espacios/{id}`
- **THEN** responde `403 Forbidden` en formato RFC 7807
- **AND** no se modifica la parametrización

### Requirement: Préstamo admitido según la modalidad del área

El sistema SHALL aceptar únicamente el tipo de reserva que corresponde a la modalidad del área: préstamo de unidades para `PuestoIndividual` y `SubZonaCompartida`, y series o solicitudes de área completa para `AreaCompleta`.

#### Scenario: Reserva de puesto en un área por puesto individual

- **GIVEN** un área con modalidad `PuestoIndividual` y un puesto libre
- **WHEN** un usuario envía `POST /api/reservas` sobre ese puesto
- **THEN** responde `201 Created`
- **AND** la reserva queda confirmada

#### Scenario: Serie semestral en un área por puesto individual

- **GIVEN** un área con modalidad `PuestoIndividual`
- **WHEN** un docente radica una serie semestral sobre el área
- **THEN** responde `409 Conflict` con `type` `modalidad-incompatible`
- **AND** no se genera ninguna ocurrencia

#### Scenario: Reserva de puesto en un área completa

- **GIVEN** un área con modalidad `AreaCompleta`
- **WHEN** un usuario intenta reservar uno de sus puestos con `POST /api/reservas`
- **THEN** responde `409 Conflict` con `type` `modalidad-incompatible`
- **AND** no se crea ninguna reserva

#### Scenario: Serie y solicitud sobre un área completa

- **GIVEN** un área con modalidad `AreaCompleta` y elemento `LaboratorioEspecializado`
- **WHEN** un docente radica una serie semestral y un estudiante radica una solicitud con PDF sobre ella
- **THEN** ambas operaciones responden `201 Created`
- **AND** la agenda del área refleja la serie y la solicitud aprobada

#### Scenario: Reserva de sub-zona compartida

- **GIVEN** un área con modalidad `SubZonaCompartida` y elemento `ZonaDescansoJuegos`
- **WHEN** un usuario reserva una de sus unidades con `POST /api/reservas`
- **THEN** responde `201 Created`
- **AND** una serie semestral sobre esa área responde `409 Conflict` con `type` `modalidad-incompatible`
