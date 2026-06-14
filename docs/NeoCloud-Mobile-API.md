# NeoCloud Mobile — Guía de integración de la API

Documento técnico para el desarrollo de **NeoCloud Mobile** (Flutter, Android tablet/celular).
La app consume **la misma API y base de datos** que el panel web (NeoSTP.Api): no es un backend separado,
sino un cliente ligero sobre la API REST de NeoSTP Cloud.

> **Audiencia:** backend/API NeoSTP y desarrollador Flutter.
> **Backend:** lo mantenemos nosotros (NeoSTP). Este documento define el contrato que la app debe consumir.
> **App Android:** repo externo `https://github.com/manuelberganza-dev/neocloud_mobile_android`.
> La app ya fue trabajada; en este repositorio solo se mantiene la API, su contrato, pruebas y datos demo.
> **Acompaña a:** `Plan-Hallazgos-Api-Mobile.md` (hallazgos API contra la app real) y
> `NeoCloud-Mobile-Plan.md` (referencia historica del alcance backend/mobile).
>
> **Estado del backend: COMPLETO.** Todos los endpoints de este documento están **implementados, probados y
> desplegados** en `main`. Las únicas piezas con proveedor *mock* (pluggable, sin cambiar el contrato) son la
> **extracción OCR/IA** de NeoScan y el **envío push real (FCM)**; la app se construye igual y la integración
> real se "enciende" por configuración cuando haya credenciales.
>
> **Revision 2026-06-14:** contrato contrastado contra `api_endpoints.dart`, `api_client.dart` y repositorios
> Flutter de auth, DTE, cobros, NeoScan, alertas y POS. Hallazgos de compatibilidad en
> `docs/Plan-Hallazgos-Api-Mobile.md`.

---

## 1. Arquitectura y principios

```
┌─────────────────────────┐        HTTPS / JSON         ┌──────────────────────────┐
│   NeoCloud Mobile        │  ──────────────────────────▶│   NeoSTP.Api (ASP.NET)    │
│   (Flutter)              │   Bearer JWT (Authorization)│   misma API que la Web     │
│                          │◀──────────────────────────  │                            │
└─────────────────────────┘     ApiResponse<T> (JSON)    └────────────┬─────────────┘
                                                                       │ EF Core
                                                          ┌────────────▼─────────────┐
                                                          │ SQL Server (misma BD)     │
                                                          └────────────┬─────────────┘
                                                                       │ firma + transmisión
                                                          ┌────────────▼─────────────┐
                                                          │ Ministerio de Hacienda SV │
                                                          │ (apitest / api producción)│
                                                          └──────────────────────────┘
```

- **Una sola fuente de verdad:** la app NO firma ni transmite a Hacienda; solo invoca endpoints. El
  backend hace la firma con el certificado y la transmisión a MH.
- **Multi-tenant:** cada usuario pertenece a una empresa (`empresaId` viaja en el JWT). **La app móvil es
  solo para usuarios de empresa: no maneja SuperAdmin ni envía `?empresaId`** (el tenant es implícito y
  único, ver §5). El parámetro `?empresaId` que aparece en algunos endpoints es exclusivo del panel web/SuperAdmin
  y la app debe ignorarlo.
- **Sin estado en el cliente** salvo los tokens. Toda la lógica fiscal vive en el backend.
- **Ligera:** la app pagina y filtra del lado servidor (`page`, `pageSize`, `search`), nunca trae
  datasets completos.

---

## 2. Entornos y URL base

| Entorno | URL base API | Notas |
|---|---|---|
| Desarrollo local | `https://localhost:7043` / `http://localhost:5058` | launchSettings de `NeoSTP.Api` |
| Pruebas / staging | (definido por NeoSTP) | apunta a Hacienda **apitest** |
| Producción | (definido por NeoSTP) | apunta a Hacienda **producción** |

- Todas las rutas cuelgan de `/{base}/api/...`.
- **Explorador interactivo (Scalar):** `GET /scalar/v1` (o `/scalar`, redirige) — UI para navegar y **probar** los endpoints, con botón *Authorize* para pegar el Bearer JWT. Lee el spec OpenAPI.
- **Spec OpenAPI (JSON):** `GET /openapi/v1.json` — para importar en Postman o generar clientes.
- **Health check:** `GET /health` → `{ "data": { "status": "ok", "service": "NeoSTP.Api" } }`. Úsalo para el "ping" de conectividad en el splash.
- **CORS:** afecta solo a navegadores; una app Flutter nativa (Dart `http`/`dio`) **no** está sujeta a CORS. No requiere configuración especial.
- **Ambiente DTE (PRUEBAS/PRODUCCION):** NO lo decide la app; lo determina la **configuración DTE de la empresa** (ver §6). La app solo muestra el ambiente vigente.

---

## 3. Formato de respuesta estándar (`ApiResponse<T>`)

**Todos** los endpoints (salvo descargas binarias) devuelven este sobre JSON:

```jsonc
// Éxito
{ "success": true,  "message": null, "data": { /* T */ }, "errors": [], "traceId": "0HM..." }

// Error
{ "success": false, "message": "Mensaje legible", "data": null, "errors": ["CODIGO"], "traceId": "0HM..." }
```

