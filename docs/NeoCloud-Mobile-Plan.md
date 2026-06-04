# NeoCloud Mobile — Plan de trabajo, sprints y análisis de brechas

Acompaña a `NeoCloud-Mobile-API.md`. Define **qué ya cubre la API actual**, **qué falta construir en el
backend** y un **plan de sprints** para Flutter (Android tablet/celular) y backend.

> Backend lo mantenemos nosotros (NeoSTP). El desarrollador Flutter trabaja contra los endpoints del
> documento de API; las brechas marcadas `B-x` las entregamos nosotros en el backend antes/junto a cada fase.

---

## 1. Cobertura: propuesta vs. API actual

Leyenda: ✅ ya existe · 🟡 existe parcial / necesita ajuste · 🔴 brecha (backend nuevo) · 📱 solo cliente Flutter.

| # | Función de la app (propuesta) | Estado API | Endpoints / brecha |
|---|---|---|---|
| 3.1 | Dashboard: ventas día, total facturado, DTE emitidos/rechazados | ✅ | `GET /api/dashboard/empresa` |
| 3.1 | Dashboard: facturas pendientes de cobro, clientes con deuda | 🔴 | **B-2 Cobros/CxC** |
| 3.1 | Alertas fiscales / de cobro | 🔴 | **B-4 Alertas/Notif.** |
| 3.2 | Emisión rápida DTE (01/03/05/06/14) | ✅ | `POST /api/dte/{tipo}` + pipeline; **B-1** simplifica a 1 paso |
| 3.2 | Buscar/crear cliente, agregar productos, descuento, vista previa | ✅ | `lookups`, `clientes`, `productos`, totales los calcula el backend |
| 3.2 | Escanear código de barras → producto | ✅ 📱 | `mobile_scanner` → `GET /api/lookups/productos?search={codigo}` |
| 3.2 | Consultar estado MH, compartir PDF/WhatsApp/correo | ✅ 📱 | `documentos/{id}` + `/pdf` + `share_plus`; `reenviar` |
| 3.3 | CRM ligero: ficha, crear/editar cliente | ✅ | `clientes` CRUD |
| 3.3 | Saldo pendiente, crédito disponible, historial de cobros | 🔴 | **B-2 Cobros/CxC** |
| 3.3 | Importar desde contactos, llamar, WhatsApp, correo | ✅ 📱 | `url_launcher` (datos del `ClienteDto`) |
| 3.3 | Verificación de NIT contra MH | 🔴 | **B-6 Verificación NIT** |
| 3.3 | Etiquetas (frecuente/moroso/VIP), notas, recordatorios | 🟡/🔴 | notas internas existen en DTE; etiquetas/recordatorios de cliente = **B-2** |
| 3.4 | Catálogo: buscar, escanear, crear rápido, activar/inactivar | ✅ 📱 | `productos` CRUD + `lookups/productos` |
| 3.4 | Existencia básica / inventario | 🔴 | sin módulo de inventario (fuera de alcance inicial) |
| 3.5 | NeoScan: escanear factura/PDF/QR, OCR, clasificar | 🔴 | **B-3 NeoScan/OCR** (Sprint 23 backend pendiente) |
| 3.6 | Alertas push, centro de notificaciones, F-07, cert por vencer | 🔴 | **B-4 Alertas/Notif.** (cert por vencer ya está en config) |
| 3.7 | Cobros: pendientes/vencidas, recordatorio, registrar pago, adjuntar | 🔴 | **B-2 Cobros/CxC** |
| 3.7 | QR de pago / enlace de pago | 🔴 | **B-5 QR de cobro** |
| 3.8 | Consulta DTE: emitidos/recibidos, estado MH, PDF/JSON, reenviar | ✅ 🟡 | emitidos ✅; **recibidos** depende de **B-3** |
| 4.1 | QR por factura (verificación MH) | ✅ | ya embebido en el PDF generado |
| 4.2 | Código de barras por producto | ✅ 📱 | `codigoBarra` en `ProductoDto`; generar etiqueta = web |
| 4.3 | QR de cobro (Pagadito/ACH/transferencia) | 🔴 | **B-5 QR de cobro** |
| — | Login, tenant, perfil, permisos | ✅ | `auth/*`, `me` |
| — | Configurar emisor + certificado + credenciales MH | ✅ | `dte/configuracion/*` |

**Conclusión:** el **núcleo operativo (emisión, consulta, CRM, catálogo, configuración, compartir)** ya está
soportado por la API actual. Las brechas son módulos de negocio nuevos: **Cobros/CxC, NeoScan/OCR,
Alertas/Push, QR de cobro, verificación NIT** y la conveniencia **emisión en un paso**.

---

## 2. Brechas de backend (las entregamos nosotros)

