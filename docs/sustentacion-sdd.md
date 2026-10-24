# Sustentación del uso de SDD (resumen)

## 1. Cómo se aplicó el proceso

Los cinco cambios siguen el mismo ciclo:

1. **`proposal.md`**: por qué, qué cambia, capacidades nuevas o modificadas, impacto y riesgo principal.
2. **Deltas de especificación**: bloques `ADDED` o `MODIFIED` con escenarios Given/When/Then.
3. **`design.md`**: decisiones numeradas, cada una con su alternativa descartada, más riesgos y plan de migración.
4. **`tasks.md`**: cada tarea trae su comprobación. Se da por hecha cuando pasa la verificación, no cuando se escribe el código.
5. **Cierre**: `openspec validate --strict`, revisión, commit atómico y archivo. Las **152 tareas** están marcadas `[x]`.

| # | Cambio | Capacidades nuevas / modificadas | Rol |
| :-- | :--- | :--: | :--- |
| 01 | `core-infrastructure` | 4 / 0 | Capas, identidad, errores |
| 02 | `area-catalog-and-qr` | 3 / 1 | Inventario físico y cuentas |
| 03 | `direct-booking-core` | 1 / 0 | Agenda puntual de puestos |
| 04 | `semester-bookings-and-approvals` | 4 / 1 | Semestre, aprobaciones, calendario |
| 05 | `checkin-noshow-and-monitoring` | 1 / 0 | Presencia real y ocupación |

**Por qué los deltas suman 76 requisitos y la línea base tiene 75:** el encabezado `MODIFIED` reemplaza un requisito en vez de añadir otro. La especificación consolidada guarda la intención vigente, y el historial queda en `archive/`.

---

## 2. Resumen por cambio

### 01 · Infraestructura base

- **Resultados:** solución .NET 10 en cuatro capas con prueba de arquitectura que falla ante una violación. Autenticación híbrida (JWT local y Entra ID), refresh con rotación y errores RFC 7807.
- **Dificultades:** `Usuario` hereda de `IdentityUser` y no puede vivir en el dominio. Dependencia de Entra ID (se mitiga con login local y `503` explícito). Tokens en `localStorage`.
- **Decisiones:** no exponer `DbSet` en el puerto, no crear un proyecto `Identity` aparte, no usar `Cookie + OpenIdConnect`.
- **Conclusión:** una regla de arquitectura escrita como escenario verificable se vuelve una puerta automática del pipeline.

### 02 · Catálogo de áreas y QR

- **Resultados:** `Espacio` con `Puesto` interno, QR opaco e inmutable, SVG vectorial, catálogo público, autorregistro acotado y consola de roles. Verificación documentada: **117 pruebas de backend y 52 de la SPA**.
- **Dificultades:** cuentas no deseadas por el autorregistro, apropiación de identidad institucional, carrera del primer administrador (se cierra con transacción `Serializable`).
- **Decisiones modificadas:** el rol efectivo pasa a ser el asignado en la plataforma y se elimina el aprovisionamiento automático. Un `oid` no vinculado recibe `403`.
- **Conclusión:** un cambio profundo de autorización viajó como un delta pequeño, sin reescritura silenciosa.

### 03 · Reserva directa

- **Resultados:** `Reserva` como agregado, motor de disponibilidad puro, cupo de 2 reservas activas y cancelación hasta 30 minutos antes. **Cinco barreras** contra la doble reserva.
- **Dificultades:** `RowVersion` detecta actualizaciones perdidas, no inserciones. Por eso el alta se protege con bloqueo. Quedan reservas futuras huérfanas si se desactiva un puesto.
- **Decisiones:** disponibilidad autenticada, cupo global, plazo único y QR inmutable. Se descartó calcular disponibilidad con `EXISTS` en SQL.
- **Conclusión:** cerrar las preguntas abiertas en el diseño evitó reabrir contratos durante las tareas.

### 04 · Semestres y aprobaciones

- **Resultados:** recurrencia semestral, `ReservaEspacio` como agenda única del área, solicitudes con máquina de estados, pre-informe PDF validado, correo en cola transaccional y calendario institucional.
- **Dificultades:** colisiones entre series (transacción serializable más índice único), migración con heurística para datos existentes, correo *at-least-once*, PDF sin análisis de malware.
- **Decisiones modificadas:** modalidad y elemento de préstamo pasan a ser parámetros explícitos por área. La notificación pasa de "en línea" a encolada.
- **Conclusión:** es la etapa con más decisiones acopladas. También muestra el costo del método: un diseño de más de 44 000 caracteres.

### 05 · Check-in, No-Show y monitoreo

- **Resultados:** estados `Confirmada → EnUso → Finalizada` y `NoShow`, check-out anticipado que acorta `Fin`, barrido cada 5 minutos, mapa de ocupación con sondeo cada 15 s y escáner con `BarcodeDetector`.
- **Dificultades:** un barrido caído retiene franjas ajenas, sesiones sin cerrar, `BarcodeDetector` no universal y fraude con foto del QR estático (riesgo R3, aceptado).
- **Decisiones modificadas:** `CanceladaPorNoShow` pasa a `NoShow` conservando el valor `3`. Un índice único `IN (1, 4)` reemplaza al anterior. La tolerancia tiene una única fuente: `Reservas:VentanaToleranciaCheckInMinutos`.
- **Conclusión:** declarar una invariante como fuente única impide que el endpoint y el worker diverjan sin que el documento lo delate.

---

## 3. Resumen por especificación

