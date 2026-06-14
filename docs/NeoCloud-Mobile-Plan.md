# NeoCloud Mobile — Plan de trabajo, sprints y estado del backend

Acompaña a `NeoCloud-Mobile-API.md`. Resume **qué cubre la API** y el **plan de sprints** para construir la app
Flutter (Android tablet/celular).

> **Estado del backend: COMPLETO.** Todas las brechas (B-1…B-6) y los pendientes menores ya están
> entregados, probados (370 tests verdes) y desplegados en `main`. **No queda backend por desarrollar** para el
> alcance de la propuesta móvil; lo único pendiente son **integraciones externas** que dependen de credenciales/
> servicios de terceros. NeoScan ya cuenta con proveedor Gemini configurable (`Scan:Provider=Gemini`)
> y `Mock` por defecto; FCM real y NIT en línea de MH siguen dependiendo de credenciales/servicio externo.
>
> En consecuencia, **el plan de sprints de abajo es el roadmap del cliente Flutter**: cada sprint consume
> endpoints que ya existen. El dev Flutter trabaja contra el contrato de `NeoCloud-Mobile-API.md` y el explorador
> **Scalar** (`/scalar/v1`).

---

## 1. Cobertura: propuesta vs. API

Leyenda: ✅ disponible en el backend · 📱 trabajo de cliente Flutter · 🔵 integración externa pendiente (hook listo).

| # | Función de la app (propuesta) | Estado | Endpoints |
|---|---|---|---|
| 3.1 | Dashboard: ventas día, total facturado, DTE emitidos/rechazados | ✅ | `GET /api/dashboard/empresa` |
| 3.1 | Dashboard: facturas pendientes de cobro, clientes con deuda | ✅ | `GET /api/cobros/resumen` |
| 3.1 | Alertas fiscales / de cobro | ✅ | `GET /api/alertas/resumen` |
| 3.2 | Emisión rápida DTE (01/03/05/06/14) | ✅ | `POST /api/dte/emitir` (un paso) + atajos por tipo |
| 3.2 | Buscar/crear cliente, agregar productos, descuento, vista previa | ✅ | `lookups`, `clientes`, `productos`; totales los calcula el backend |
| 3.2 | Escanear código de barras → producto | ✅ 📱 | `mobile_scanner` → `GET /api/lookups/productos?search={codigo}` |
| 3.2 | Consultar estado MH, compartir PDF/WhatsApp/correo | ✅ 📱 | `documentos/{id}` + `/pdf` + `share_plus`; `reenviar` |
| 3.3 | CRM ligero: ficha, crear/editar cliente | ✅ | `clientes` CRUD |
| 3.3 | Saldo pendiente, historial de cobros | ✅ | `GET /api/cobros/clientes/{id}` · `dte/{id}/pagos` |
| 3.3 | Importar de contactos, llamar, WhatsApp, correo | ✅ 📱 | `url_launcher` (datos del `ClienteDto`) |
| 3.3 | Verificación de NIT (formato + autocompletado local) | ✅ | `GET /api/lookups/verificar-nit` |
| 3.3 | Etiquetas (VIP/frecuente; moroso derivado) | ✅ | `PATCH /api/clientes/{id}/etiqueta` |
| 3.4 | Catálogo: buscar, escanear, crear rápido, activar/inactivar | ✅ 📱 | `productos` CRUD + `lookups/productos` |
| 3.4 | Existencia básica / inventario | — | sin módulo de inventario (fuera de alcance inicial) |
| 3.5 | NeoScan: bandeja, capturar, revisar/corregir, clasificar | ✅ | `/api/scanai/documentos/*` |
| 3.5 | NeoScan: extracción OCR/IA automática | ✅/🔵 | Mock por defecto + Gemini configurable; hardening productivo en `Plan-Hallazgos-Bugs-Demo.md` |
| 3.6 | Centro de notificaciones, cert por vencer, factura vencida, DTE rechazado | ✅ | `/api/alertas/*` (+ generación automática en el Worker) |
| 3.6 | Entrega push real | 🔵 | mock pluggable hoy; FCM real por configurar (service account) |
| 3.7 | Cobros: pendientes/vencidas, registrar pago, adjuntar | ✅ | `GET /api/cobros/pendientes` · `POST /api/cobros/dte/{id}/pagos` |
| 3.7 | QR de pago / enlace de pago | ✅ | `POST /api/cobros/qr` |
| 3.8 | Consulta DTE: emitidos/recibidos, estado MH, PDF/JSON, reenviar | ✅ | emitidos; **recibidos** vía NeoScan (`registrar-dte-recibido`) |
| 4.1 | QR por factura (verificación MH) | ✅ | embebido en el PDF generado |
| 4.2 | Código de barras por producto | ✅ 📱 | `codigoBarra` en `ProductoDto`; generar etiqueta = web |
| 4.3 | QR de cobro (Pagadito/ACH/transferencia) | ✅ | `/api/cobros/cuentas` + `/api/cobros/qr` |
| — | Login, tenant, perfil, permisos | ✅ | `auth/*`, `me` |
| — | Configurar emisor + certificado + credenciales MH | ✅ | `dte/configuracion/*` |