- `success` (bool): decide el flujo en el cliente.
- `data`: el payload tipado (objeto, lista o `PagedResult<T>`).
- `errors`: lista de **códigos** o mensajes de validación (ver §10).
- `traceId`: inclúyelo en reportes de soporte; correlaciona con los logs del backend.

**Listas paginadas** (`PagedResult<T>`):

```jsonc
{ "success": true, "data": {
    "items": [ /* T[] */ ],
    "total": 137, "page": 1, "pageSize": 20, "totalPages": 7
} }
```

> Recomendación Flutter: un único `ApiClient` que deserializa `ApiResponse<T>`, lanza una excepción
> tipada `ApiException(message, errors, statusCode, traceId)` cuando `success == false`, y centraliza el
> manejo de 401 (refresh) y 403/429.

---

## 4. Autenticación (JWT)

La app se autentica **igual que la web**: usuario + contraseña → JWT. No usa API Keys (esas son para
integraciones servidor-a-servidor de NeoConnect).

### 4.1 Login — `POST /api/auth/login`  (anónimo)

```jsonc
// Request
{ "usernameOrEmail": "vendedor1", "password": "••••••", "mfaCode": null }

// Response (data)
{
  "accessToken": "eyJhbGciOi...",
  "accessTokenExpiresAt": "2026-06-04T15:00:00Z",
  "refreshToken": "b64-opaque-token",
  "refreshTokenExpiresAt": "2026-06-11T14:00:00Z",
  "user": {
    "id": 12, "empresaId": 5, "username": "vendedor1", "email": "...",
    "nombreCompleto": "Juan Pérez", "tipoUsuarioCodigo": "OPERADOR",
    "roles": ["OPERADOR"], "permisos": ["DTE.Emitir","Clientes.Ver", ...]
  },
  "mfaEnrollmentRequired": false
}
```

- **Access token:** ~60 min (`Jwt:ExpiryMinutes`). **Refresh token:** varios días.
- Guarda ambos tokens en almacenamiento **seguro** (`flutter_secure_storage`), nunca en `SharedPreferences` plano.
- `user.permisos` es la **lista de permisos efectivos**: úsala para mostrar/ocultar acciones en la UI
  (botón "Nueva factura" solo si contiene `DTE.Emitir`, etc.). Ver §9.
- `mfaEnrollmentRequired = true` (SuperAdmin sin MFA): redirigir a enrolar 2FA. Para usuarios de empresa normales es `false`.
- Si el usuario tiene MFA activo y no envías `mfaCode`, el login falla pidiendo el código → reintenta con `mfaCode`.

### 4.2 Uso del token

En **toda** llamada autenticada:

```
Authorization: Bearer {accessToken}
Content-Type: application/json
```

### 4.3 Refresh — `POST /api/auth/refresh`  (anónimo)

```jsonc
{ "refreshToken": "b64-opaque-token" }   // → mismo shape que login
```

Flujo recomendado: ante un **401** con token vencido, intentar `refresh` una vez; si también falla,
cerrar sesión y volver al login. Implementar como interceptor en `dio`.

### 4.4 Otros

| Método | Ruta | Uso |
|---|---|---|
| `GET`  | `/api/auth/me` | Re-cargar perfil + permisos (al reabrir la app). |
| `POST` | `/api/auth/logout` | Body `{ refreshToken }`. Revoca el refresh token. |
| `POST` | `/api/auth/change-password` | `{ currentPassword, newPassword }`. |
| `POST` | `/api/auth/mfa/enroll` · `/mfa/confirm` · `/mfa/disable` | TOTP (Google Authenticator). |

---

## 5. Multi-tenant en móvil (IMPORTANTE) — solo usuarios de empresa, sin SuperAdmin

> **Regla de la app:** NeoCloud Mobile **NO maneja SuperAdmin**. La app es exclusivamente para **usuarios
> de empresa**. No hay pantalla de "selección de empresa" ni se envía nunca `?empresaId`.

### 5.1 Cómo queda garantizado el aislamiento

- El `empresaId` de un usuario de empresa **viaja fijo en el JWT**. Todos los endpoints resuelven el tenant
  como `EmpresaId_del_token ?? ?empresaId`, por lo que para un usuario de empresa **el token siempre manda**
  y cualquier `?empresaId` enviado **se ignora**. → Imposible operar sobre otra empresa.
- El `?empresaId` solo se honra cuando el token **no** trae empresa, y eso **únicamente ocurre con
  SuperAdmin** (modo soporte del panel web). Como la app no usa SuperAdmin, ese camino no aplica.
- **Conclusión:** mientras la app use solo cuentas de empresa, el aislamiento multi-tenant es **total y
  automático**, sin lógica extra en el cliente. La app **no debe** construir ni enviar `?empresaId` en
  ninguna llamada.

### 5.2 Manejo de SuperAdmin en el login

El endpoint `/api/auth/login` es compartido con la web y sí permite a un SuperAdmin autenticarse. Si por
error alguien inicia sesión con una cuenta SuperAdmin en la app:

- En la respuesta de login, `user.tipoUsuarioCodigo == "SUPERADMIN"` y `user.empresaId == null`.
- La app **debe detectarlo y bloquear el acceso** con un mensaje tipo *"Esta es una cuenta de administrador.
  Usa el panel web de NeoSTP para tareas administrativas"*, y volver al login. No continuar al dashboard.
