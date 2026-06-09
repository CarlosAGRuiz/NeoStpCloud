# NeoSTP.Api

API REST central de NeoSTP Cloud. Expone la operacion multiempresa de la suite, sirve a la app movil y publica NeoConnect para integradores externos.

## Resumen

- Proyecto: `src/NeoSTP.Api/NeoSTP.Api.csproj`.
- OpenAPI JSON: `/openapi/v1.json`.
- Explorador Scalar: `/scalar/v1`.
- Health simple: `/health`.
- Health checks: `/health/live` y `/health/ready`.
- Respuesta estandar: `ApiResponse<T>`.
- Auth interna: JWT Bearer.
- Auth integradores: API Key en `X-Api-Key`.
- Tenant: todo dato operativo debe quedar aislado por `EmpresaId`.

La API carga `appsettings.Local.json` si existe, aplica migraciones/seed al arrancar y registra request logging con Serilog.

## Arquitectura

`NeoSTP.Api` es la entrada HTTP. Los controllers delegan en servicios de `NeoSTP.Application` y `NeoSTP.Infrastructure`; no deben concentrar reglas de negocio pesadas.

Flujo normal:

1. Controller recibe el request y valida auth, permiso y modulo.
2. Servicio ejecuta el caso de uso.
3. `Result` o `Result<T>` se mapea con `ApiControllerBase.Respond`.
4. La respuesta sale como `ApiResponse<T>` con `traceId`.

Middlewares principales, en orden:

1. `ApiKeyAuthMiddleware`: valida `X-Api-Key`; si hay JWT valido, JWT tiene precedencia.
2. `AdminIpAllowlistMiddleware`: limita superficies administrativas segun allowlist.
3. `CurrentTenantMiddleware`: exige empresa resuelta salvo auth, health, OpenAPI, SuperAdmin o API Key.
4. `ApiQuotaMiddleware`: aplica cuotas y registra uso por usuario, empresa o API Key.

## Ejecucion local

```bash
dotnet build NeoSTP.slnx
dotnet run --project src/NeoSTP.Api
```

Con Docker:

```bash
docker compose up --build api
```

Al iniciar, la API ejecuta `DatabaseSeeder.SeedAsync(app.Services)` y `EmpresaPruebaSeeder.SeedAsync(app.Services)` cuando `EmpresaPrueba:Enabled=true`.

## Configuracion

Los secretos locales deben vivir en `src/NeoSTP.Api/appsettings.Local.json`, que esta ignorado por git.

| Seccion | Uso |
|---|---|
| `ConnectionStrings:NeoStpDb` | SQL Server de NeoSTP Cloud. |
| `Jwt` | Issuer, audience, secret y expiracion del access token. |
| `Cors:AllowedOrigins` | Origenes permitidos; en Development permite cualquier origen si esta vacio. |
| `Email` | Proveedor `Mock` o `Smtp`, remitente global y SMTP global. |
| `Hacienda` | Cliente Hacienda `Mock` o `Http`. |
| `Dte` / `Dte:Territorial` | Firma, ambiente DTE y datos territoriales por defecto. |
| `EmpresaPrueba` | Provisioning idempotente de empresa/admin de pruebas. |
| `Security` | Password policy y bloqueo de cuenta. |
| `Hardening:RateLimit` | Cuotas/rate limit. |
| `Hardening:Backup` | Backup local o storage externo. |
| `Legal` | Textos legales. |
| `Billing` | Proveedor de pago y credenciales de pasarelas. |
| `Scan` | OCR `Mock` o `Gemini`. |
| `Push` | Push `Mock` o `Fcm`. |
| `Pos` | IVA, ancho de ticket, moneda y pie de ticket. |
| `Nomina` | Parametros ISSS/AFP/Renta. |

Ejemplo minimo:

```json
{
  "ConnectionStrings": {
    "NeoStpDb": "Server=.;Database=NeoSTP_Cloud;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "Jwt": {
    "Key": "replace-me-with-a-strong-32-plus-char-secret"
  },
  "Email": {
    "Provider": "Mock"
  },
  "Hacienda": {
    "Client": "Mock"
  },
  "Dte": {
    "Signer": "Mock"
  }
}
```

Nunca documentar ni commitear claves reales, certificados, passwords SMTP, API keys ni secretos de webhooks.