**Conclusión:** **todo el alcance funcional de la propuesta está cubierto por la API.** NeoScan puede
operar con captura manual (`Mock`) o Gemini por configuración, sin cambiar el contrato. Las integraciones
externas que aun dependen de insumos son FCM real, NIT en línea de MH y el endurecimiento productivo de
NeoScan antes de venderlo como OCR real de demo.

---

## 2. Backend — entregado (B-1…B-6) ✅

Todo lo siguiente está **implementado, probado y en `main`**:

| ID | Entrega | Detalle |
|---|---|---|
| **B-1** ✅ | **Emisión en un paso** | `POST /api/dte/emitir` (+ atajos por tipo); orquesta borrador→generar→validar→firmar→enviar vía `IConnectDteService`. Tests `DteControllerEmitirTests`. |
| **B-2** ✅ | **Cobros / Cuentas por cobrar** | `PagoCliente` (`Cobros_Pagos`), `CobranzaCalculator`, `ICobranzaService`, `/api/cobros/*` (resumen, pendientes, saldo cliente, registrar/confirmar/anular pago), permisos `Cobros.Ver`/`Gestionar`. **Etiquetas de cliente** (`PATCH /api/clientes/{id}/etiqueta`). Migración `B2_CobranzaPagosCliente`. Tests. |
| **B-3** ✅ | **NeoScanAI** | `ScanDocumento` (`Scan_Documentos`) + `DteDocumentoRecibido` (`Dte_DocumentosRecibidos`), `IScanService` (bandeja, subir, corregir, registrar-gasto/compra/dte-recibido → alimenta NeoProfit, rechazar), `IScanExtractionService` mock pluggable, `/api/scanai/documentos/*`, módulo `NEOSCANAI`. **Límite mensual** (`Scan:LimiteMensual`). Migración `B3_NeoScanAI`. Tests. |
| **B-4** ✅ | **Alertas y push** | `Alerta`/`DispositivoNotificacion`/`PreferenciaNotificacion`, `IAlertaService`, `IAlertaGeneracionService` (deriva de DTE rechazado, cert por vencer, facturas vencidas), `IPushSender` mock pluggable, `/api/alertas/*`. **Job del Worker** (`AlertaGeneracionWorker`). Migración `B4_AlertasNotificaciones`. Tests. |
| **B-5** ✅ | **QR / enlace de cobro** | `CuentaCobro` (`Cobros_CuentasCobro`) + `ICobroQrService` (CRUD cuentas + QR con QRCoder; monto desde saldo o fijo; URL de pago o texto de transferencia); `/api/cobros/cuentas` y `/api/cobros/qr`. Migración `B5_CuentasCobroQr`. Tests. |
| **B-6** ✅ | **Verificación de NIT/DUI** | `GET /api/lookups/verificar-nit` (formato salvadoreño + autocompletado local). `INitVerificationService`. Tests. |
| **B-7** ✅ | **DTE recibidos** | Cubierto por B-3: `POST /api/scanai/documentos/{id}/registrar-dte-recibido` + entidad `Dte_DocumentosRecibidos`. |

