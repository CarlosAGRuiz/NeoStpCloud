# NeoSTP.Api

API REST central de NeoSTP Cloud. Expone la operacion multiempresa de la suite, sirve a la app movil y publica NeoConnect para integradores externos.

La app movil Android vive en el repo externo
`https://github.com/manuelberganza-dev/neocloud_mobile_android`. En NeoSTP Cloud solo se trabaja el
contrato backend/API que esa app consume: endpoints, DTOs, permisos, datos demo y pruebas.

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

**Capacidades enterprise expuestas por API:** un usuario puede pertenecer a varias empresas y cambiar
la activa sin volver a autenticarse (`/api/auth/empresas`, `/api/auth/cambiar-empresa`); hay
consolidado de grupo (`/api/dashboard/grupo`), inventario y traslados por sucursal, aprobacion de
ordenes de compra por umbral, webhooks de negocio ademas de los de DTE, y portabilidad completa de
datos (`/api/datos/exportar`).

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

Al iniciar, la API ejecuta `DatabaseSeeder.SeedAsync(app.Services)` y `EmpresaPruebaSeeder.SeedAsync(app.Services)` cuando `EmpresaPrueba:Enabled=true`. Si `EmpresaPrueba:MobileDemo:Enabled=true`, tambien asegura usuarios `mobile.*` y datos demo API/mobile/comerciales.

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
| `EmpresaPrueba` | Provisioning idempotente de empresa/admin de pruebas y datos API/mobile/comerciales opt-in (`MobileDemo`). |
| `Security` | Password policy y bloqueo de cuenta. |
| `Hardening:RateLimit` | Cuotas/rate limit. |
| `Hardening:Backup` | Backup local o storage externo. |
| `Legal` | Textos legales. |
| `Billing` | Proveedor de pago y credenciales de pasarelas. |
| `Scan` | OCR `Mock` o `Gemini`, MIME permitidos, umbral de confianza y timeout OCR mobile. |
| `Push` | Push `Mock` o `Fcm`. |
| `Pos` | IVA, ancho de ticket, moneda y pie de ticket. |
| `Nomina` | Parametros ISSS/AFP/Renta. |
| `Worker:RecordatoriosCobro` | Job opcional de recordatorios CxC vencida por email/WhatsApp. |
| `Worker:LimpiezaAuditoria` | Purga programada de auditoria por retencion (off por defecto, minimo 30 dias). |
| `WhatsApp` | Proveedor `Mock` o `Meta` (Cloud API: Token, PhoneNumberId; E.164 con +503 por defecto). |
| `Observability:Otlp:Endpoint` | Exporta trazas y metricas OpenTelemetry (Meter `NeoSTP`); vacio = sin overhead. |
| `Cache` | Cache distribuida `Memory` (default) o `Redis` para lookups/catalogos con invalidacion. |
| `Scan:Storage` | Blobs de escaneo en `Database` (default) o `FileSystem` (ruta local/UNC); `/health/ready` valida el root cuando es filesystem. |

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
Para storage, secretos y retencion de documentos fiscales/NeoScan, seguir el runbook HB-7:
[`../../docs/Runbook-Storage-Secretos-Retencion.md`](../../docs/Runbook-Storage-Secretos-Retencion.md).

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

## Politica de versionado

Fuente operativa: [`../../docs/API-Contratos-Versionado.md`](../../docs/API-Contratos-Versionado.md).

- **Tier A (`/api/*`)**: API interna/mobile/demo. No usa version explicita por ahora; se mantiene
  compatible con cambios aditivos. La app Android debe tolerar campos nuevos, pero no se renombraran
  rutas/campos existentes sin ruta paralela o plan de migracion.
- **Tier B (`/api/v1/*`)**: API publica NeoConnect para integradores. Un breaking change externo crea
  `/api/v2`; `/api/v1` queda operativo durante ventana de deprecacion.
- **JSON**: siempre `ApiResponse<T>` y listados con `PagedResult<T>`.
- **Descargas binarias**: PDF/JSON sellado/ticket/CSV/archivo salen como bytes crudos, no como
  `ApiResponse<T>` en 200 OK; los errores si usan el envelope.