## Autenticacion

### JWT Bearer

Usado por usuarios internos, web, movil y endpoints administrativos.

1. Login: `POST /api/auth/login`.
2. Enviar el access token:

```http
Authorization: Bearer <jwt>
```

Los controllers protegidos usan `[Authorize]`, `[RequirePermiso("...")]` y, cuando aplica, `[RequireModule("...")]`.

### API Key NeoConnect

Usada por integradores externos en `/api/v1`.

```http
X-Api-Key: nsk_xxxxx
```

Las keys se crean desde `/api/connect/api-keys` o desde la UI `/Integraciones`. La raw key se muestra una sola vez; la base guarda hash SHA-256 y prefijo visible.

Scopes actuales:

| Scope | Uso |
|---|---|
| `DTE:Write` | Emitir DTE por `/api/v1/dte`. |
| `DTE:Read` | Listar, consultar y descargar DTE. |
| `Clientes:Read` | Listar clientes. |
| `Clientes:Write` | Crear clientes. |
| `Productos:Read` | Listar productos. |
| `Productos:Write` | Crear productos. |
| `Webhooks:Manage` | Gestion de webhooks NeoConnect. |

Errores NeoConnect frecuentes:

| HTTP | Codigo | Significado |
|---|---|---|
| 401 | `APIKEY_REQUIRED` | Falta `X-Api-Key`. |
| 401 | `APIKEY_INVALID` | Key invalida, revocada o expirada. |
| 403 | `APIKEY_SCOPE_MISSING` | La key no tiene el scope requerido. |
| 429 | `RATE_LIMIT_EXCEEDED` | Cuota excedida. |

## Formato de respuesta

Exito:

```json
{
  "success": true,
  "message": null,
  "data": {},
  "errors": [],
  "traceId": "..."
}
```

Error:

```json
{
  "success": false,
  "message": "Descripcion del error",
  "data": null,
  "errors": ["VALIDATION"],
  "traceId": "..."
}
```

Mapeo general:

| HTTP | Casos comunes |
|---|---|
| 400 | Validacion, password debil, relaciones invalidas. |
| 401 | Credenciales invalidas, refresh invalido, API Key faltante/invalida. |
| 402 | Licencia o plan invalido. |
| 403 | Empresa prohibida, usuario sin tenant o scope faltante. |
| 404 | Recurso no encontrado o de otra empresa. |
| 409 | Estado invalido, duplicado, limite excedido, stock insuficiente, caja abierta. |
| 429 | Rate limit/cuota excedida. |
| 502 | Firma, Hacienda, email, lote o impresora fallida. |
| 500 | Fallo critico como desencriptado de secreto. |

## Catalogo de endpoints

### Plataforma

| Metodo | Ruta | Uso |
|---|---|---|
| GET | `/health` | Health simple JSON. |
| GET | `/health/live` | Liveness. |
| GET | `/health/ready` | Readiness con BD. |
| GET | `/openapi/v1.json` | Especificacion OpenAPI. |
| GET | `/scalar/v1` | Explorador interactivo. |

### Auth

| Metodo | Ruta | Uso |
|---|---|---|
| POST | `/api/auth/login` | Login con usuario/password y posible MFA. |
| POST | `/api/auth/refresh` | Renovar access token. |
| POST | `/api/auth/logout` | Revocar refresh token. |
| POST | `/api/auth/change-password` | Cambiar password. |
| POST | `/api/auth/mfa/enroll` | Enrolar TOTP. |
| POST | `/api/auth/mfa/confirm` | Confirmar MFA. |
| POST | `/api/auth/mfa/disable` | Desactivar MFA. |
| GET | `/api/auth/me` | Usuario autenticado. |

### Core