### Pendientes — solo integraciones externas (no bloquean la app; hook listo)

| Pendiente | Estado | Nota |
|---|---|---|
| ✅/🔵 **OCR/IA real** de NeoScan | Mock por defecto; Gemini configurable (`Scan:Provider=Gemini`) | La app funciona con captura manual o campos precargados. Pendiente hardening productivo: asincrono, umbral de confianza, MIME whitelist y API key por header. |
| 🔵 **FCM real** para push | Mock activo (`Push:Provider=Mock`) | Requiere service account de Firebase. El centro de alertas funciona vía polling sin push real. |
| 🔵 **NIT en línea de MH** | Hook `Fuente=MH` listo | El servicio público de MH no está disponible; hoy se valida formato + datos locales. |
| ⚪ **UIs web** de Cobros/Scan/Alertas | Opcional | El consumidor principal es la app; la web puede agregarse después. |

---

## 3. Plan de sprints (cliente Flutter)

Sprints de ~2 semanas, **solo Flutter** — todos los endpoints que consumen **ya están disponibles** en el
backend. El orden busca valor temprano y bajo riesgo; el dev puede reordenar porque no hay dependencias de
backend pendientes.

### Sprint 0 — Fundaciones
- Proyecto Flutter, arquitectura (capas, `dio`, `riverpod`/`bloc`, `freezed`).
- `ApiClient` con `ApiResponse<T>`, errores tipados, interceptor de **JWT + refresh (401)**.
- Almacenamiento seguro de tokens; pantalla **Login** + `me`; carga de **permisos**.
- **Tenant implícito (single-empresa):** la app es **solo para usuarios de empresa**; `empresaId` sale del
  token y **nunca** se envía `?empresaId`. Si el login devuelve `SUPERADMIN`, bloquear con "usa el panel web"
  (sin pantalla de selección de empresa). Ver §5 de la guía de API.
- **Health check** en splash; tema/diseño base; navegación por pestañas (Inicio, Facturar, Clientes, Escanear, Más).
- Backend: ✅ `auth/*`, `me`, `health`.

### Sprint 1 — Dashboard + Consulta de DTE (read-only)
- **Inicio/Dashboard:** KPIs (`/api/dashboard/empresa`), cartera de cobros (`/api/cobros/resumen`), badge de
  alertas (`/api/alertas/resumen`).
- **Consulta DTE:** lista con filtros (tipo, estado, fechas, search), detalle, **ver/compartir PDF**, JSON, reenviar.
- Backend: ✅ `dashboard`, `cobros/resumen`, `alertas/resumen`, `dte/documentos*`.

### Sprint 2 — Emisión rápida de DTE
- **Facturar:** buscar/crear cliente, líneas (lookups/escaneo de código de barras), descuento, **vista previa**
  (totales del backend), **emitir en un paso** (`POST /api/dte/emitir`), estado MH, **compartir PDF**.
- Tipos 01 y 03 primero; luego 05/06/14. Manejo de rechazo/contingencia.
- Backend: ✅ `dte/emitir`, `lookups`, `clientes`, `productos`.

### Sprint 3 — CRM ligero + Catálogo
- **Clientes:** lista/búsqueda, ficha, crear/editar, inactivar, **etiqueta** (`PATCH .../etiqueta`),
  **verificar NIT** (`/api/lookups/verificar-nit`), llamar/WhatsApp/correo, importar de contactos, facturar desde la ficha.
- **Catálogo:** lista/búsqueda, escanear código de barras, crear rápido, activar/inactivar.
- Backend: ✅ `clientes`, `productos`, `lookups`.

### Sprint 4 — Configuración de cuenta DTE + perfil
- **Configuración fiscal** replicada de la web: emisor, ambiente, credenciales MH, **subir certificado**
  (base64), probar conexión, estado de "cuenta lista".
- **Perfil:** datos del usuario, cambio de contraseña, MFA, cerrar sesión.
- Backend: ✅ `dte/configuracion/*`, `auth/*`.