- Todo cambio de controller estable debe actualizar README/API, documento mobile o NeoConnect segun
  aplique, y pasar `ApiVersioningContractTests`.

## Catalogo de endpoints

### Plataforma

| Metodo | Ruta | Uso |
|---|---|---|
| GET | `/health` | Health simple JSON. |
| GET | `/health/live` | Liveness. |
| GET | `/health/ready` | Readiness con BD, correo y storage configurado. |
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
| GET | `/api/auth/empresas` | Empresas donde el usuario puede operar: la principal + sus membresias. |
| POST | `/api/auth/cambiar-empresa` | Cambia la empresa activa y reemite el token con los permisos del rol en esa empresa. |

El login federado (SSO OIDC) es interactivo y vive en la Web; por API solo se administra su
configuracion. Ver `/api/sso/config`.

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
| GET | `/api/dashboard/grupo` | Consolidado de todas las empresas del usuario (`?anio=&mes=`). |
| GET/PUT | `/api/sso/config` | Configuracion de SSO corporativo de la empresa. Permiso `Seguridad.Sso.Gestionar`. |
| GET | `/api/datos/exportar` | ZIP con todos los datos de la empresa en CSV. Permiso `Datos.Exportar`. |

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
| POST | `/api/cobros/recordatorios/ejecutar` | Ejecutar recordatorios de facturas vencidas por email/WhatsApp. |
| GET/PUT | `/api/cobros/recordatorios/configuracion` | Configuracion por empresa de recordatorios (reglas, canales, plantillas). |
| GET/POST/PUT | `/api/compras/proveedores` | Proveedores. |
| POST | `/api/compras/proveedores/{id}/inactivar` | Inactivar proveedor. |
| POST | `/api/compras/proveedores/{id}/reactivar` | Reactivar proveedor. |
| GET/POST | `/api/compras/ordenes` | Listar o crear ordenes de compra. |
| GET/PUT | `/api/compras/ordenes/{id}` | Consultar o editar orden en borrador. |
| POST | `/api/compras/ordenes/{id}/emitir` | Emitir orden. Si supera el umbral de la empresa queda en `POR_APROBAR` en vez de `EMITIDA`. |
| POST | `/api/compras/ordenes/{id}/aprobar` | Aprobar una orden `POR_APROBAR` y emitirla. Permiso `Compras.Aprobar`. |
| POST | `/api/compras/ordenes/{id}/rechazar` | Devolver a borrador anotando el motivo. Permiso `Compras.Aprobar`. |
| POST | `/api/compras/ordenes/{id}/cancelar` | Cancelar orden borrador/emitida. |
| POST | `/api/compras/ordenes/{id}/recepciones` | Registrar entrega parcial/completa idempotente e ingresar bienes a inventario. |
| POST | `/api/compras/ordenes/{id}/convertir-factura` | Crear una CxP consolidada al completar recepciones; no duplica inventario. |
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
| POST | `/api/tesoreria/conciliacion/{cuentaId}/importar` | Importar estado de cuenta del banco (CSV/XLSX multipart, dedupe). |
| GET | `/api/tesoreria/conciliacion/{cuentaId}/movimientos` | Lineas bancarias (filtro por estado, con detalles N:1). |
| GET | `/api/tesoreria/conciliacion/{cuentaId}/sugerencias` | Matches sugeridos 1:1 y combinaciones N:1 (confianza ALTA/MEDIA). |
| GET | `/api/tesoreria/conciliacion/{cuentaId}/resumen` | Conciliadas/parciales/pendientes y monto sin conciliar. |
| POST | `/api/tesoreria/conciliacion/movimientos/{id}/conciliar/{movId}` | Aplicar un movimiento interno (acumula; PARCIAL hasta completar). |
| POST | `/api/tesoreria/conciliacion/movimientos/{id}/conciliar-combinacion` | Aplicar varios movimientos de una vez (body: `[ids]`). |
| POST | `/api/tesoreria/conciliacion/movimientos/{id}/quitar/{movId}` | Quitar un movimiento aplicado. |
| POST | `/api/tesoreria/conciliacion/movimientos/{id}/desconciliar` | Desconciliar la linea completa. |
| POST | `/api/tesoreria/conciliacion/{cuentaId}/conciliar-sugeridos` | Aplicar todas las sugerencias de confianza ALTA. |
| GET | `/api/inventario/existencias` | Existencias. |
| GET | `/api/inventario/existencias/{productoId}` | Existencia por producto. |
| GET | `/api/inventario/kardex/{productoId}` | Kardex. |
| POST | `/api/inventario/entradas` | Entrada manual. |
| POST | `/api/inventario/salidas` | Salida manual. |
| POST | `/api/inventario/ajustes` | Ajuste manual. |
| POST | `/api/inventario/stock-minimo` | Stock minimo. |
| POST | `/api/inventario/traslados` | Traslado atomico entre sucursales (salida + entrada con referencia compartida). |

