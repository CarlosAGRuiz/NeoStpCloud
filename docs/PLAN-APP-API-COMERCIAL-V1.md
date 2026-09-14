# Plan App/API Comercial v1

## Objetivo

Convertir la API existente de NeoSTP Cloud en un contrato estable para la app móvil y completar las funciones comerciales que dependen del contexto real de cada empresa: seguridad/MFA opcional, tipos DTE autorizados por plan y entrega automática y trazable del correo de una factura procesada.

Este plan amplía la app y la API. No reabre el hardening, la certificación fiscal ni el despliegue productivo ya completados.

## Decisiones aprobadas

1. MFA es una función de seguridad opcional para todos los usuarios. Si el usuario la activa, debe completar el segundo factor al iniciar sesión.
2. La API es la autoridad para permisos, módulos y tipos DTE. Ocultar opciones en la app no sustituye la validación del servidor.
3. Los tipos DTE visibles y emitibles dependen de la empresa activa y de su plan vigente. El requisito comercial es que NeoSTP pueda contratar 13 tipos y otra empresa solamente 4, pero el backend actual reconoce 11 DTE. APP-2 debe completar explícitamente los tipos faltantes antes de autorizar 13; los cuatro eventos fiscales certificados no se contarán como tipos DTE.
4. Al procesarse un DTE se producen dos entregas de correo independientes:
   - receptor: conserva exactamente la plantilla y adjuntos actuales;
   - emisor: recibe un correo nuevo de copia, dirigido únicamente a su correo configurado.
5. No se utilizará CC ni BCC. El receptor no conocerá la dirección usada para la copia del emisor y cada entrega tendrá estado, intentos y error propios.
6. La aceptación fiscal nunca se revierte por un fallo de correo.

## Alcance por incremento

### APP-1 — Seguridad, sesiones y MFA

#### App

- Pantalla `Seguridad` con estado MFA: desactivado, pendiente de confirmación o activo.
- Inicio de enrolamiento mostrando QR y clave manual sin guardar el secreto en logs, analítica, capturas de error ni almacenamiento general.
- Confirmación con TOTP antes de activar MFA.
- Códigos de recuperación visibles una sola vez, con acciones copiar y guardar de forma consciente.
- Desactivación mediante reautenticación y TOTP o código de recuperación válido.
- Soporte del desafío MFA durante login sin conceder acceso parcial a módulos.
- Tokens en almacenamiento seguro del sistema operativo y cierre local al revocar la sesión.
- Bloqueo de capturas de pantalla en QR, clave manual y códigos de recuperación cuando la plataforma móvil lo permita.

#### API

- Estabilizar y documentar el estado y ciclo MFA usando los endpoints de autenticación existentes.
- Respuestas con códigos estables para enrolamiento requerido, código inválido, recuperación usada, desafío vencido, sesión revocada y rate limit.
- Revalidar usuario, empresa, membresía y `SecurityStamp` al refrescar sesión.
- Exponer sesiones/dispositivos activos y permitir revocar una sesión propia como mejora posterior del mismo bloque.
- Mantener rate limit, auditoría y respuestas `Cache-Control: no-store` en operaciones sensibles.

#### Criterios de aceptación

- Un usuario puede activar, usar y desactivar MFA desde la app.
- Un usuario que no lo active continúa utilizando NeoSTP normalmente.
- Un desafío MFA no permite consultar datos de ninguna empresa antes de verificarse.
- Los secretos y códigos no aparecen en logs ni respuestas posteriores.

### APP-2 — Tipos DTE por plan y empresa

#### Regla efectiva

El conjunto disponible se calcula en servidor:

`tipos certificados por NeoSTP ∩ tipos autorizados por el plan vigente ∩ módulos/permisos del usuario`

La empresa activa siempre determina el resultado. Cambiar de empresa recalcula el catálogo sin emitir un nuevo login. SuperAdmin en modo soporte también queda limitado por la empresa seleccionada para las operaciones fiscales ordinarias.

#### API