| Especificación | Req. / Esc. | Idea central |
| :--- | :--: | :--- |
| `clean-architecture-baseline` | 5 / 10 | La arquitectura se vuelve verificable. Los puertos tipados llegaron en 02. |
| `problem-details` | 6 / 13 | El contrato de error no cambió en cinco etapas. Solo se añadieron `type` estables. |
| `local-authentication` | 7 / 16 | Rotación de refresh con detección de reutilización. Los *Non-Goals* frenaron el crecimiento. |
| `entra-id-federation` | 5 / 14 | Único `MODIFIED` real: de intersección de roles a rol de la plataforma. |
| `gestion-espacios` | 13 / 49 | La inmutabilidad del QR se reutiliza como contrato en 03 y 05. |
| `user-accounts` | 6 / 18 | Autorregistro más vinculación explícita. El rol nunca viene del cliente. |
| `role-administration` | 5 / 14 | Protecciones de último administrador y autoedición. |
| `prestamo-directo` | 4 / 19 | Los escenarios de concurrencia forzaron el diseño de las barreras. |
| `reservas-semestrales` | 5 / 17 | Serie "todo o nada". El calendario se evalúa antes que la agenda. |
| `solicitudes-laboratorio` | 5 / 16 | Los límites declarados como *Non-Goals* hacen defendible el alcance. |
| `bandeja-aprobaciones` | 3 / 16 | La regla de capas condicionó el diseño: puerto `IColaCorreos` más `BackgroundService`. |
| `calendario-institucional` | 5 / 25 | Lo "solo configuración" también necesita especificación estricta. |
| `asistencia` | 6 / 32 | Mayor densidad temporal. La matriz de estados justifica cada `409`. |

---

## 4. Decisiones modificadas más importantes

Las de mayor impacto cambian semántica, no solo implementación:

1. **Rol federado** (01 → 02): de intersección perfil ∩ app roles a rol de la plataforma.
2. **Primer acceso federado** (01 → 02): de aprovisionamiento automático a `403` más vinculación explícita.
3. **Estado de no-show** (05): `CanceladaPorNoShow = 3` pasa a `NoShow = 3`, y se añaden `EnUso = 4` y `Finalizada = 5`.
4. **Reserva vigente** (05): de `Confirmada` a `Confirmada | EnUso`.
5. **Tolerancia de check-in** (05): una sola fuente configurable, entre 1 y 720 minutos.

Otras decisiones relevantes: aforo como parámetro y no constante, QR opaco, `Puesto` como entidad interna, disponibilidad autenticada, notificación por cola transaccional y PDF servido por endpoint autenticado.

---

## 5. Dificultades transversales

### Hallazgo crítico: deuda de trazabilidad

En 04 y 05 se añadieron restricciones que afectan a `reservas/prestamo-directo` sin modificar su especificación. El término "reserva vigente" no enumera estados, así que pudo redefinirse en el diseño.

- **Ventaja:** no hay texto duplicado.
- **Riesgo:** quien lea solo la línea base no encuentra la lista de estados ni la restricción de calendario.
- **Recomendación:** añadir un glosario de estados o marcar la capacidad afectada como `MODIFIED` con un delta mínimo.

### Riesgos aceptados

| Riesgo | Mitigación declarada |
| :--- | :--- |
| Fraude con foto del QR (R3) | Solo funciona dentro de la ventana y con reserva propia vigente |
| Correo duplicado | Mensaje informativo e idempotente |
| PDF sin análisis de malware | Descarga autenticada, `attachment` y `no-store` |
| Tokens en `localStorage` | Expiración absoluta, rotación y política de contenido |
| Rol obsoleto hasta el refresh | `requiereRenovacion` en la respuesta |
| Reservas futuras huérfanas | Se validan al consultar. La cancelación masiva queda como deuda |
| Caché de calendario desactualizada | TTL corto, invalidación y bloqueo con ocupación futura |

---

## 6. Deudas declaradas (no son olvidos)

- MFA, recuperación de contraseña y revocación masiva (01).
- Eliminación de cuentas e invitaciones por correo (02).
- Excepciones de calendario por área (03 y 04).
- Reprogramar, extender o transferir reservas (03).
- Cancelación masiva al desactivar puestos o áreas (03).
- Antivirus, versionado y firma digital de PDF (04).
- Sanciones por inasistencia (05).
- Check-in de series semestrales (05).
- Tiempo real con SignalR o WebSocket (03, 04 y 05).

---

## 7. Conclusiones sobre SDD

1. **La especificación fue un contrato**, no documentación. Sus escenarios traen códigos HTTP y `type` de error concretos.
2. **Los deltas evitaron acumular verdad.** Solo dos capacidades preexistentes se tocaron: una reescritura y una ampliación.
3. **Los *Non-Goals* controlaron el alcance** y convierten preguntas incómodas en decisiones documentadas.
4. **Las alternativas descartadas son la defensa técnica ya escrita.**
5. **La verificación se planificó con la tarea**, y "terminado" es observable.
6. **La trazabilidad respondió "por qué" sin leer código**: propuesta → delta → diseño → tarea.
7. **Los invariantes compartidos dieron coherencia**: el QR inmutable y el contrato RFC 7807 sobrevivieron a las cinco etapas.
8. **El costo es real:** diseños de 39 000 a 44 000 caracteres y riesgo de deriva entre capacidades.
9. **Encaja con desarrollo asistido por IA**: proposal, spec, design y tasks acotados son contexto suficiente sin inventar requisitos.
10. **Recomendaciones:**
    - Glosario de estados y notas de dependencia entre capacidades.
    - Declarar `MODIFIED` cuando cambie la semántica de un término compartido.
    - Un reporte de verificación por etapa. Hoy solo existe `docs/etapa-2-verificacion.md`.
    - Mantener la validación del arranque con configuración inválida.