- (Refuerzo del backend: un SuperAdmin sin empresa que llame a endpoints de datos sin `?empresaId` recibe
  `400 AUTH_NO_TENANT`; nunca verá datos de ninguna empresa por accidente.)

> Resumen para el dev Flutter: trata `tipoUsuarioCodigo == "SUPERADMIN"` como "no soportado en móvil".
> Los usuarios de empresa son `ADMIN` / `OPERADOR` / `CONTADOR` / `READONLY` (con `empresaId` no nulo); para
> ellos el tenant es implícito y único — nunca pidas ni envíes `empresaId`. Lo que muestra cada pantalla lo
> decide `user.permisos`, no el tipo de usuario.

---

## 6. Configurar la cuenta DTE (certificado + credenciales MH)

La app puede **replicar la configuración fiscal de la web** (igual de completa, presentación más simple).
Base: `/api/dte/configuracion`. Requiere permiso **`DTE.Configurar`**.

### 6.1 Leer configuración — `GET /api/dte/configuracion`

```jsonc
// data: DteConfiguracionDto
{
  "empresaId": 5, "ambienteCodigo": "PRUEBAS",
  "usuarioMh": "06140101010011", "tienePasswordMh": true,
  "tipoEstablecimientoCodigo": "01", "codigoEstablecimientoMh": "0001", "codigoPuntoVentaMh": "P001",
  "tieneCertificado": true, "certificadoNombre": "cert.crt",
  "certificadoHuella": "AB12...", "certificadoVence": "2027-01-01T00:00:00",
  "certificadoTienePassword": true,
  "ultimaPruebaAt": "2026-06-01T10:00:00Z", "ultimaPruebaResultado": "OK",
  "esCompleto": true
}
```

> Los secretos (password MH, blob del certificado) **nunca** se devuelven; solo banderas `tieneX`.

### 6.2 Guardar datos del emisor — `PUT /api/dte/configuracion`

```jsonc
// SaveDteConfiguracionRequest
{
  "ambienteCodigo": "PRUEBAS",            // PRUEBAS | PRODUCCION
  "usuarioMh": "06140101010011",
  "passwordMh": "••••••",                  // null/omitido = no cambiar
  "tipoEstablecimientoCodigo": "01",
  "codigoEstablecimientoMh": "0001",
  "codigoPuntoVentaMh": "P001"
}
```

### 6.3 Subir certificado — `POST /api/dte/configuracion/certificado`

El certificado se envía como **base64 dentro del JSON** (no multipart):

```jsonc
// UploadCertificadoRequest
{
  "nombre": "cert.crt",
  "contenidoBase64": "MIID...",            // bytes del .crt/.cer/.pfx en base64
  "password": "••••••",                    // password del .pfx si aplica
  "emitido": "2024-01-01T00:00:00",
  "vence":   "2027-01-01T00:00:00"
}
```

En Flutter: leer el archivo con `file_picker`, `base64Encode(bytes)`, enviar. El backend cifra el blob con DataProtection.

- `DELETE /api/dte/configuracion/certificado` — elimina el certificado.
- `POST /api/dte/configuracion/probar-conexion` — autentica contra MH con las credenciales guardadas y
  devuelve `{ exitoso, mensaje, codigoHttp, detalle }`. Úsalo en un botón "Probar conexión".

> **Onboarding móvil:** la cuenta está lista para emitir cuando `esCompleto == true` (credenciales MH +
> establecimiento + certificado presentes). Refleja ese estado en un checklist simple.

---

## 7. Emisión de DTE (operación central de la app)

### 7.1 Cómo funciona contra Hacienda (pipeline)

Un DTE pasa por estados; cada transición es un endpoint. El backend firma con el certificado y transmite a MH:

```
BORRADOR ──generar──▶ GENERADO ──validar──▶ VALIDADO ──firmar──▶ FIRMADO ──enviar──▶ PROCESADO
                                                          (firma con cert)   (transmite a MH → sello)
                                                                              └─▶ RECHAZADO / CONTINGENCIA
```

- **PROCESADO** = aceptado por Hacienda (trae `selloRecibido`).
- **RECHAZADO** = MH lo rechazó (ver `respuestaHacienda` / Diagnóstico).
- **CONTINGENCIA** = se emitió sin conexión a MH; el Worker lo retransmite luego.

### 7.2 Crear el documento (BORRADOR)

Hay un endpoint por tipo (todos reciben `CreateDteDocumentoRequest` y crean el borrador):

| Método | Ruta | Tipo DTE |
|---|---|---|
| `POST` | `/api/dte/factura` | 01 Consumidor final |
| `POST` | `/api/dte/credito-fiscal` | 03 Crédito fiscal |
| `POST` | `/api/dte/nota-credito` | 05 Nota de crédito |
| `POST` | `/api/dte/nota-debito` | 06 Nota de débito |
| `POST` | `/api/dte/sujeto-excluido` | 14 Sujeto excluido |
| `POST` | `/api/dte/documentos` | genérico (el tipo va en el body) |

Requiere permiso **`DTE.Emitir`**. Body:

```jsonc
// CreateDteDocumentoRequest
{
  "tipoDteCodigo": "01",
  "sucursalId": null, "puntoVentaId": null,
  "clienteId": 42,                  // O bien receptorManual para cliente no registrado:
  "receptorManual": null,           // { tipoDocumento, numeroDocumento, nrc, nombre, correo, ... }
  "condicionOperacionCodigo": "1",  // 1 Contado · 2 Crédito · 3 Otro
  "formaPagoCodigo": null, "plazoDias": null, "tipoMonedaCodigo": "USD",
  // Para NC/ND: documento relacionado
  "documentoRelacionadoId": null, "numeroDocumentoRelacionado": null, "tipoDteRelacionado": null,
  "observaciones": null,
  // Contingencia (normalmente 0/1 en móvil):
  "modeloFacturacion": 0, "tipoTransmision": 0, "tipoContingenciaCodigo": null, "motivoContingencia": null,
  "lineas": [
    {
      "productoId": 7, "codigo": "PRD-001", "descripcion": "Caja papel bond",
      "unidadMedidaCodigo": "59", "tipoItem": 1,
      "cantidad": 5, "precioUnitario": 45.50, "montoDescuento": 0,
      "clasificacion": "GRAVADA",   // GRAVADA | EXENTA | NO_SUJETA
      "noGravado": false, "observaciones": null
    }
  ]
}
```

La respuesta es `DteDocumentoDto` con el `id` del borrador y los totales ya calculados por el backend
(la app **no** calcula IVA/totales; los muestra).

### 7.3 Procesar el borrador → Hacienda

Encadenar sobre el `id` devuelto:

```
POST /api/dte/documentos/{id}/generar
POST /api/dte/documentos/{id}/validar
POST /api/dte/documentos/{id}/firmar
POST /api/dte/documentos/{id}/enviar    → estado final PROCESADO (con selloRecibido) o RECHAZADO
```

Cada paso devuelve el `DteDocumentoDto` actualizado. Si un paso falla, devuelve error con el código del fallo
(ej. `FIRMA_FAILED`, `HACIENDA_AUTH_FAILED`) y el documento queda en el último estado válido.

### 7.4 Emisión en un solo paso (recomendado para móvil) ✅

Camino recomendado bajo JWT. Una sola llamada ejecuta borrador→generar→validar→firmar→enviar y devuelve el
`DteDocumentoDto` final (idealmente PROCESADO con sello). Permiso `DTE.Emitir`.

| Método | Ruta | Tipo |
|---|---|---|
| `POST` | `/api/dte/emitir` | el tipo va en el body (`tipoDteCodigo`) |
| `POST` | `/api/dte/emitir/factura` | 01 (atajo) |
| `POST` | `/api/dte/emitir/credito-fiscal` | 03 (atajo) |
| `POST` | `/api/dte/emitir/nota-credito` | 05 (atajo) |
| `POST` | `/api/dte/emitir/nota-debito` | 06 (atajo) |
| `POST` | `/api/dte/emitir/sujeto-excluido` | 14 (atajo) |

Body idéntico a `CreateDteDocumentoRequest` (§7.2). Si algún paso del pipeline falla (validación, firma,
MH), devuelve el error de ese paso con su código; el documento queda en el último estado válido y puede
consultarse/retomarse. Este es el camino recomendado para el flujo "vendo → facturo → comparto"; los 5
pasos sueltos (§7.3) siguen disponibles para flujos avanzados.

### 7.5 Compartir / reenviar

| Método | Ruta | Devuelve |
|---|---|---|
| `GET`  | `/api/dte/documentos/{id}/pdf` | `application/pdf` (binario) — guardar y compartir por WhatsApp/correo con `share_plus`. |
| `GET`  | `/api/dte/documentos/{id}/json` | `application/json` (DTE sellado). |
| `POST` | `/api/dte/documentos/{id}/reenviar` | Body `{ destinatario }`. Envía PDF+JSON por correo desde el backend. |

> El PDF ya incluye el **QR de verificación MH**, logo y firma de la empresa (branding configurable). La app no
> necesita generar el QR del documento: lo trae el PDF.

---

## 8. Endpoints por módulo de la app

### 8.1 Dashboard ejecutivo — `GET /api/dashboard/empresa`

Devuelve `DashboardEmpresaDto`: `dteHoy`, `dteMes`, `totalPagarMes`, `procesados`, `rechazados`,
`contingencias`, `pendientes`, `planNombre`, `limiteDteMensual`, `porcentajeUsoDte`, `porEstado[]`,
`porTipo[]`, `tendenciaDiaria[]` (últimos 30 días). Cubre: ventas del día, total facturado, DTE emitidos,
DTE rechazados.

> KPIs financieros adicionales (ganancia, margen, ranking) en `GET /api/profit/*` (requiere módulo NEOPROFIT
> y permiso `Profit.Ver`).

### 8.2 Consulta de DTE — `/api/dte`

| Método | Ruta | Uso |
|---|---|---|
| `GET` | `/api/dte/documentos` | Lista paginada. Query: `page, pageSize, search, tipoDteCodigo, estadoCodigo, desde, hasta`. |
| `GET` | `/api/dte/documentos/{id}` | Detalle completo (`DteDocumentoDto` con `detalles[]`, `jsonDte`, `respuestaHacienda`). |
| `GET` | `/api/dte/documentos/{id}/pdf` · `/json` | Descargas. |

Permiso de lectura: `DTE.Consultar` para lista, detalle, PDF y JSON. La emision sigue protegida por
`DTE.Emitir`.

### 8.3 CRM ligero — Clientes `/api/clientes`

