# NeoConnect API v1

API pública de NeoSTP Cloud para integraciones externas (NeoBusiness, NeoScan, ERPs de terceros).
Permite **emitir y consultar DTE** y **gestionar clientes/productos** mediante **API Key**.

> Base URL: `https://<host>/api/v1`
> Especificación OpenAPI: `https://<host>/openapi/v1.json`

---

## Autenticación

Toda petición se autentica con una **API Key** en el header:

```
X-Api-Key: nsk_xxxxxxxx...
```

- Las keys se crean y revocan desde la Web en **Integraciones** (`/Integraciones`). La raw key se muestra **una sola vez**.
- Si la petición también lleva un JWT válido, el JWT tiene precedencia (la key se ignora).
- Key inválida, revocada o expirada → `401` con código `APIKEY_INVALID`.

### Verificar la key

```
GET /api/v1/ping
X-Api-Key: nsk_...
```

```json
{ "success": true, "data": { "empresaId": 12, "scopes": ["DTE:Write","DTE:Read"], "ok": true } }
```

---

## Scopes

Cada key tiene uno o más scopes; cada endpoint exige el suyo. Sin el scope → `403` (`APIKEY_SCOPE_MISSING`).

| Scope | Permite |
|---|---|
| `DTE:Write` | Emitir DTE |
| `DTE:Read` | Consultar/listar DTE, descargar PDF/JSON |
| `Clientes:Read` | Listar clientes |
| `Clientes:Write` | Dar de alta clientes |
| `Productos:Read` | Listar productos |
| `Productos:Write` | Dar de alta productos |
| `Webhooks:Manage` | (gestión vía webhooks NeoConnect) |

---

## Sandbox vs. Producción

El **ambiente** no se elige por endpoint: lo determina la **configuración DTE de la empresa**
(`AmbienteCodigo` = `PRUEBAS` o `PRODUCCION`). Para integrar sin afectar producción, configura la
empresa en ambiente **PRUEBAS**; todos los DTE emitidos por la API se transmitirán al ambiente de
pruebas de Hacienda. Cambiar a `PRODUCCION` activa la emisión real sin cambios en el código del cliente.

---

## Versionado

NeoConnect es la superficie publica para integradores y por eso versiona en path:

- Version actual: `/api/v1`.
- Cambios compatibles se agregan en `/api/v1`: campos nuevos, filtros opcionales, endpoints nuevos
  o codigos de error mas especificos.
- Un breaking change crea `/api/v2`; `/api/v1` queda activo durante una ventana de deprecacion
  documentada.
- Nunca se remueven scopes, campos o rutas de `/api/v1` sin reemplazo, fecha objetivo y ejemplos de
  migracion.

La politica completa esta en `docs/API-Contratos-Versionado.md`.

---

## Endpoints

### Emitir un DTE — `POST /api/v1/dte` · scope `DTE:Write`

Ejecuta el pipeline completo **borrador → generar → validar → firmar → enviar** y devuelve el documento
en su estado final (idealmente `PROCESADO`). Si un paso falla, devuelve el error de ese paso.

Body: `CreateDteDocumentoRequest` (mismo contrato que la emisión interna: tipo DTE, cliente, líneas, etc.).

```json
{
  "success": true,
  "data": { "id": 1024, "numeroControl": "DTE-01-...", "estadoCodigo": "PROCESADO", "selloRecibido": "..." }
}
```

### Listar DTE — `GET /api/v1/dte` · scope `DTE:Read`
Filtros vía query (`DteListQuery`): estado, tipo, rango de fechas, paginación.

### Consultar un DTE — `GET /api/v1/dte/{id}` · scope `DTE:Read`
Devuelve estado y detalle del documento.

### Descargar PDF — `GET /api/v1/dte/{id}/pdf` · scope `DTE:Read`
Responde `application/pdf`.

### Descargar JSON sellado — `GET /api/v1/dte/{id}/json` · scope `DTE:Read`
Responde `application/json` (el DTE sellado por Hacienda).

### Listar clientes — `GET /api/v1/clientes` · scope `Clientes:Read`
### Alta de cliente — `POST /api/v1/clientes` · scope `Clientes:Write`
Body: `CreateClienteRequest`.

### Listar productos — `GET /api/v1/productos` · scope `Productos:Read`
### Alta de producto — `POST /api/v1/productos` · scope `Productos:Write`
Body: `CreateProductoRequest`.

---

## Formato de respuesta