| Metodo | Ruta | Uso |
|---|---|---|
| GET/POST | `/api/empresas` | Listar o crear empresas. |
| GET/PUT | `/api/empresas/{id}` | Consultar o editar empresa. |
| GET | `/api/empresas/{id}/licencia` | Licencia y modulos. |
| POST | `/api/empresas/{id}/plan` | Cambiar plan. |
| POST | `/api/empresas/{id}/modulos/{moduloId}/activar` | Activar modulo. |
| POST | `/api/empresas/{id}/modulos/{moduloId}/desactivar` | Desactivar modulo. |
| GET/POST/PUT | `/api/usuarios` | CRUD de usuarios. |
| POST | `/api/usuarios/{id}/reset-password` | Reset administrativo. |
| GET/POST/PUT | `/api/roles` | CRUD de roles. |
| GET | `/api/roles/permisos` | Catalogo de permisos. |
| GET | `/api/planes` | Planes comerciales. |
| GET | `/api/planes/{id}` | Detalle de plan. |
| GET | `/api/modulos` | Modulos licenciables. |
| GET/POST/PUT | `/api/sucursales` | Sucursales. |
| GET/POST/PUT | `/api/puntos-venta` | Puntos de venta. |
| GET | `/api/dashboard/empresa` | Dashboard empresa. |
| GET | `/api/dashboard/superadmin` | Dashboard SuperAdmin. |

### Catalogos, clientes, productos y lookups

| Metodo | Ruta | Uso |
|---|---|---|
| GET/POST | `/api/catalogos` | Listar o crear catalogos. |
| GET/PUT | `/api/catalogos/{codigo}` | Consultar o editar catalogo. |
| GET/POST | `/api/catalogos/{codigo}/items` | Items de catalogo. |
| PUT/DELETE | `/api/catalogos/{codigo}/items/{id}` | Editar o eliminar item. |
| POST | `/api/catalogos/{codigo}/import` | Importar catalogo. |
| GET | `/api/catalogos/{codigo}/export` | Exportar catalogo. |
| GET/POST | `/api/clientes` | Listar o crear clientes. |
| GET/PUT | `/api/clientes/{id}` | Consultar o editar cliente. |
| GET/POST | `/api/productos` | Listar o crear productos. |
| GET/PUT | `/api/productos/{id}` | Consultar o editar producto. |
| GET | `/api/lookups/catalogo/{codigo}` | Items de catalogo. |
| GET | `/api/lookups/departamentos` | Departamentos. |
| GET | `/api/lookups/municipios` | Municipios. |
| GET | `/api/lookups/distritos` | Distritos. |
| GET | `/api/lookups/clientes` | Selector de clientes. |
| GET | `/api/lookups/productos` | Selector de productos. |
| GET | `/api/lookups/sucursales` | Selector de sucursales. |
| GET | `/api/lookups/verificar-nit` | Verificacion NIT. |

### DTE, certificacion, eventos y contingencia

| Metodo | Ruta | Uso |
|---|---|---|
| GET/PUT | `/api/dte/configuracion` | Configuracion DTE cifrada. |
| POST/DELETE | `/api/dte/configuracion/certificado` | Cargar o eliminar certificado. |
| POST | `/api/dte/configuracion/probar-conexion` | Probar MH. |
| GET | `/api/dte/documentos` | Listar DTE. |
| GET | `/api/dte/documentos/{id}` | Detalle DTE. |
| POST | `/api/dte/documentos` | Crear borrador generico. |
| POST | `/api/dte/factura` | Crear factura. |
| POST | `/api/dte/credito-fiscal` | Crear CCF. |
| POST | `/api/dte/nota-credito` | Crear nota de credito. |
| POST | `/api/dte/nota-debito` | Crear nota de debito. |
| POST | `/api/dte/sujeto-excluido` | Crear sujeto excluido. |
| POST | `/api/dte/emitir` | Emision en un paso. |
| POST | `/api/dte/emitir/{tipo}` | Emision en un paso por tipo. |
| POST | `/api/dte/documentos/{id}/generar` | Generar JSON. |
| POST | `/api/dte/documentos/{id}/validar` | Validar DTE. |
| POST | `/api/dte/documentos/{id}/firmar` | Firmar JWS. |
| POST | `/api/dte/documentos/{id}/enviar` | Enviar a Hacienda. |
| POST | `/api/dte/documentos/{id}/invalidar` | Invalidar DTE. |
| GET | `/api/dte/documentos/{id}/pdf` | Descargar PDF. |
| GET | `/api/dte/documentos/{id}/json` | Descargar JSON sellado. |
| POST | `/api/dte/documentos/{id}/reenviar` | Reenviar correo. |
| GET | `/api/certificacion/resumen` | Resumen matriz. |
| GET | `/api/certificacion/matriz` | Matriz de escenarios. |
| GET | `/api/certificacion/tipos/{codigo}/escenarios` | Escenarios por tipo. |
| GET | `/api/certificacion/errores` | Errores. |
| POST | `/api/certificacion/tipos/{codigo}/generar-prueba` | Generar prueba. |
| POST | `/api/certificacion/documentos/{id}/marcar-completado` | Completar por DTE. |
| POST | `/api/certificacion/eventos/{id}/marcar-completado` | Completar por evento. |
| POST | `/api/certificacion/documentos/{id}/reintentar` | Reintentar prueba. |
| GET | `/api/dte/eventos` | Listar eventos DTE. |
| GET | `/api/dte/eventos/{id}` | Detalle evento. |
| GET | `/api/dte/eventos/{id}/json` | JSON del evento. |
| GET | `/api/dte/eventos/{id}/pdf` | PDF del evento. |
| POST | `/api/dte/eventos/invalidacion` | Evento invalidacion. |
| POST | `/api/dte/eventos/contingencia` | Evento contingencia. |
| POST | `/api/dte/eventos/retorno` | Evento retorno. |
| POST | `/api/dte/eventos/operaciones-especiales` | Evento operaciones especiales. |
| GET | `/api/dte/contingencia/resumen` | Resumen contingencia. |
| GET | `/api/dte/contingencia/documentos` | DTE en contingencia. |
| GET | `/api/dte/contingencia/lotes` | Lotes. |
| GET | `/api/dte/contingencia/lotes/{loteId}` | Detalle lote. |
| POST | `/api/dte/contingencia/lotes/crear` | Crear lote. |
| POST | `/api/dte/contingencia/lotes/{loteId}/consultar` | Consultar lote. |
| POST | `/api/dte/contingencia/documentos/{dteId}/reintentar` | Reintentar envio. |
| GET/POST | `/api/dte/diagnostico/*` | Diagnostico de errores MH. |