| Método | Ruta | Permiso |
|---|---|---|
| `GET` | `/api/clientes?page&pageSize&search` | `Clientes.Ver` |
| `GET` | `/api/clientes/{id}` | `Clientes.Ver` |
| `POST` | `/api/clientes` (`CreateClienteRequest`) | `Clientes.Crear` |
| `PUT` | `/api/clientes/{id}` (`UpdateClienteRequest`) | `Clientes.Editar` |
| `PATCH` | `/api/clientes/{id}/inactivar` | `Clientes.Editar` |
| `PATCH` | `/api/clientes/{id}/etiqueta` | `Clientes.Editar` — body `{ etiqueta }` (VIP \| FRECUENTE \| vacío). |

`ClienteDto`: `id, tipoDocumentoCodigo, numeroDocumento, nrc, nombre, nombreComercial, tipoContribuyenteCodigo,
esContribuyente, codigoActividad, departamentoCodigo, municipioCodigo, direccion, correo, telefono, estadoCodigo,
etiqueta`. Para "crear cliente rápido" basta `tipoDocumentoCodigo + numeroDocumento + nombre + tipoContribuyenteCodigo`.

> "Llamar/WhatsApp/correo desde la ficha" se resuelve en Flutter (`url_launcher`) con `telefono`/`correo`.

### 8.4 Catálogo de productos — `/api/productos`

Mismo patrón CRUD que clientes: `GET /api/productos` (lista paginada), `GET /{id}`, `POST`, `PUT /{id}`,
`PATCH /{id}/inactivar` (`Productos.Ver/Crear/Editar`). `ProductoDto`: `id, codigoInterno, codigoBarra,
nombre, descripcion, tipoItem (BIEN/SERVICIO), esServicio, unidadMedidaCodigo, precioUnitario, costoUnitario,
aplicaIva, tributoCodigo, estadoCodigo`. El `codigoBarra` habilita el escaneo→buscar producto.

### 8.5 Búsquedas rápidas y cascadas — `/api/lookups`

Pensado para autocompletes y selects (ligero, devuelve `LookupItem[]` = `{ value, label, parent?, meta? }`):

| Ruta | Uso |
|---|---|
| `GET /api/lookups/clientes?search=` | Autocomplete de cliente al facturar. |
| `GET /api/lookups/productos?search=` | Autocomplete de producto (también por código de barras). |
| `GET /api/lookups/sucursales` | Sucursales de la empresa. |
| `GET /api/lookups/departamentos` · `municipios?departamento=` · `distritos?municipio=` | Cascada territorial SV. |
| `GET /api/lookups/catalogo/{codigo}?parent=` | Catálogos MH genéricos (tipos doc, unidades, etc.). |
| `GET /api/lookups/verificar-nit?documento=` | **Verificación NIT/DUI (B-6):** valida el formato salvadoreño y autocompleta desde tus clientes/emisor. Devuelve `{ formatoValido, tipoDocumento, documentoNormalizado, encontradoLocal, nombre, nrc, fuente }`. (La verificación en línea de MH no es pública; queda como hook pluggable.) |

### 8.6 Cobros / Cuentas por cobrar — `/api/cobros` ✅ (B-2 entregado)

Saldos derivados de DTE factura/CCF **a crédito** (condición 2/3) PROCESADO menos pagos CONFIRMADOS.
Lectura `Cobros.Ver`, escritura `Cobros.Gestionar`.

| Método | Ruta | Uso |
|---|---|---|
| `GET` | `/api/cobros/resumen` | `CobranzaResumenDto`: `totalPendiente`, `totalVencido`, `facturasPendientes`, `facturasVencidas`, `clientesConDeuda`. Para las tarjetas del dashboard. |
| `GET` | `/api/cobros/pendientes?page&pageSize&search&clienteId&soloVencidas` | Lista paginada de `CobroPendienteDto` (saldo, vencimiento, `estadoCobro` PENDIENTE/VENCIDO, `diasVencido`). |
| `GET` | `/api/cobros/clientes/{clienteId}` | `SaldoClienteDto`: saldo consolidado + facturas pendientes del cliente. |
| `GET` | `/api/cobros/dte/{dteId}/pagos` | Historial de pagos de una factura. |
| `POST` | `/api/cobros/dte/{dteId}/pagos` | Registrar pago. Body `RegistrarPagoRequest` `{ fecha?, monto, formaPagoCodigo, referencia?, nota?, comprobanteUrl?, pendienteRevision }`. Valida `monto > 0` y `≤ saldo`. |
| `POST` | `/api/cobros/pagos/{pagoId}/confirmar` | Confirma un pago en revisión (recién entonces reduce el saldo). |
| `POST` | `/api/cobros/pagos/{pagoId}/anular` | Anula un pago. |
| `GET` | `/api/cobros/cuentas` | Cuentas/pasarelas de cobro de la empresa (`CuentaCobroDto`). |
| `POST` | `/api/cobros/cuentas` | Crear cuenta de cobro (`Cobros.Gestionar`). Body `CrearCuentaCobroRequest`. |
| `PUT` | `/api/cobros/cuentas/{id}` | Actualizar cuenta (`Cobros.Gestionar`). |
| `POST` | `/api/cobros/cuentas/{id}/inactivar` | Inactivar cuenta (`Cobros.Gestionar`). |
| `POST` | `/api/cobros/qr` | **QR de cobro.** Body `{ dteDocumentoId?, cuentaCobroId?, monto?, referencia? }` → `CobroQrDto`. |

