# GL1F — Cierre de conciliación, licencias y branding

Fecha: 2026-09-04. Rama `codex/gl0a-auth-security`, HEAD `a8c5d16` más cambios locales preservados. **Verificación local GL1F cerrada: 1,588 pruebas unitarias y nueve de integración aprobadas; PRODUCCIÓN NO-GO.** Este documento no afirma despliegue, certificación externa ni preparación integral de las pasarelas.

## Resultado por área

| Área | Comportamiento verificado | Alcance de la evidencia |
| --- | --- | --- |
| Conciliación DTE | Permite consultar Hacienda únicamente cuando existe un intento enviado o incierto con JWS y marca temporal. No reenvía el DTE. Liga tenant, NIT firmado, ambiente, tipo, UUID y número de control; solo acepta como `PROCESADO` una respuesta inequívoca con HTTP exitoso, código `001` y sello. Una consulta negativa conserva el estado fiscal y registra evidencia; los cambios fiscales concurrentes no son sobreescritos. | Pruebas sintéticas de servicio, concurrencia, autorización, respuesta y correo. Sin consulta real a Hacienda. |
| Licencia y cancelación SaaS | La cancelación inmediata revoca la licencia local; la programada conserva únicamente el período finito ya pagado, sin extenderlo. La operación es idempotente, bloquea asociaciones ambiguas y evita cambios incompatibles con cobros pendientes. Los módulos no se borran, pero el guard de licencia impide su uso cuando la licencia deja de ser vigente. | Unitarias, HTTP sintético y SQL LocalDB con proveedor sustituido. Sin pasarela ni correo externos. |
| Ingreso Stripe | El webhook falla cerrado si el secreto de firma está ausente, es placeholder o inválido; no existe excepción por ambiente Development. Payload o firma inválidos reciben respuesta genérica y no exponen secreto ni cuerpo. | Controlador/TestServer con firmas sintéticas. No acredita checkout, cobro ni evento Stripe real. |
| Branding, PDF y correo | Carga y almacenamiento validan estructura, dimensiones, píxeles, animación y MIME real de PNG/JPEG/WebP. Las consultas limitan blobs antes de materializarlos; la generación fiscal usa una proyección sin logo ni firma. Un blob legado inválido se omite en PDF/correo sin alterar JSON, JWS, totales ni entidad rastreada; el correo usa el MIME detectado. | PDFs válidos y de fallback, además de PNG de inspección, revisados visualmente por el agente de diseño. Pruebas automáticas sintéticas, sin SMTP real. |

La conciliación registra la métrica únicamente después de persistir el cambio fiscal. Tanto la auditoría de confirmación como la de resultado negativo son secundarias y best-effort: una caída del subsistema de auditoría no oculta el resultado ya guardado al operador.

## Regresión encontrada por la suite completa

La primera proyección fiscal sin blobs expuso un conflicto de tracking de EF Core: durante la generación se podía asociar al documento una segunda instancia de `Empresa` con la misma clave que otra instancia ya rastreada por el contexto. La suite completa detectó el caso, aunque los filtros iniciales habían sido favorables.

La corrección mantiene la empresa fiscal como proyección temporal sin tracking, la asocia solamente mientras el generador construye el JSON y restaura la navegación original en un bloque `finally`. Así evita que EF intente adjuntar o persistir la proyección saneada. El grupo afectado volvió a aprobar **44/44** casos. Esta cifra es un subconjunto de la suite unitaria final y no se suma al total.

## Evidencia de verificación