| ID | Brecha | Alcance backend | Prioridad |
|---|---|---|---|
| **B-1** ✅ | **Emisión en un paso** (entregado) | `POST /api/dte/emitir` (+ atajos `/emitir/factura|credito-fiscal|nota-credito|nota-debito|sujeto-excluido`), JWT, permiso `DTE.Emitir`. Orquesta borrador→generar→validar→firmar→enviar vía `IConnectDteService.EmitirAsync`. Tests `DteControllerEmitirTests`. | ✅ Listo |
| **B-2** | **Cobros / Cuentas por cobrar** | Saldo por cliente y por factura, estados pendiente/vencida, registrar pago básico, adjuntar comprobante, etiquetas de cliente (frecuente/moroso/VIP), recordatorios. Nuevas entidades (`Cobro`/`PagoCliente`/`ClienteEtiqueta`) + endpoints `/api/cobros/*`. Esfuerzo alto. | Alta |
| **B-3** | **NeoScan / OCR** | Sprint 23 del backlog. Bandeja de documentos, captura, OCR/IA (emisor, fecha, monto, n° control, sello, NIT), clasificar como compra/gasto/factura recibida; alimenta NeoProfit. Endpoints `/api/scan/*`. Esfuerzo alto. | Media |
| **B-4** | **Alertas y notificaciones push** | Registro de dispositivo (FCM token), centro de alertas, generación de alertas (DTE rechazado, cert por vencer, factura vencida, F-07), marcar resuelta. Endpoints `/api/alertas/*` + worker. Esfuerzo medio-alto. | Media |
| **B-5** | **QR / enlace de cobro** | Generar QR/enlace de pago asociado a factura/monto, integrado con proveedores (Wompi/PayPal/transferencia ya existen en Billing). Endpoints `/api/cobros/{id}/qr`. Depende de B-2. Esfuerzo medio. | Media |
| **B-6** | **Verificación de NIT/NRC en línea** | Consulta contra el servicio de MH (si está disponible) para validar/autocompletar receptor. Endpoint `/api/lookups/verificar-nit`. Esfuerzo bajo-medio (sujeto a disponibilidad MH). | Baja |
| **B-7** | **DTE recibidos** | Registro y consulta de documentos recibidos (proveedores). Se materializa con B-3 (NeoScan) + B-2. | Media |

> Estas brechas también benefician al panel web; algunas ya están en el backlog (NeoScanAI = Sprint 23).

---

## 3. Plan de sprints

Sprints de ~2 semanas. **Frontend (FE)** = Flutter; **Backend (BE)** = NeoSTP. Las fases están ordenadas para
entregar valor temprano con bajo riesgo y dejar las brechas grandes para después.

### Sprint 0 — Fundaciones (FE)
- Proyecto Flutter, arquitectura (capas, `dio`, `riverpod`/`bloc`, `freezed`).
- `ApiClient` con `ApiResponse<T>`, manejo de errores tipado, interceptor de **JWT + refresh (401)**.
- Almacenamiento seguro de tokens; pantalla **Login** + `me`; carga de **permisos**.
- Selección de empresa (solo multi-empresa/SuperAdmin); **health check** en splash.
- Tema/diseño base (alineado a la identidad NeoSTP), navegación por pestañas (Inicio, Facturar, Clientes, Escanear, Más).
- *BE:* ninguno (usa API actual).

### Sprint 1 — Dashboard + Consulta de DTE (FE, read-only)
- **Inicio/Dashboard:** `GET /api/dashboard/empresa` (KPIs del día/mes, por estado, tendencia).
- **Consulta DTE:** lista con filtros (`tipo`, `estado`, fechas, `search`), detalle, **ver/compartir PDF**, ver JSON, estado MH, reenviar.
- Accesos rápidos del dashboard.
- *BE:* ninguno. (Quick win: solo lectura, sin riesgo fiscal.)

### Sprint 2 — Emisión rápida de DTE (FE + BE B-1)
- *BE:* **B-1** `POST /api/dte/emitir` (un solo paso).
- **Facturar:** buscar/crear cliente, agregar líneas (lookups/escaneo de código de barras), descuento simple,
  **vista previa** (totales del backend), emitir, consultar estado, **compartir PDF**.
- Tipos: 01 y 03 primero; luego 05/06/14.
- Manejo de contingencia/rechazo (mostrar estado y diagnóstico).

### Sprint 3 — CRM ligero + Catálogo (FE)
- **Clientes:** lista/búsqueda, ficha, crear/editar, inactivar, llamar/WhatsApp/correo, importar de contactos, emitir factura desde la ficha.
- **Catálogo:** lista/búsqueda, escanear código de barras, crear rápido, activar/inactivar.
- *BE:* ninguno (CRUD ya existe). Opcional **B-6** (verificación NIT) si MH lo permite.