**QR de cobro** (`POST /api/cobros/qr`): genera un código QR de pago para compartir con el cliente. Si pasas
`dteDocumentoId`, el **monto = saldo** de la factura y la **referencia = número de control**; si no, indica
`monto`. Usa la cuenta indicada o la primera activa. Respuesta `CobroQrDto`: `{ monto, referencia,
cuentaNombre, payload, qrPngBase64 }` — `qrPngBase64` es la imagen PNG lista para mostrar/compartir
(`share_plus`), y `payload` es la URL de pago (si la cuenta tiene `urlPago` con `{monto}`/`{referencia}`)
o un texto de transferencia. El panel web administra las cuentas; la app solo genera y comparte el QR.

`CobroPendienteDto`: `dteDocumentoId, tipoDteCodigo, numeroControl, fechaEmision, vencimiento, clienteId,
clienteNombre, total, pagado, saldo, estadoCobro, diasVencido`.

> Reglas: solo facturas (01) y CCF (03) **a crédito** generan cuenta por cobrar; saldo = `total − Σ pagos
> confirmados`; vencida si `saldo > 0` y `vencimiento < hoy`. Un pago `pendienteRevision` NO reduce el saldo
> hasta confirmarse. Los recordatorios (WhatsApp/correo) se disparan desde Flutter con los datos del cliente;
> reenviar la factura por correo: `POST /api/dte/documentos/{id}/reenviar`.

### 8.7 NeoScanAI — `/api/scanai/documentos` ✅ (B-3 entregado)

Bandeja de documentos capturados (foto/PDF) → extracción → revisión/corrección → conversión a
**gasto / compra / DTE recibido** (alimenta NeoProfit). Requiere el módulo **NEOSCANAI**.
Lectura/captura `ScanAI.Ver`; confirmaciones `ScanAI.Confirmar`.

| Método | Ruta | Uso |
|---|---|---|
| `GET` | `/api/scanai/documentos?page&pageSize&search&estadoCodigo&tipoClasificacion` | Bandeja paginada (`ScanDocumentoDto`). |
| `GET` | `/api/scanai/documentos/{id}` | Detalle (campos extraídos + estado). |
| `GET` | `/api/scanai/documentos/{id}/archivo` | Descarga la imagen/PDF capturado. |
| `POST` | `/api/scanai/documentos` | Subir captura. Body `{ nombre, contentType, contenidoBase64, origen }`. Ejecuta la extracción y deja el doc en la bandeja. |
| `PUT` | `/api/scanai/documentos/{id}/campos` | Corregir campos (`CorregirScanRequest`). |
| `POST` | `/api/scanai/documentos/{id}/resultado` | Cargar resultado de un proveedor externo de OCR/IA. |
| `POST` | `/api/scanai/documentos/{id}/registrar-gasto` | Confirmar como gasto (body `CreateProfitGastoRequest`). |
| `POST` | `/api/scanai/documentos/{id}/registrar-compra` | Confirmar como compra (body `CreateProfitCompraRequest`). |
| `POST` | `/api/scanai/documentos/{id}/registrar-dte-recibido` | Confirmar como DTE recibido de proveedor. |
| `POST` | `/api/scanai/documentos/{id}/rechazar` | Rechazar (body `{ motivo }`). |

`ScanDocumentoDto`: `id, estadoCodigo, tipoClasificacion, origen, archivoNombre, tieneArchivo,
emisorNombre, emisorNit, emisorNrc, fecha, tipoDocumento, numeroControl, selloRecibido, subtotal,
iva, total, confianza, notas, profitGastoId, profitCompraId, dteRecibidoId`.

**Estados:** `RECIBIDO → PROCESANDO → (PROCESADO | REQUIERE_REVISION) → (CONFIRMADO | RECHAZADO)`.

> **Importante (extracción OCR/IA):** el backend funciona con `Scan:Provider=Mock` por defecto:
> guarda la captura y deja el documento en `REQUIERE_REVISION` con `confianza = 0`, para que el usuario
> capture/corrija los campos manualmente en la app. También existe proveedor real Gemini
> (`Scan:Provider=Gemini` + `Scan:Gemini:ApiKey`) sin cambiar el contrato: cuando se active, los campos
> vendrán precargados con su `confianza`. La app debe funcionar igual en ambos casos (mostrar lo que
> venga y permitir corregir antes de confirmar). Para demo productiva, NeoScan valida MIME permitidos
> (`image/jpeg`, `image/png`, `application/pdf` por defecto), usa `Scan:ConfianzaMinimaProcesado`
> para decidir `PROCESADO` vs `REQUIERE_REVISION` y envia la API key de Gemini por header
> `x-goog-api-key`. **Limite mensual:** si la empresa supera el cupo configurado
> (`Scan:LimiteMensual`, 0 = sin limite), `POST /documentos` devuelve `409` con `LIMIT_EXCEEDED`.

### 8.8 Alertas y notificaciones push — `/api/alertas` ✅ (B-4 entregado)

Centro de alertas (DTE rechazado, certificado por vencer, factura vencida) + registro de dispositivos
(FCM) + preferencias. Para todo usuario de empresa autenticado (sus propias alertas + las de la empresa).