Las lecturas de inventario aceptan `?sucursalId=`. Sin el parametro devuelven el consolidado de
todas las sucursales con costo ponderado; `sucursalId` nulo en los datos significa bodega central.
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
| GET/PUT | `/api/rrhh/prestaciones/politica` | Politica tenant de vacaciones y aguinaldo. |
| GET/POST | `/api/rrhh/vacaciones` | Listar o solicitar vacaciones. |
| GET | `/api/rrhh/vacaciones/empleados/{empleadoId}/resumen` | Saldo devengado, usado y disponible. |
| POST | `/api/rrhh/vacaciones/{id}/aprobar` | Aprobar y calcular prima vacacional. |
| POST | `/api/rrhh/vacaciones/{id}/rechazar` | Rechazar solicitud pendiente. |
| POST | `/api/rrhh/vacaciones/{id}/cancelar` | Cancelar antes de vincular a planilla. |
| GET | `/api/rrhh/aguinaldos/{anio}` | Calculos de aguinaldo del anio. |
| POST | `/api/rrhh/aguinaldos/{anio}/calcular` | Calcular/recalcular registros no aprobados. |
| POST | `/api/rrhh/aguinaldos/{anio}/aprobar` | Aprobar inclusion en planilla. |
| GET/POST | `/api/scanai/documentos` | Bandeja OCR/IA. |
| GET | `/api/scanai/documentos/{id}` | Detalle documento. |
| GET | `/api/scanai/documentos/{id}/archivo` | Archivo original. |
| PUT | `/api/scanai/documentos/{id}/campos` | Corregir campos. |
| POST | `/api/scanai/documentos/{id}/reprocesar` | Reintentar OCR/IA sin duplicar documento. |
| POST | `/api/scanai/documentos/{id}/resultado` | Guardar resultado OCR. |
| POST | `/api/scanai/documentos/{id}/registrar-gasto` | Convertir a gasto. |
| POST | `/api/scanai/documentos/{id}/registrar-compra` | Convertir a compra. |
| POST | `/api/scanai/documentos/{id}/registrar-dte-recibido` | Registrar DTE recibido. |
| POST | `/api/scanai/documentos/{id}/rechazar` | Rechazar documento. |

### NEOCRM

Esquema tecnico: `docs/NEOCRM-Schema-V2-C1.md`.