### Cobros, compras, tesoreria e inventario

| Metodo | Ruta | Uso |
|---|---|---|
| GET | `/api/cobros/resumen` | Resumen CxC. |
| GET | `/api/cobros/pendientes` | Cuentas pendientes. |
| GET | `/api/cobros/clientes/{clienteId}` | Estado de cliente. |
| GET/POST | `/api/cobros/dte/{dteId}/pagos` | Pagos de un DTE. |
| POST | `/api/cobros/pagos/{pagoId}/confirmar` | Confirmar pago. |
| POST | `/api/cobros/pagos/{pagoId}/anular` | Anular pago. |
| GET/POST/PUT | `/api/cobros/cuentas` | Cuentas de cobro. |
| POST | `/api/cobros/cuentas/{id}/inactivar` | Inactivar cuenta. |
| POST | `/api/cobros/qr` | Generar QR/enlace. |
| GET/POST/PUT | `/api/compras/proveedores` | Proveedores. |
| POST | `/api/compras/proveedores/{id}/inactivar` | Inactivar proveedor. |
| POST | `/api/compras/proveedores/{id}/reactivar` | Reactivar proveedor. |
| GET/POST | `/api/compras/facturas` | Facturas de compra. |
| GET | `/api/compras/facturas/{id}` | Detalle factura. |
| POST | `/api/compras/facturas/{id}/anular` | Anular factura. |
| POST | `/api/compras/pagos` | Registrar pago proveedor. |
| POST | `/api/compras/pagos/{id}/anular` | Anular pago proveedor. |
| GET | `/api/compras/resumen` | Resumen CxP. |
| GET/POST/PUT | `/api/tesoreria/cuentas` | Cuentas banco/caja. |
| POST | `/api/tesoreria/cuentas/{id}/inactivar` | Inactivar cuenta. |
| POST | `/api/tesoreria/cuentas/{id}/reactivar` | Reactivar cuenta. |
| GET/POST | `/api/tesoreria/movimientos` | Movimientos. |
| POST | `/api/tesoreria/movimientos/{id}/anular` | Anular movimiento. |
| GET | `/api/tesoreria/resumen` | Resumen tesoreria. |
| GET | `/api/inventario/existencias` | Existencias. |
| GET | `/api/inventario/existencias/{productoId}` | Existencia por producto. |
| GET | `/api/inventario/kardex/{productoId}` | Kardex. |
| POST | `/api/inventario/entradas` | Entrada manual. |
| POST | `/api/inventario/salidas` | Salida manual. |
| POST | `/api/inventario/ajustes` | Ajuste manual. |
| POST | `/api/inventario/stock-minimo` | Stock minimo. |
| GET | `/api/inventario/resumen` | Resumen inventario. |