| Método | Ruta | Uso |
|---|---|---|
| `GET` | `/api/alertas?page&pageSize&estadoCodigo&tipoCodigo` | Centro de notificaciones (`AlertaDto`). Por defecto excluye RESUELTAS. |
| `GET` | `/api/alertas/resumen` | `{ pendientes, criticas, advertencias }` — para el **badge** del dashboard. |
| `POST` | `/api/alertas/{id}/leer` · `/{id}/resolver` | Marcar leída / resuelta. |
| `POST` | `/api/alertas/leer-todas` | Marcar todas como leídas. |
| `POST` | `/api/alertas/generar` | Recalcula alertas desde datos reales (idempotente, dedupe por clave). Devuelve `{ creadas }`. |
| `POST` | `/api/alertas/dispositivos` | Registrar token FCM: `{ token, plataforma }` (ANDROID/IOS/WEB). |
| `POST` | `/api/alertas/dispositivos/eliminar` | Body `{ token }` — baja del dispositivo. |
| `GET` / `PUT` | `/api/alertas/preferencias` | Canal (PUSH/CORREO/AMBOS), no molestar, horario. |

`AlertaDto`: `id, tipoCodigo, severidad (INFO/ADVERTENCIA/CRITICA), titulo, mensaje, entidadTipo, entidadId,
estadoCodigo (PENDIENTE/LEIDA/RESUELTA), createdAt`. Usa `entidadTipo`+`entidadId` para el **deep-link**
("abrir documento desde la alerta").

> **Flujo en la app:** al iniciar sesión, registra el token FCM (`POST /dispositivos`); muestra el badge con
> `/resumen`; lista en el centro de notificaciones; al tocar una alerta, navega a la entidad y marca leída/resuelta.
> **Push:** hoy el backend usa un **sender mock** (`Push:Provider=Mock`, registra en logs); el envío real por
> **FCM es pluggable** sin cambiar el contrato. La generación de alertas corre **automáticamente en el Worker**
> (`AlertaGeneracionWorker`, cada `Worker:GeneracionAlertas:IntervaloMinutos`, 60 por defecto) para todas las
> empresas activas; además puedes dispararla manualmente con `POST /generar`.

### 8.9 Eventos DTE — `/api/dte/eventos`

`GET` lista/detalle/json/pdf; `POST invalidacion | contingencia | retorno | operaciones-especiales`. La
invalidación (anulación de un DTE procesado) y la contingencia se exponen aquí y en `/api/dte/evento/*`.

---

## 9. Permisos y control de acceso en la UI

- El backend protege cada endpoint con `[RequirePermiso("...")]`; el SuperAdmin siempre pasa.
- La app debe leer `user.permisos` (del login o `/me`) y **deshabilitar/ocultar** acciones sin permiso para
  evitar 403. Mapa de permisos relevantes para móvil:

| Permiso | Habilita |
|---|---|
| `DTE.Emitir` | Crear y procesar DTE |
| `DTE.Consultar` | Ver/descargar PDF/JSON |
| `DTE.Reenviar` | Reenviar por correo |
| `DTE.Invalidar` | Anular DTE |
| `DTE.Configurar` | Configurar emisor/certificado |
| `Clientes.Ver/Crear/Editar` | CRM (incluye etiqueta) |
| `Productos.Ver/Crear/Editar` | Catálogo |
| `Cobros.Ver` / `Cobros.Gestionar` | Cobros/CxC: ver saldos/QR · registrar pagos y cuentas |
| `ScanAI.Ver` / `ScanAI.Confirmar` | NeoScan: bandeja/captura · confirmar a gasto/compra/DTE recibido |
| `Profit.Ver` / `Profit.Gestionar` | KPIs financieros / gastos y compras (módulo NEOPROFIT) |
| _(sin permiso especial)_ | Alertas y dispositivos: cualquier usuario de empresa gestiona los suyos |

- **Licenciamiento por módulo:** algunos endpoints exigen además un módulo activo en el plan
  (`[RequireModule("NEOPROFIT")]`, `NEOSCANAI`, etc.) → HTTP 402/403 si el plan no lo incluye. La app debe
  degradar con elegancia (ocultar la pestaña Profit/Scan).

---

## 10. Códigos de error y manejo HTTP

`errors[0]` trae un **código estable**; el HTTP status lo mapea el backend (`ApiControllerBase`):

| HTTP | Códigos típicos | Acción en la app |
|---|---|---|
| 400 | `VALIDATION`, `AUTH_NO_TENANT` | Mostrar mensaje/validaciones de campo. |
| 401 | `AUTH_INVALID_CREDENTIALS`, `AUTH_USER_INACTIVE`, `AUTH_USER_LOCKED`, `AUTH_REFRESH_INVALID`, `PWD_INVALID` | Login/refresh; si refresh falla, cerrar sesión. |
| 402 | `LICENSE_INVALID` | Plan no cubre el módulo → upsell/ocultar. |
| 403 | (permiso/módulo) | Ocultar acción. |
| 404 | `DTE_NOT_FOUND`, `CLIENTE_NOT_FOUND`, `PRODUCTO_NOT_FOUND`, `CONFIG_NOT_FOUND`, `PAGO_NOT_FOUND`, `CUENTA_NOT_FOUND`, `SCAN_NOT_FOUND`, `ALERTA_NOT_FOUND`, `DISPOSITIVO_NOT_FOUND`, `GASTO_NOT_FOUND`, `COMPRA_NOT_FOUND` | "No encontrado". |
| 409 | `INVALID_STATE`, `*_DUPLICATE`, `LIMIT_EXCEEDED` | Conflicto (transición inválida, duplicado, cupo de escaneos/recurso superado). |
| 429 | `RATE_LIMIT_EXCEEDED` | Cuota de API por empresa/usuario. Respetar `Retry-After`; backoff. |
| 502 | `FIRMA_FAILED`, `HACIENDA_AUTH_FAILED`, `EMAIL_FAILED` | Falla de firma/MH/correo → reintentar/avisar. |