| Metodo | Ruta | Uso |
|---|---|---|
| GET | `/api/crm/resumen` | KPIs de contactos, pipeline y actividades. |
| GET/POST | `/api/crm/contactos` | Listar o crear contactos CRM. |
| GET/PUT | `/api/crm/contactos/{id}` | Consultar o editar contacto. |
| POST | `/api/crm/contactos/{id}/inactivar` | Inactivar contacto. |
| GET/POST | `/api/crm/etapas` | Listar o crear etapas del pipeline. |
| PUT | `/api/crm/etapas/{id}` | Editar etapa del pipeline. |
| GET/POST | `/api/crm/oportunidades` | Listar o crear oportunidades. |
| GET/PUT | `/api/crm/oportunidades/{id}` | Consultar o editar oportunidad. |
| POST | `/api/crm/oportunidades/{id}/etapa` | Mover oportunidad de etapa; cierra como ganada/perdida si la etapa lo define. |
| GET/POST | `/api/crm/actividades` | Listar o crear actividades. |
| POST | `/api/crm/actividades/{id}/completar` | Completar actividad. |
| POST | `/api/crm/actividades/{id}/cancelar` | Cancelar actividad. |
| GET/POST | `/api/crm/cotizaciones` | Listar o crear cotizaciones con lineas. |
| GET | `/api/crm/cotizaciones/{id}` | Detalle de cotizacion. |
| POST | `/api/crm/cotizaciones/{id}/estado` | Cambiar estado (BORRADOR/ENVIADA/ACEPTADA/RECHAZADA). |
| POST | `/api/crm/cotizaciones/{id}/convertir` | Convertir a Factura/CCF electronica (exige `DTE.Emitir`). |

### Portal del receptor (V2-C2, modulo NEOPORTAL)

Gestion interna de enlaces publicos; el acceso del receptor es por la **web** (`/portal/{token}`),
con token de 256 bits expirable/revocable (solo el hash queda en BD).

| Metodo | Ruta | Uso |
|---|---|---|
| GET | `/api/portal/enlaces` | Listar enlaces (sin exponer el token). |
| POST | `/api/portal/enlaces/documento/{dteId}` | Generar enlace a un DTE (token solo en la respuesta de creacion). |
| POST | `/api/portal/enlaces/estado-cuenta/{clienteId}` | Generar enlace de estado de cuenta del cliente. |
| POST | `/api/portal/enlaces/{id}/revocar` | Revocar enlace (el token muere de inmediato). |

### NeoConta y reportes fiscales (V2-D1/D2, modulos NEOBI y NEOCONTA)

| Metodo | Ruta | Uso |
|---|---|---|
| GET | `/api/conta/cuentas` | Catalogo contable minimo (se siembra al primer uso). |
| POST | `/api/conta/asientos/generar?anio=&mes=` | Genera asientos automaticos del periodo (idempotente). |
| GET | `/api/conta/asientos` | Asientos del periodo. |
| GET | `/api/conta/asientos/{id}` | Detalle con partidas. |
| POST | `/api/conta/asientos/{id}/reversar` | Reversa espejo (no hay borrado). |
| GET | `/api/conta/balanza` (+`/csv`) | Balanza de comprobacion del periodo. |
| GET | `/api/conta/asientos/csv` | Asientos del periodo en formato plano (una fila por movimiento) para importar en un contable externo. |
| GET | `/api/reportes/fiscal/libro-ventas-consumidor` (+`/csv`) | Libro IVA consumidor final por dia. |
| GET | `/api/reportes/fiscal/libro-ventas-contribuyentes` (+`/csv`) | Libro IVA contribuyentes (NC en negativo). |
| GET | `/api/reportes/fiscal/libro-compras` (+`/csv`) | Libro IVA compras. |
| GET | `/api/reportes/fiscal/f07` | Resumen F-07 del periodo. |

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

### Recepciones de orden V3-S2

- `idempotencyKey` es obligatoria (8-64 caracteres) y unica por empresa; repetirla en la misma
  orden devuelve la recepcion existente sin crear otro movimiento.
- Cada linea usa `ordenCompraLineaId` y `cantidad`; el servidor calcula el acumulado y bloquea
  cantidades superiores al pendiente.
- Los bienes generan kardex `RECEPCION_COMPRA`; los servicios solo quedan en el historial.
- Con recepciones, `convertir-factura` se habilita al estado `RECIBIDA` y crea una CxP consolidada
  sin enviar nuevamente lineas a inventario.

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

Ejecutar recordatorios de cobro:

```bash
curl -X POST http://localhost:5058/api/cobros/recordatorios/ejecutar \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d "{\"diasVencidoMinimo\":1,\"maximo\":50,\"enviarEmail\":true,\"enviarWhatsApp\":false}"
```

Ping NeoConnect:

```bash
curl http://localhost:5058/api/v1/ping \
  -H "X-Api-Key: nsk_xxxxx"
```

## Webhooks NeoConnect

**Eventos de facturacion** (payload: `evento`, `empresaId`, `dteId`, `codigoGeneracion`, `tipoDte`,
`estado`, `ocurrioAt`):

- `DTE.Procesado`
- `DTE.Rechazado`
- `DTE.Contingencia`
- `DTE.Invalidado`

**Eventos de negocio** (payload: `evento`, `empresaId`, `entidadTipo`, `entidadId`, `descripcion`,
`datos`, `ocurrioAt`; el objeto `datos` varia por evento):

| Evento | Cuando |
|---|---|
| `Cobros.PagoConfirmado` | Se confirma el pago de una factura. Trae `monto`, `formaPago`, `saldoRestante`, `saldado`. |
| `Compras.OrdenPorAprobar` | Una orden supera el umbral y espera aprobacion. |
| `Inventario.StockBajo` | Un producto **cruza** su stock minimo (no se repite en cada movimiento posterior). |
| `Agenda.CitaCreada` | Se agenda una cita. |

Cada entrega incluye firma HMAC-SHA256:

```http
X-NeoConnect-Signature: sha256=<hex>
```

El worker reintenta con backoff exponencial y marca fallido tras el maximo configurado. El despacho
es best-effort: si la integracion falla, **nunca** rompe la operacion de negocio que lo emitio.

Constantes en `ConnectEventos` (`All`, `Negocio`, `Describir()`); catalogo ampliado y ejemplo de
payload en [`docs/NeoConnect-API-v1.md`](../../docs/NeoConnect-API-v1.md).

## Worker de recordatorios CxC

`RecordatorioCobroWorker` vive en `src/NeoSTP.Worker` y esta deshabilitado por defecto para evitar envios accidentales. Cuando `Worker:RecordatoriosCobro:Enabled=true`, recorre empresas activas y ejecuta el mismo caso de uso del endpoint `/api/cobros/recordatorios/ejecutar`.

Configuracion base:

```json
{
  "Worker": {
    "RecordatoriosCobro": {
      "Enabled": false,
      "IntervaloHoras": 24,
      "DiasVencidoMinimo": 1,
      "MaximoPorEmpresa": 50,
      "EnviarEmail": true,
      "EnviarWhatsApp": false
    }
  }
}
```

El envio por correo usa `ITenantEmailSender`, por lo que respeta SMTP por empresa y fallback global. WhatsApp usa `IWhatsAppSender`; la implementacion actual es `MockWhatsAppSender` y deja el contrato listo para conectar WhatsApp Business API sin cambiar Cobranza. Cada intento queda registrado en `Cobros_Recordatorios` con `EmpresaId`, DTE, cliente, canal, destinatario, estado, saldo y dias vencidos para auditoria e idempotencia diaria por documento/canal.

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

Plan recurrente para demos Web/API: [`../../docs/Plan-Pruebas-Web-Api-Demos.md`](../../docs/Plan-Pruebas-Web-Api-Demos.md).
Plan de sprints de hallazgos y bugs: [`../../docs/Plan-Hallazgos-Bugs-Demo.md`](../../docs/Plan-Hallazgos-Bugs-Demo.md).
Plan especifico de hallazgos API movil: [`../../docs/Plan-Hallazgos-Api-Mobile.md`](../../docs/Plan-Hallazgos-Api-Mobile.md).
Runbook demo API mobile: [`../../docs/Runbook-Api-Mobile-Demo.md`](../../docs/Runbook-Api-Mobile-Demo.md).
Politica de contratos y versionado: [`../../docs/API-Contratos-Versionado.md`](../../docs/API-Contratos-Versionado.md).
Runbook HB-7 de storage, secretos y retencion: [`../../docs/Runbook-Storage-Secretos-Retencion.md`](../../docs/Runbook-Storage-Secretos-Retencion.md).
Runbook HB-8 de demo/release: [`../../docs/Runbook-Demo-Release.md`](../../docs/Runbook-Demo-Release.md).

