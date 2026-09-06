# GL-1B — idempotencia de creación/emisión DTE

2026-09-04 · `codex/gl0a-auth-security` · Código local, **sin desplegar**. No se modificó la APK ni la base del cliente.

## Garantía implementada

La combinación empresa + ámbito + hash de clave identifica una solicitud, no una coincidencia aproximada de monto/cliente. La reserva se guarda en el propio DTE bajo la transacción y lock de creación existentes, con índice SQL único. No existe una ventana entre crear el documento y persistir su clave.

El cuerpo se compara mediante una huella canónica: orden estable de propiedades, números decimales equivalentes y orden de líneas preservado. La clave no forma parte de la huella. No se almacena la clave original ni una segunda copia del cuerpo.

Una solicitud repetida:

- devuelve el ID, número, código de generación y estado del DTE existente;
- no consume otro cupo/correlativo ni repite generación, firma o transmisión;
- rechaza datos distintos con HTTP 409/`IDEMPOTENCY_CONFLICT`;
- no se convierte automáticamente de pruebas a producción;
- mantiene autenticación, permisos/scopes y aislamiento por empresa.

POS usa un ámbito interno separado y el ID de venta; ese origen no se acepta desde JSON público. El vínculo venta-DTE se confirma en la misma transacción que crea el borrador. Una caída posterior conserva el vínculo; los reintentos de promoción lo consultan. No se marca FACTURADA mientras el DTE no esté PROCESADO. Concurrencia optimista protege el vínculo y el estado frente a anulación simultánea.

## Contrato para Manuel e integradores

1. Generar una clave por venta (por ejemplo UUID) y guardarla localmente **antes del primer POST**.
2. Enviar `Idempotency-Key: <clave>`, o `idempotencyKey` en el JSON. De 1 a 128 caracteres ASCII visibles sin espacios. No usar datos personales como clave.
3. Ante timeout, pérdida de red o reinicio, reenviar la misma clave y cuerpo. No generar una clave nueva para la misma venta.
4. Revisar `data.idempotencyReplayed`, `data.id`, `data.estadoCodigo` y el sello. Éxito HTTP no equivale a PROCESADO.
5. Errores posteriores a la creación pueden incluir el DTE en `data` aun con `success=false`. Guardar ese ID y consultar el recurso.
6. Si hace primero un POST de borrador y después uno de emisión con la misma clave, se devuelve el borrador: no se inicia otro pipeline. Para emisión en un paso, usar el endpoint emitir desde el inicio.

No se reanuda automáticamente una emisión incierta. La recuperación es sobre el documento existente con los permisos y transiciones vigentes; antes de volver a enviar ante resultado incierto debe reconciliarse el estado fiscal. Nunca asumir que un timeout implica que Hacienda no lo recibió.

## Compatibilidad y alcance

- La clave sigue siendo opcional para no romper clientes actuales. Sin ella, POST independientes pueden crear documentos diferentes. Una clave interna por invocación solo protege los reintentos de la estrategia SQL dentro de esa invocación.
- La protección POS cubre promoción de un ticket a DTE, no creación del ticket, cobros/pasarelas, movimientos de inventario ni idempotencia general de eventos.
- No se deduplican ni vinculan por suposición documentos históricos sin clave. Revisar previamente ventas con emisiones antiguas fallidas o sin vínculo.
- No promete entrega remota exactamente una vez ni sustituye reconciliación Hacienda/outbox. Un replay no inicia transmisión; los mecanismos existentes de recuperación tienen su propio ciclo.
- No hay expiración automática de claves. Borrar documentos o quitar estas columnas eliminaría la protección histórica.

## Migración y despliegue

`20260904140909_GL1B_DteIdempotency` agrega tres columnas nullable e índice único filtrado a Dte_Documentos. Los documentos históricos permanecen intactos. El modelo también marca campos POS existentes como tokens de concurrencia; no requiere nuevas columnas POS.

La migración solo se probó en una base aleatoria sintética. La rama contiene también GL0A/GL0B/GL0C de autenticación: no iniciar API/Web/Worker sobre la base activa para aplicarlas implícitamente.

Antes de desplegar: respaldo/restauración verificados, adopción de la clave por consumidores, revisión de emisiones antiguas inciertas, migración autorizada y actualización coordinada de todos los emisores (API/Web/Worker). No mezclar nodos antiguos que ignoren claves. Ante rollback, bloquear emisiones antes de volver a código no compatible; conservar el historial de claves, no ejecutar Down por rutina.

## Verificación

- Suite completa Release: 1,276 unitarias + 9 de integración aprobadas (1,285 total). Las 9 de integración usan InMemory; las comprobaciones SQL son independientes.
- 28 comprobaciones SQL aprobadas, incluyendo los controles GL-1A y la cadena completa de migraciones en la base sintética.
- Compilación final de solución: 0 errores/0 advertencias. Modelo EF sin cambios pendientes respecto de la migración. En la compilación previa de tests aparece la advertencia preexistente CS8604 de Inventario/LotesInventarioTests.cs:137.
- Pruebas de clave, normalización, JSON privado POS, replay en todos los estados, recuperación de ID en errores, HTTP 409, atajos API y scopes NeoConnect.
- SQL LocalDB 2025 aislado: migración completa desde cero; 12 solicitudes de la misma clave producen un DTE; relectura con nuevo contexto; conflicto de monto; aislamiento por empresa; cambio de ambiente; respuesta de transporte perdida simulada; vínculo POS ante fallo parcial; 12 promociones POS concurrentes; índice único y conflicto de anulación.
- El arnés usa creación/reserva y POS reales; las etapas fiscales/transporte de la prueba de timeout se simulan. No transmite a Hacienda.
- Las bases sintéticas se eliminan en finally y se verifica que la instancia de auditoría quede vacía. No se conectó a la base del cliente.

Siguiente bloque: errores Hacienda estructurados y recuperación guiada sobre el DTE existente. Antes de producción también falta evidencia en SQL Server 2022, clientes que reutilicen la clave y pruebas E2E autorizadas.