- Publicar `GET /api/dte/tipos-disponibles` para JWT y un equivalente versionado cuando aplique a NeoConnect.
- Devolver código MH, nombre, versión/esquema aplicable, capacidades y motivo no sensible cuando un tipo no esté disponible.
- Incluir los tipos disponibles, módulos, permisos y banderas móviles en un contrato de arranque (`bootstrap`) para reducir llamadas y evitar menús inconsistentes.
- Validar la autorización efectiva en todos los endpoints de creación, emisión, POS a DTE, CRM a DTE y NeoConnect; nunca confiar en el tipo enviado por la app.
- Usar la configuración existente `TiposDteAutorizadosCsv` como transición y normalizarla detrás de un servicio de dominio/aplicación. El inventario inicial debe reconciliar los 11 DTE que hoy reconoce `DteTypeAuthorization.Supported` con los 13 requeridos por NeoSTP; cualquier tipo nuevo exige esquema, generación, firma, transmisión, PDF, correo, permisos y pruebas antes de agregarse al conjunto soportado. Evaluar una tabla relacional solamente si la administración y auditoría futuras justifican la migración.
- Conservar documentos históricos cuando el plan cambie. Un downgrade impide nuevas emisiones del tipo retirado, pero no oculta ni modifica DTE anteriores.
- Auditar cambios de tipos autorizados indicando empresa, actor, plan, valores anterior/nuevo y fecha.

#### App

- Mostrar únicamente los tipos devueltos por la API para la empresa activa.
- Invalidar caché al cambiar empresa, plan, sesión o membresía.
- Mostrar un mensaje comercial claro si una acción deja de estar incluida, sin revelar configuración interna de otra empresa.
- No codificar listas fijas de 4, 13 o 15 tipos dentro de la app.

#### Criterios de aceptación

- La empresa configurada con 4 tipos ve y puede emitir solo esos 4.
- La fase inicial demuestra aislamiento con subconjuntos válidos de los 11 DTE actuales. La aceptación final de NeoSTP con 13 exige implementar y validar previamente los dos tipos faltantes; configurar códigos desconocidos nunca habilita ni oculta silenciosamente todo el catálogo.
- Manipular manualmente el request para enviar un tipo no incluido devuelve `403` con código estable y no reserva correlativo ni crea DTE.
- Cambiar de empresa actualiza el catálogo y no filtra nombres, planes ni documentos entre tenants.

### APP-3 — Correo automático del DTE con dos entregas

#### Flujo

En la misma transacción de base de datos que confirma `PROCESADO` con sello, incluido el caso de conciliación:

1. Confirmar la transición fiscal y crear durablemente un grupo de notificación ligado a `EmpresaId` y `DteDocumentoId` como una sola unidad atómica.
2. Crear en esa misma transacción la entrega `RECEPTOR` si el DTE tiene correo receptor válido.
3. Crear en esa misma transacción la entrega `EMISOR` si la empresa tiene correo de copia configurado.
4. El Worker reclama cada entrega con lease/idempotencia, genera PDF y JSON desde el DTE persistido y realiza el envío correspondiente.
5. Guardar estado, intentos, proveedor, fecha de aceptación, error sanitizado y próxima ejecución.
6. Reintentar fallos transitorios con backoff; llevar fallos definitivos a revisión manual.

Como defensa adicional, un reconciliador idempotente detectará DTE procesados sin grupo completo y creará únicamente las entregas faltantes. Esto cubre datos históricos y cualquier transición producida por un camino heredado, sin duplicar correos ya reclamados o enviados.

#### Contenido

- `RECEPTOR`: misma plantilla, asunto, PDF y JSON que recibe actualmente. No menciona ni expone la entrega al emisor.
- `EMISOR`: correo separado con asunto identificable como copia del DTE, resumen mínimo y los mismos adjuntos fiscales. Su destinatario es exclusivamente el correo configurado del emisor.
- Si receptor y emisor tienen la misma dirección, se mantienen las dos entregas porque representan finalidades y estados distintos.

#### API y Web

- Exponer estado resumido en el detalle del DTE: receptor y emisor por separado.
- Estados iniciales: `NO_APLICA`, `PENDIENTE`, `ENVIANDO`, `ENVIADO`, `REINTENTANDO`, `FALLIDO`.
- Permitir reenvío manual por destino (`RECEPTOR`, `EMISOR` o ambos) con permiso `DTE.Emitir`, antiforgery en Web, idempotencia y auditoría.
- Generar alerta interna cuando una entrega quede `FALLIDO` y resolverla al enviarse correctamente.
- No mostrar direcciones completas salvo a usuarios autorizados de la misma empresa; en listados usar correo enmascarado.

#### Criterios de aceptación

- Procesar un DTE crea como máximo una entrega automática por finalidad.
- Reconsultar o reintentar un DTE ya procesado no genera duplicados.
- El receptor recibe exactamente el correo actual y no conoce la copia del emisor.
- El emisor recibe un mensaje nuevo e independiente.
- Un fallo SMTP conserva el DTE como `PROCESADO` y queda visible/reintentable.
- La pantalla y la API distinguen el resultado de receptor y emisor.
- Si el proveedor ofrece webhooks, sus eventos de entrega/rebote se correlacionan por destinatario; sin webhook, `ENVIADO` significa aceptado por el proveedor SMTP, no leído ni entregado al buzón.