Preflight ejecutable:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\demo-preflight.ps1 `
  -Profile Demo -RequireServices -ApiBaseUrl http://localhost:5058 `
  -WebBaseUrl http://localhost:5031 -EvidencePath tmp\demo-preflight.json
```

```bash
dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj
dotnet test tests/NeoSTP.Tests.Integration/NeoSTP.Tests.Integration.csproj
```

Areas con cobertura relevante:

- Auth, password policy, MFA, hardening y quotas.
- DTE, Hacienda mocks, correo SMTP y DTE recibido.
- NeoConnect API keys, middleware y webhooks.
- Cobros, compras, inventario, POS/caja, RRHH, NeoProfit, NeoScanAI y contrato mobile operativo.
- Demo readiness HB-3/HB-4: rutas API criticas, permisos, modulos licenciables, NeoConnect v1,
  rutas Web y vistas Razor protegidas por `DemoReadinessContractTests`.
- Datos demo HB-5: seed idempotente de DTE, compras/CxP, inventario, tesoreria, portal, CRM,
  RRHH y Profit cubierto por `EmpresaPruebaSeederTests`.
- Contratos/versionado HB-6: rutas Tier A `/api/*`, NeoConnect `/api/v1`, content-types binarios,
  docs enlazadas y politica de breaking changes cubiertas por `ApiVersioningContractTests`.
- Storage/secretos/retencion HB-7: readiness de storage `Database`/`FileSystem`, provider invalido,
  runbook operativo y guardrail anti-rutas absolutas en NeoScan cubiertos por `Hb7StorageSecretRetentionTests`.
- Demo/release HB-8: preflight de codigo, secretos, providers, build/tests, health/OpenAPI, decision
  y evidencia sanitizada cubierto por `Hb8DemoReleaseTests`.
- V3-S1 ordenes de compra: calculo server-side, tenant, estados, conversion unica a CxP/inventario,
  seed demo, rutas y permisos cubiertos por `OrdenCompraServiceTests` y `V3OrdenCompraContractTests`.
- V3-S2 recepciones: acumulados por linea, estado parcial/completo, idempotencia, kardex enlazado,
  facturacion consolidada y rutas/vistas Web cubiertas por las suites de ordenes y demo readiness.
- V3-S3 prestaciones RRHH: politica tenant, saldo/traslape de vacaciones, prima y aguinaldo por
  antiguedad/proporcionalidad; planilla, seed, rutas/permisos y Web cubiertos por las suites V3-S3.
- Integracion Scan/Profit/DTE recibido y Cobranza/alertas.
- Recordatorios de cobranza: envio, omision, frecuencia configurable, plantillas e historial.
- NEOCRM: contactos, pipeline default por empresa, oportunidades, cierre ganado/perdido, actividades y cotizacion a DTE.
- NeoPortal: tokens (hash, expiracion, revocacion) y aislamiento por empresa/cliente.
- NeoConta: doble partida, idempotencia, reversa espejo y balanza cuadrada.
- Reportes fiscales: LibroIvaCalculator (NC resta, ND suma, INVALIDADO excluido).
- Conciliacion bancaria: matcher 1:1 y combinaciones N:1, import con dedupe, parciales.
- WhatsApp Meta: payload, normalizacion E.164 y manejo de errores con HTTP simulado.
- Operacion: purga de auditoria por retencion y storage externo de escaneos.

Estado actual validado 2026-06-20: `dotnet build NeoSTP.slnx` con 0 warnings/0 errores y
`dotnet test NeoSTP.slnx` con 750 unitarias + 9 integracion. La suite incluye contrato mobile
operativo (`MobileApiContractOperationalTests`), demo readiness HB-3/HB-4
(`DemoReadinessContractTests`), datos demo HB-5 (`EmpresaPruebaSeederTests`) y versionado HB-6
(`ApiVersioningContractTests`) sin cambios breaking de API, mas HB-7 (`Hb7StorageSecretRetentionTests`)
para storage/secretos/retencion y HB-8 (`Hb8DemoReleaseTests`) para preflight/demo/release.