### POS y caja

| Metodo | Ruta | Uso |
|---|---|---|
| GET/POST | `/api/pos/ventas` | Listar o crear venta POS. |
| GET | `/api/pos/ventas/{id}` | Detalle venta. |
| POST | `/api/pos/ventas/{id}/anular` | Anular venta. |
| POST | `/api/pos/ventas/{id}/promover` | Promover a DTE. |
| GET | `/api/pos/ventas/{id}/ticket` | Ticket PDF. |
| POST | `/api/pos/ventas/{id}/enviar` | Enviar ticket por correo. |
| GET | `/api/pos/resumen` | Resumen del dia. |
| GET/POST | `/api/pos/impresoras` | Impresoras POS. |
| DELETE | `/api/pos/impresoras/{id}` | Eliminar impresora. |
| POST | `/api/pos/ventas/{ventaId}/imprimir-red/{impresoraId}` | Imprimir por red. |
| POST | `/api/pos/impresoras/{impresoraId}/probar` | Probar impresora. |
| GET | `/api/pos/caja/estado` | Estado de caja. |
| GET | `/api/pos/caja` | Historial de sesiones. |
| GET | `/api/pos/caja/{id}` | Detalle de sesion. |
| POST | `/api/pos/caja/abrir` | Abrir caja. |
| POST | `/api/pos/caja/{id}/cerrar` | Cerrar caja. |

### NeoProfit, RRHH y NeoScanAI

| Metodo | Ruta | Uso |
|---|---|---|
| GET | `/api/profit/dashboard` | KPIs financieros. |
| GET | `/api/profit/productos` | Rentabilidad por producto. |
| GET | `/api/profit/clientes` | Rentabilidad por cliente. |
| GET | `/api/profit/sucursales` | Rentabilidad por sucursal. |
| GET | `/api/profit/tendencia` | Tendencia financiera. |
| GET/POST/PUT/DELETE | `/api/profit/gastos` | Gastos NeoProfit. |
| GET/POST/PUT/DELETE | `/api/profit/compras` | Compras NeoProfit. |
| GET/POST/PUT | `/api/rrhh/empleados` | Empleados. |
| POST | `/api/rrhh/empleados/{id}/inactivar` | Inactivar empleado. |
| GET/POST | `/api/rrhh/planillas` | Planillas. |
| GET | `/api/rrhh/planillas/{id}` | Detalle planilla. |
| POST | `/api/rrhh/planillas/{id}/cerrar` | Cerrar planilla. |
| POST | `/api/rrhh/planillas/{id}/anular` | Anular planilla. |
| GET | `/api/rrhh/planillas/{id}/recibo/{empleadoId}` | Recibo PDF. |
| GET/POST | `/api/scanai/documentos` | Bandeja OCR/IA. |
| GET | `/api/scanai/documentos/{id}` | Detalle documento. |
| GET | `/api/scanai/documentos/{id}/archivo` | Archivo original. |
| PUT | `/api/scanai/documentos/{id}/campos` | Corregir campos. |
| POST | `/api/scanai/documentos/{id}/resultado` | Guardar resultado OCR. |
| POST | `/api/scanai/documentos/{id}/registrar-gasto` | Convertir a gasto. |
| POST | `/api/scanai/documentos/{id}/registrar-compra` | Convertir a compra. |
| POST | `/api/scanai/documentos/{id}/registrar-dte-recibido` | Registrar DTE recibido. |
| POST | `/api/scanai/documentos/{id}/rechazar` | Rechazar documento. |

### Alertas, correo, hardening y billing