### APP-4 — Experiencia móvil comercial

Prioridad alta:

- Selector de empresa con actualización inmediata de permisos, módulos y tipos DTE.
- Inicio configurable por rol: ventas, caja, inventario o administración.
- Creación de borrador y emisión con `Idempotency-Key` persistida para tolerar pérdida de red.
- Línea de tiempo DTE con sello, diagnóstico y siguiente acción segura.
- Filtros por período, estado, cliente y tipo; descarga/compartir PDF y JSON.
- Centro de notificaciones para DTE procesado, rechazado y fallo de correo.
- Clientes y productos con búsqueda rápida, favoritos y lector de código de barras.

Prioridad media:

- Borradores locales cifrados y sincronización explícita; nunca guardar JWS, certificado o contraseña de Hacienda en el dispositivo.
- Venta POS y corte de caja adaptados a pantallas móviles.
- Alertas de inventario por sucursal.
- Reautenticación biométrica local para abrir la app, sin reemplazar contraseña/MFA del servidor.
- Modo de soporte con diagnóstico exportable y sanitizado.

Fuera de esta versión:

- Certificados de firma almacenados en el teléfono.
- Emisión fiscal completamente offline.
- Pasarelas, WhatsApp, OCR o push productivo sin proveedor aprobado y configurado.
- Listas de módulos, permisos o tipos DTE codificadas en la app.

## Mejoras transversales de API

- Contrato móvil de arranque con usuario, empresa activa, empresas disponibles, permisos, módulos, tipos DTE, MFA y banderas de función, sin secretos.
- Versionado y política de compatibilidad para DTO usados por la app.
- Errores con `ApiResponse.code`, mensaje utilizable y datos mínimos de recuperación.
- Paginación y filtros consistentes en documentos, clientes, productos, alertas e inventario.
- `ETag`/cache condicional para catálogos de baja variación.
- Correlation ID desde app hasta API, Worker, correo y auditoría.
- OpenAPI actualizado y ejemplos de login con MFA, cambio de empresa, tipos DTE y correo.
- Métricas sin datos sensibles: latencia, errores, rate limits, reintentos de correo y colas pendientes.

## Seguridad, tenancy y licenciamiento

- Todo query y comando operativo conserva `EmpresaId`.
- Los permisos de UI son orientativos; la API vuelve a autorizar cada operación.
- Los cambios de plan no se aceptan desde endpoints móviles ordinarios.
- No exponer SMTP, contraseñas MH, certificados, TOTP, recovery codes ni claves API en `bootstrap`, logs o diagnósticos.
- Auditar enrolamiento/desactivación MFA, revocación de sesiones, cambios de tipos autorizados y reenvíos de correo sin registrar secretos ni cuerpos completos.

## Pruebas requeridas

- Unitarias: cálculo de tipos efectivos, transición de entregas, idempotencia, backoff y sanitización.
- Integración SQL: concurrencia de outbox, leases, unicidad por finalidad y aislamiento de empresas.
- API: MFA completo, cambio de empresa, plan 4 vs. 13 tipos, requests manipulados y estados de correo.
- Web: estados separados y reenvío con autorización/antiforgery.
- App: login con/sin MFA, cambio de empresa, caché invalidada, pérdida de red y reintento idempotente.
- Regresión: build sin warnings, suite completa y prueba SMTP controlada sin destinatarios reales.

## Secuencia recomendada

1. APP-3 correo durable con receptor/emisor separado, porque afecta la primera factura productiva.
2. APP-2 tipos DTE efectivos por plan, comenzando por el servicio compartido y validación server-side.
3. APP-1 MFA y sesiones móviles usando los contratos existentes.
4. APP-4 experiencia móvil y consolidación del contrato `bootstrap`.
5. Actualizar OpenAPI, README, evidencia y notas de release en cada incremento.

## Repositorios y entregables

- Este repositorio contiene Domain, Application, Infrastructure, API, Web, Worker, migraciones, OpenAPI y pruebas.
- La implementación Android vive en `manuelberganza-dev/neocloud_mobile_android` y debe consumir los contratos publicados aquí sin copiar reglas fiscales o de licenciamiento.
- Cada incremento se integra mediante PR pequeño, migración revisada cuando corresponda y evidencia de pruebas. No se deben mezclar artefactos operativos locales ni secretos en los commits.