Todas las respuestas JSON usan el envoltorio estándar `ApiResponse`:

```json
{ "success": true,  "data": { ... }, "traceId": "..." }
{ "success": false, "message": "…", "errors": ["CODIGO"], "traceId": "..." }
```

### Códigos de error frecuentes

| HTTP | Código | Significado |
|---|---|---|
| 401 | `APIKEY_REQUIRED` / `APIKEY_INVALID` | Falta la key o no es válida |
| 403 | `APIKEY_SCOPE_MISSING` | La key no tiene el scope del endpoint |
| 404 | `DTE_NOT_FOUND` / `CLIENTE_NOT_FOUND` / `PRODUCTO_NOT_FOUND` | Recurso inexistente o de otra empresa |
| 409 | `INVALID_STATE` / `VALIDATION` | Transición o datos inválidos en la emisión |
| 429 | `RATE_LIMIT_EXCEEDED` | Cuota de la key/empresa excedida (ver `Retry-After`, `X-RateLimit-*`) |
| 502 | `FIRMA_FAILED` / `HACIENDA_AUTH_FAILED` | Falla al firmar o autenticar contra Hacienda |

---

## Rate limiting

Las peticiones a `/api/v1` consumen la cuota del módulo **NEOCONNECT** y se contabilizan por API Key.
Al exceder el límite se responde `429` con `Retry-After` y cabeceras `X-RateLimit-Limit` / `X-RateLimit-Remaining`.
El consumo por key es visible en **Integraciones → Consumo**.

---

## Webhooks

Independiente de esta API REST: configura un webhook en **Integraciones** y suscríbelo a los eventos
que te interesen. Las entregas se firman con HMAC-SHA256 en el header `X-NeoConnect-Signature` y se
reintentan con backoff exponencial.

### Eventos de facturación

| Evento | Cuándo se dispara |
|---|---|
| `DTE.Procesado` | Hacienda aceptó el documento |
| `DTE.Rechazado` | Hacienda rechazó el documento |
| `DTE.Contingencia` | El documento quedó en contingencia |
| `DTE.Invalidado` | El documento fue invalidado |

Payload: `evento`, `empresaId`, `dteId`, `codigoGeneracion`, `tipoDte`, `estado`, `ocurrioAt`.

### Eventos de negocio

| Evento | Cuándo se dispara | Uso típico |
|---|---|---|
| `Cobros.PagoConfirmado` | Se confirma el pago de una factura | Cerrar el pedido en tu sistema |
| `Compras.OrdenPorAprobar` | Una orden supera el umbral y espera aprobación | Disparar tu flujo de autorización |
| `Inventario.StockBajo` | Un producto **cruza** su stock mínimo | Reabastecimiento automático |
| `Agenda.CitaCreada` | Se agenda una cita | Recordatorios, sincronizar calendario |

Payload: `evento`, `empresaId`, `entidadTipo`, `entidadId`, `descripcion`, `datos`, `ocurrioAt`.
El objeto `datos` varía por evento — por ejemplo, en `Cobros.PagoConfirmado` trae `monto`,
`formaPago`, `saldoRestante` y `saldado`.

> `Inventario.StockBajo` se emite solo al **cruzar** el mínimo, no en cada movimiento posterior:
> así una venta de un producto ya bajo no genera un webhook por venta.

```json
{
  "evento": "Cobros.PagoConfirmado",
  "empresaId": 12,
  "entidadTipo": "PagoCliente",
  "entidadId": 340,
  "descripcion": "Pago de $ 113.00 confirmado sobre DTE-01-M001P001-000000000000042.",
  "datos": {
    "dteDocumentoId": 87,
    "numeroControl": "DTE-01-M001P001-000000000000042",
    "monto": 113.00,
    "formaPago": "TRANSFERENCIA",
    "saldoRestante": 0.00,
    "saldado": true
  },
  "ocurrioAt": "2026-07-27T16:20:11Z"
}
```

---

## Portabilidad de datos

`GET /api/datos/exportar` (permiso `Datos.Exportar`) devuelve un **ZIP con todos los datos de la
empresa en CSV**: perfil, clientes, productos, DTE con su detalle, inventario, cobros, proveedores y
compras. Incluye un `LEEME.txt` con las filas de cada archivo y cómo se cruzan por columnas de Id.
Los CSV van en UTF-8 con BOM para que Excel los abra con los acentos correctos.

También disponible desde la web en **Tus datos**.