| Metodo | Ruta | Uso |
|---|---|---|
| GET | `/api/alertas` | Listar alertas. |
| GET | `/api/alertas/resumen` | Resumen. |
| POST | `/api/alertas/{id}/leer` | Marcar leida. |
| POST | `/api/alertas/{id}/resolver` | Resolver alerta. |
| POST | `/api/alertas/leer-todas` | Marcar todas leidas. |
| POST | `/api/alertas/generar` | Generar alertas. |
| POST | `/api/alertas/dispositivos` | Registrar dispositivo push. |
| POST | `/api/alertas/dispositivos/eliminar` | Eliminar dispositivo push. |
| GET/PUT | `/api/alertas/preferencias` | Preferencias. |
| GET/PUT | `/api/correo` | SMTP por empresa. |
| POST | `/api/correo/probar` | Correo de prueba. |
| GET/POST | `/api/hardening/backups` | Backups. |
| GET/POST/DELETE | `/api/hardening/cuotas` | Cuotas. |
| GET/POST/DELETE | `/api/hardening/ip-allowlist` | Allowlist admin. |
| POST | `/api/billing/webhooks/stripe` | Webhook Stripe. |
| POST | `/api/billing/webhooks/mercadopago` | Webhook MercadoPago. |

### NeoConnect gestion y API publica v1

| Metodo | Ruta | Uso |
|---|---|---|
| GET/POST | `/api/connect/api-keys` | Listar o crear API keys. |
| GET | `/api/connect/api-keys/{id}` | Detalle de key. |
| GET/POST | `/api/connect/webhooks` | Listar o crear webhooks. |
| GET/DELETE | `/api/connect/webhooks/{id}` | Detalle o eliminar webhook. |
| POST | `/api/connect/webhooks/{id}/test` | Enviar prueba firmada. |
| GET | `/api/connect/logs` | Log de entregas webhook. |
| GET | `/api/connect/usage` | Consumo por API Key. |
| GET | `/api/v1/ping` | Verificar API Key y scopes. |
| GET/POST | `/api/v1/dte` | Listar o emitir DTE por API Key. |
| GET | `/api/v1/dte/{id}` | Consultar DTE. |
| GET | `/api/v1/dte/{id}/pdf` | Descargar PDF. |
| GET | `/api/v1/dte/{id}/json` | Descargar JSON sellado. |
| GET/POST | `/api/v1/clientes` | Listar o crear clientes. |
| GET/POST | `/api/v1/productos` | Listar o crear productos. |

## Ejemplos

Login:

```bash
curl -X POST http://localhost:5058/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"superadmin\",\"password\":\"ChangeMe!2026\"}"
```

Request autenticado:

```bash
curl http://localhost:5058/api/auth/me \
  -H "Authorization: Bearer <jwt>"
```

Ping NeoConnect:

```bash
curl http://localhost:5058/api/v1/ping \
  -H "X-Api-Key: nsk_xxxxx"
```

## Webhooks NeoConnect

Eventos soportados:

- `DTE.PROCESADO`
- `DTE.RECHAZADO`
- `DTE.CONTINGENCIA`
- `DTE.INVALIDADO`

Cada entrega incluye firma HMAC-SHA256:

```http
X-NeoConnect-Signature: sha256=<hex>
```

El worker reintenta con backoff exponencial y marca fallido tras el maximo configurado.

## Convenciones para ampliar la API

- Mantener controllers delgados.
- Dejar interfaces en `NeoSTP.Application` e implementaciones en `NeoSTP.Infrastructure`.
- Usar `Result`/`Result<T>` y `Respond(...)`.
- Agregar `[RequireModule("...")]` cuando el endpoint pertenezca a un modulo licenciable.
- Agregar `[RequirePermiso("...")]` para superficies protegidas.
- Respetar `EmpresaId` en todo query/command.
- No exponer secretos completos; mostrar prefijos o estados.
- Registrar auditoria en acciones de negocio relevantes.
- Si el endpoint es publico para integradores, definir scope NeoConnect y documentarlo.
- Cubrir calculadoras/servicios con pruebas unitarias antes de depender de UI.

## Pruebas relacionadas

```bash
dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj
dotnet test tests/NeoSTP.Tests.Integration/NeoSTP.Tests.Integration.csproj
```

Areas con cobertura relevante:

- Auth, password policy, MFA, hardening y quotas.
- DTE, Hacienda mocks, correo SMTP y DTE recibido.
- NeoConnect API keys, middleware y webhooks.
- Cobros, compras, inventario, POS/caja, RRHH, NeoProfit, NeoScanAI.
- Integracion Scan/Profit/DTE recibido y Cobranza/alertas.