### Sprint 4 — Configuración de cuenta DTE + perfil (FE)
- **Configuración fiscal** replicada de la web: datos del emisor, ambiente, credenciales MH,
  **subir certificado** (base64), probar conexión, estado de "cuenta lista".
- **Perfil:** datos del usuario, cambio de contraseña, MFA, cerrar sesión.
- *BE:* ninguno (`dte/configuracion/*` ya existe).

### Sprint 5 — Cobros / Cuentas por cobrar (FE + BE B-2)
- *BE:* **B-2** entidades + endpoints `/api/cobros/*` (saldos, pendientes/vencidas, registrar pago, adjuntar comprobante, etiquetas de cliente).
- **Cobros:** pendientes/vencidas, filtros, saldo por cliente, recordatorio por WhatsApp/correo, registrar pago básico.
- Dashboard: tarjetas "pendientes de cobro" y "clientes con deuda".
- CRM: saldo/crédito/historial de cobros, etiquetas.

### Sprint 6 — QR de cobro + enlaces de pago (FE + BE B-5)
- *BE:* **B-5** generación de QR/enlace de pago sobre Billing (Wompi/PayPal/transferencia).
- **Cobros:** generar QR de pago, copiar enlace, compartir.

### Sprint 7 — NeoScan / respaldo de facturas (FE + BE B-3)
- *BE:* **B-3** (NeoScanAI, Sprint 23 del backlog): bandeja, OCR/IA, clasificación, asociación a proveedor/cliente, alimenta NeoProfit; **DTE recibidos** (B-7).
- **Escanear:** capturar factura/PDF/QR/código de barra, extraer datos, revisar/corregir, guardar respaldo, clasificar.

### Sprint 8 — Alertas y notificaciones push (FE + BE B-4)
- *BE:* **B-4** registro FCM, generación de alertas (DTE rechazado, cert por vencer, factura vencida, F-07), centro de alertas, marcar resuelta.
- **Alertas:** push (FCM), centro de notificaciones, configuración (canal/horario/no molestar), abrir documento afectado.

### Sprint 9 — Pulido, rendimiento y publicación
- Caché offline de catálogos, scroll infinito, estados vacíos/errores, accesibilidad, modo tablet.
- Pruebas en dispositivos, hardening, **publicación en Play Store** (interna → producción).
- *BE:* **B-6** verificación NIT si quedó pendiente; ajustes de rendimiento de endpoints muy usados.

---

## 4. Verificación de que la API "cumple" para la app

Checklist a validar antes/durante el desarrollo (lo cubre el backend):

- [x] **JWT** login/refresh/logout + `me` con `permisos` → control de UI por permiso. (✅ existe)
- [x] **JSON** en todo (sobre `ApiResponse<T>`), descargas binarias para PDF/JSON. (✅)
- [x] **Multi-tenant** por token; SuperAdmin con `?empresaId`. (✅)
- [x] **Configuración DTE + certificado** vía API (base64), probar conexión. (✅)
- [x] **Emisión y transmisión a MH** server-side; estados PROCESADO/RECHAZADO/CONTINGENCIA; sello. (✅)
- [x] **Consulta** paginada + filtros; PDF con QR/branding; reenvío por correo. (✅)
- [x] **CRM y catálogo** CRUD + lookups/escaneo. (✅)
- [x] **Rate limiting** y cuotas (`429` + `Retry-After`) — la app debe respetarlas. (✅)
- [x] **Emisión en un paso** (`/api/dte/emitir` + atajos por tipo) — **B-1 entregado**.
- [ ] **Cobros/CxC**, **QR de cobro** — **B-2 / B-5**.
- [ ] **NeoScan/OCR** + **DTE recibidos** — **B-3 / B-7**.
- [ ] **Alertas push** — **B-4**.
- [ ] **Verificación NIT** — **B-6**.

> La app puede arrancar **hoy** con Sprints 0–4 (login, dashboard, consulta, emisión con encadenado de pasos
> o B-1, CRM, catálogo, configuración) usando la API existente. Las brechas se entregan en paralelo desde el
> backend según el orden de sprints.

---

## 5. Notas de coordinación FE ↔ BE

- **Contrato estable:** los DTO de `NeoCloud-Mobile-API.md` son el contrato. Cambios incompatibles se versionan/avisan.
- **Ambientes:** el dev Flutter trabaja contra staging (apitest de MH). Credenciales y certificado de prueba los provee NeoSTP.
- **OpenAPI:** `/openapi/v1.json` como referencia viva del esquema.
- **Soporte:** ante errores, el `traceId` de cada respuesta correlaciona con los logs del backend.
- **Seguridad:** tokens en `flutter_secure_storage`; nunca loguear tokens ni el blob del certificado; HTTPS obligatorio en staging/prod.
