# GL-1A — integridad fiscal API

2026-09-04 · Rama de trabajo: `codex/gl0a-auth-security`. Implementado y probado localmente; **sin desplegar**. Manuel mantiene la APK; este bloque no modifica Android.

## Correcciones

- Creación exige un ambiente fiscal válido explícito; ya no presupone pruebas cuando falta configuración.
- Generar, validar, firmar y enviar rechazan una configuración incompatible con el ambiente persistido. Generar/firmar también rechazan cambios de establecimiento/punto de venta que contradigan el número de control.
- Antes de firmar se coteja ambiente e identidad del JSON; antes de enviar, los del JWS. Los clientes HTTP de auth, recepción, contingencia, eventos y lotes no asignan un ambiente por defecto a valores desconocidos.
- Retorno e invalidación comprueban ambiente del DTE origen. Contingencia exige todos los IDs solicitados de la empresa y del mismo ambiente. Lotes exigen firmas de todos los documentos; su consulta respeta el ambiente persistido.
- Revalidar no vuelve a abrir documentos enviados/procesados/invalidados. Un invalidado tampoco se regenera, firma ni envía.
- Caché MH cifrada con contexto de empresa, ambiente, usuario y contraseña cifrada. Los tokens antiguos sin contexto se renuevan una vez. Escritura SQL condicional impide que una autenticación tardía guarde un token sobre credenciales cambiadas. Las operaciones en curso usan su contexto capturado; no se trasladan automáticamente a otro endpoint.
- Reserva de número transaccional aun sin el guard comercial opcional. El contador no puede quedar por debajo del mayor correlativo presente en documentos históricos. Se mantiene la secuencia global existente por empresa/tipo: no se introducen series independientes ni reinicios anuales o de producción. El límite actual de almacenamiento `int` se detecta sin volver a cero.
- Filtro opcional `ambienteCodigo` en listado API; validación de valores y aislamiento por empresa.
- Ventas/costos DTE de NeoProfit y ventas de libros fiscales/F-07 solo consideran `PRODUCCION`.

## Evidencia

- Suite completa Release: 1,251 unitarias + 9 de integración aprobadas. Las 9 de integración usan EF InMemory; no sustituyen SQL.
- `tools/DteSqlVerification`: 13 comprobaciones aprobadas sobre SQL LocalDB 2025 aislado: 12 reservas concurrentes, rollback, 6 creaciones concurrentes con el servicio real y execution strategy, preservación al cambiar ambiente, bloqueo SQL por ambiente, compare-and-set del token, relectura de caché y libro fiscal sin pruebas.
- No se arrancaron API/Web/Worker ni se llamó a Hacienda. Se creó y eliminó exclusivamente una base aleatoria sintética `NeoStpDteAudit_<GUID>`. Se comprobó que la instancia de auditoría quedó sin bases de usuario.
- No hay migración nueva en este bloque. Las migraciones GL0A/GL0B/GL0C de autenticación siguen pendientes de despliegue en la rama.
- Advertencia preexistente al compilar tests: CS8604 en `Inventario/LotesInventarioTests.cs:137`.

## Límites y siguiente bloque

Esto no certifica la salida a producción ni sustituye pruebas contra el ambiente de pruebas autorizado de Hacienda. No se reabre el incidente DTE11 cerrado.

Siguiente: idempotencia durable de emisión API/NeoConnect/POS (repeticiones, concurrencia y timeout sin generar otro DTE), seguida de errores Hacienda estructurados para Web/API.

Antes de producción siguen pendientes validación con SQL Server 2022/restauración autorizada, revisión completa de efectos ERP (inventario, cobranza, compras y gastos no modelan todos el ambiente fiscal), reglas de referencias electrónicas externas, carga/recuperación, credenciales y autorización MH, despliegue controlado y evidencia E2E. No borrar documentos, reseed de IDs ni reiniciar correlativos.