Siempre registra `traceId` en logs locales para soporte.

---

## 11. Recomendaciones de implementación Flutter

- **HTTP:** `dio` con interceptores (auth header, refresh en 401, logging, `traceId`).
- **Estado:** `riverpod`/`bloc`. Modelos con `freezed`+`json_serializable` mapeando 1:1 los DTO de §7-8.
- **Almacenamiento seguro:** `flutter_secure_storage` para tokens.
- **Escaneo:** `mobile_scanner` (QR + códigos de barra) → resuelve a `lookups/productos?search={codigo}`.
- **Compartir:** `share_plus` con el PDF descargado; `url_launcher` para llamar/WhatsApp/correo.
- **Offline mínimo:** cachear catálogo de productos/clientes (lookups) para facturar con conexión intermitente;
  la **emisión siempre requiere conexión** (firma+MH en backend). La contingencia es del backend, no del cliente.
- **Paginación:** scroll infinito usando `page`/`totalPages`.
- **Versionado:** la app debe tolerar campos nuevos en los DTO (no romper si aparecen propiedades).

---

## 12. Referencia rápida de endpoints

```
# Auth
POST   /api/auth/login | refresh | logout | change-password
GET    /api/auth/me
POST   /api/auth/mfa/enroll | mfa/confirm | mfa/disable

# Config DTE (certificado + credenciales MH)
GET    /api/dte/configuracion
PUT    /api/dte/configuracion
POST   /api/dte/configuracion/certificado      DELETE /api/dte/configuracion/certificado
POST   /api/dte/configuracion/probar-conexion

# Emisión
POST   /api/dte/factura | credito-fiscal | nota-credito | nota-debito | sujeto-excluido | documentos
POST   /api/dte/documentos/{id}/generar | validar | firmar | enviar | invalidar
POST   /api/dte/evento/contingencia | invalidacion | operaciones-especiales | retorno
GET    /api/dte/documentos | documentos/{id} | documentos/{id}/pdf | documentos/{id}/json
POST   /api/dte/documentos/{id}/reenviar
POST   /api/dte/emitir   |   emitir/factura | emitir/credito-fiscal | emitir/nota-credito | emitir/nota-debito | emitir/sujeto-excluido

# Eventos DTE
GET    /api/dte/eventos | {id} | {id}/json | {id}/pdf
POST   /api/dte/eventos/invalidacion | contingencia | retorno | operaciones-especiales

# Maestros
GET/POST/PUT/PATCH  /api/clientes ...
GET/POST/PUT/PATCH  /api/productos ...
GET    /api/lookups/clientes | productos | sucursales | departamentos | municipios | distritos | catalogo/{codigo} | verificar-nit
PATCH  /api/clientes/{id}/etiqueta                            # VIP | FRECUENTE | vacio (B-2 follow-up)

# Cobros / CxC (Cobros.Ver / Cobros.Gestionar)
GET    /api/cobros/resumen | pendientes | clientes/{clienteId} | dte/{dteId}/pagos | cuentas
POST   /api/cobros/dte/{dteId}/pagos | pagos/{pagoId}/confirmar | pagos/{pagoId}/anular
POST   /api/cobros/cuentas | cuentas/{id}/inactivar | qr        # QR de cobro (B-5)
PUT    /api/cobros/cuentas/{id}

# NeoScanAI (modulo NEOSCANAI; ScanAI.Ver / ScanAI.Confirmar)
GET    /api/scanai/documentos | {id} | {id}/archivo
POST   /api/scanai/documentos                                   # subir captura (base64)
PUT    /api/scanai/documentos/{id}/campos                       # corregir
POST   /api/scanai/documentos/{id}/resultado                    # resultado externo OCR
POST   /api/scanai/documentos/{id}/registrar-gasto | registrar-compra | registrar-dte-recibido | rechazar

# Alertas / push (usuario de empresa autenticado)
GET    /api/alertas | resumen | preferencias
POST   /api/alertas/{id}/leer | {id}/resolver | leer-todas | generar
POST   /api/alertas/dispositivos | dispositivos/eliminar
PUT    /api/alertas/preferencias

# Métricas
GET    /api/dashboard/empresa
GET    /api/profit/dashboard | productos | clientes | sucursales | tendencia   (módulo NEOPROFIT)

# Salud
GET    /health
```

> **OpenAPI:** el backend publica el esquema en `/openapi/v1.json` (incluye la API pública NeoConnect v1).
> Útil como referencia, pero NeoCloud Mobile usa los endpoints JWT documentados aquí, no la API por key.