### Sprint 5 — Cobros / Cuentas por cobrar
- **Cobros:** pendientes/vencidas (filtros), saldo por cliente, **registrar pago** (adjuntar comprobante),
  recordatorio por WhatsApp/correo (📱), tarjetas de cartera en el dashboard.
- Backend: ✅ `cobros/*`.

### Sprint 6 — QR de cobro
- **Configurar cuentas de cobro** (`/api/cobros/cuentas`) y **generar QR de pago** (`/api/cobros/qr`) por factura
  o monto; copiar enlace, compartir (📱).
- Backend: ✅ `cobros/cuentas`, `cobros/qr`.

### Sprint 7 — NeoScan / respaldo de facturas
- **Escanear:** capturar factura/PDF (`POST /api/scanai/documentos`), revisar/corregir campos, **registrar como
  gasto/compra/DTE recibido**, rechazar; bandeja con filtros.
- Backend: ✅ `scanai/documentos/*`. Funciona con extracción mock por defecto o Gemini configurable; la app siempre debe permitir corrección antes de confirmar.

### Sprint 8 — Alertas y notificaciones
- **Alertas:** centro de notificaciones (`/api/alertas`), badge, marcar leída/resuelta, registrar **token FCM**
  (`/api/alertas/dispositivos`), preferencias; deep-link a la entidad afectada.
- Backend: ✅ `alertas/*` (generación automática en el Worker). (🔵 push real cuando se configure FCM.)

### Sprint 9 — Pulido, rendimiento y publicación
- Caché offline de catálogos, scroll infinito, estados vacíos/errores, accesibilidad, modo tablet.
- Pruebas en dispositivos, hardening, **publicación en Play Store** (interna → producción).

---

## 4. Checklist "la API cumple" — ✅ completo

- [x] **JWT** login/refresh/logout + `me` con `permisos`.
- [x] **JSON** en todo (`ApiResponse<T>`) + descargas binarias (PDF/JSON/QR).
- [x] **Multi-tenant** por token (la app no usa SuperAdmin ni envía `?empresaId`).
- [x] **Configuración DTE + certificado** (base64) + probar conexión.
- [x] **Emisión y transmisión a MH** server-side (incl. **emisión en un paso** B-1).
- [x] **Consulta** paginada + filtros; PDF con QR/branding; reenvío por correo.
- [x] **CRM y catálogo** CRUD + lookups/escaneo + **etiquetas** + **verificación NIT**.
- [x] **Cobros/CxC** (B-2) y **QR de cobro** (B-5).
- [x] **NeoScan** bandeja + conversión a gasto/compra/**DTE recibido** (B-3).
- [x] **Alertas** + dispositivos FCM + generación automática en el Worker (B-4).
- [x] **Rate limiting** y cuotas (`429` + `Retry-After`).
- [ ] 🔵 Hardening NeoScan real / FCM real / NIT MH en línea — integraciones externas (hook listo).

> La app puede arrancar **hoy** con **cualquier** sprint: no hay dependencias de backend pendientes.

---

## 5. Notas de coordinación FE ↔ BE

- **Contrato estable:** los DTO de `NeoCloud-Mobile-API.md` son el contrato. Cambios incompatibles se versionan/avisan.
- **Explorar/probar:** Scalar en `/scalar/v1` (botón *Authorize* con el JWT); spec en `/openapi/v1.json`.
- **Ambientes:** el dev Flutter trabaja contra staging (apitest de MH). Credenciales y certificado de prueba los provee NeoSTP.
- **Soporte:** el `traceId` de cada respuesta correlaciona con los logs del backend.
- **Seguridad:** tokens en `flutter_secure_storage`; nunca loguear tokens ni el blob del certificado; HTTPS en staging/prod.
- **Multi-tenant:** la app **no maneja SuperAdmin** y **nunca envía `?empresaId`**; el tenant es el del token
  (único e implícito), así que el aislamiento es automático. SuperAdmin se bloquea en el login. Ver §5 de la guía de API.