- Suite unitaria completa sobre la recompilación limpia: **1,588/1,588 aprobadas**, sin fallos ni omitidas, en `tmp/gl1f/gl1f-final-unit-clean.trx`.
- Integración sobre la recompilación limpia: **9/9 aprobadas**, sin fallos ni omitidas, en `tmp/gl1f/gl1f-final-integration-clean.trx`.
- Total de solución en ejecuciones finales disjuntas: **1,597 pruebas aprobadas**. Los filtros siguientes están contenidos o se superponen con ese total y no se agregan nuevamente.
- Filtro focal GL1F: **44/44 aprobadas**.
- Conciliación DTE y correo: **14/14 aprobadas**.
- Billing/SaaS: **75/75 aprobadas**, en `tmp/gl1f/gl1f-billing.trx`.
- Billing SQL: **24/24 comprobaciones aprobadas**, en `tmp/gl1f/billing-sql.log`. Es un ensayo SQL sintético separado, no un conjunto adicional de pruebas xUnit. La base aleatoria del ensayo fue eliminada y la instancia LocalDB quedó detenida según la evidencia de ejecución.
- Recompilación limpia y aislada `Release` de la solución, incluidos API, Web y Worker: **cero errores y cero advertencias**. Es compilación local, no publicación ni despliegue.
- La inspección visual independiente del agente de diseño y del agente principal confirmó que los PDFs finales abren como documentos válidos, que el fallback sin branding conserva la composición y que los PNG de control se renderizan. La inspección visual no reemplaza una prueba de impresión, cliente de correo o dispositivo real.
- Los procesos API/Web que ya estaban activos se conservaron sin reinicio: las lecturas de salud devolvieron HTTP 200 en `http://127.0.0.1:5058/health` y `http://127.0.0.1:5031/health/live`. Son procesos de una compilación previa y no prueban que GL1F esté desplegado.

Las pruebas emplearon empresas, credenciales, firmas, respuestas, proveedores y transporte sintéticos salvo donde se indica expresamente SQL LocalDB. No se consultó ni modificó la base del cliente.

## Bloqueos y límites abiertos

El estado continúa **PRODUCCIÓN NO-GO** por los siguientes puntos:

1. **P1 — cancelación distribuida:** la llamada de cancelación al proveedor ocurre antes del commit SQL local. Si el proveedor confirma y luego falla el guardado local, la suscripción externa puede quedar cancelada mientras la licencia local continúa activa. Hace falta una saga/outbox y un reconciliador durable; un reintento ciego no resuelve esa ambigüedad.
2. **P1 — Wompi y PayPal incompletos:** el checkout aún no transporta correctamente monto/moneda desde el plan, no existe correlación durable suficiente entre checkout, pago, plan, empresa y suscripción, y los webhooks no completan de forma confiable la activación de licencia. Los POST externos con reintentos carecen de una estrategia de idempotencia durable. Stripe endurecido tampoco equivale a una pasarela completa.
3. **P1 — entrega posterior a conciliación:** el DTE se guarda `PROCESADO` antes de despachar el webhook de Connect. Esa entrega sigue siendo best-effort y no tiene outbox ni redelivery exactamente una vez desde esta ruta; un fallo posterior al commit no debe provocar una nueva consulta o transmisión fiscal, pero sí requiere recuperación de la notificación.
4. **P1 — contrato Hacienda no acreditado en vivo:** el protocolo de consulta se contrastó con el [Documento de Lineamiento de Integración de Factura Electrónica, versión 8.0, publicado por Hacienda el 21 de septiembre de 2021](https://factura.gob.sv/wp-content/uploads/2021/11/FESVDGIIMH_GuiaIntegracionFacturaElectronicasSV.pdf). Esa guía oficial es histórica y no prueba que el endpoint, credenciales o respuesta actuales de PRUEBAS coincidan; falta una consulta controlada y autorizada contra Hacienda antes de promover este flujo.
5. **P2 — costo de branding:** la validación defensiva decodifica y realiza un render sintético por uso. Una misma imagen, de hasta cuatro millones de píxeles, puede validarse varias veces en una solicitud; no existe caché por hash ni límite global de concurrencia para ese trabajo nativo.

No se consideran cerrados mediante GL1F los requisitos más amplios de precios y conciliación de pasarelas, pausa/reanudación de suscripciones, configuración de tipos DTE por plan ni la garantía transaccional completa de webhooks.

## Decisión y siguiente paso

GL1F mejora de forma verificable la recuperación de estados fiscales inciertos, la cancelación local de licencias y la tolerancia a branding inválido. Sin embargo, **no autoriza activar producción**.

El siguiente bloque debe implementar primero la coordinación durable proveedor↔SQL y la correlación de pagos; después, el outbox/reentrega de notificaciones de conciliación. Con esos controles cerrados, corresponde ejecutar una prueba de consulta en ambiente PRUEBAS con un documento autorizado y evidencia sanitizada, seguida por smoke tests de API/Web/Worker y un plan explícito de rollback.

En esta ronda no hubo despliegue, cambios a la base del cliente, transmisión o consulta real a Hacienda, cobros con proveedores reales, envío SMTP real, commit/push, ni detención o reinicio de API/Web.
