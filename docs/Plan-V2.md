# NeoSTP Cloud — Plan de mejora V2 y nuevos módulos

Roadmap de la **versión 2** de NeoSTP Cloud: análisis del estado actual (Web + API), ejes de mejora
transversales y nuevos módulos a incorporar, **desglosados** por alcance, entregables y prioridad.

> Fecha de corte: cierre del backend de NeoCloud Mobile (B-1…B-6). Build verde, 370 tests unit + 2 integración.
> Acompaña a: `NeoCloud-Mobile-API.md`, `NeoCloud-Mobile-Plan.md`, `CONTEXTO-PROYECTO.md`, backlog sprints 13–30.

---

## 1. Estado actual (qué ya existe)

**Plataforma:** .NET 10 · ASP.NET Core MVC/Razor (Web) + Web API (Api) + Worker · EF Core 10 · SQL Server.
Multiempresa con RBAC, licenciamiento por módulo, auditoría, hardening, design system `ns-*`.

**Módulos en producción:**
- **Core/Admin** — empresas, usuarios, roles, permisos, multiempresa, MFA SuperAdmin, IP allowlist.
- **NeoDTE** — emisión (01/03/05/06/14), pipeline a Hacienda, certificación, eventos, contingencia, diagnóstico, branding (logo/firma) en PDF, correo de envío.
- **Maestros** — clientes (+ etiquetas, carga masiva), productos (+ código de barras, carga masiva), catálogos MH, lookups, verificación NIT.
- **Dashboard + NeoProfit** — KPIs, rentabilidad, gastos y compras manuales.
- **NeoConnect** — API keys, webhooks firmados, endpoints v1, OpenAPI público + Scalar.
- **Billing / Pagos LATAM** — planes, suscripciones, Wompi/PayPal/Transferencia, Legal/consentimiento.
- **Onboarding self-service** — checklist + asistente.
- **Backend NeoCloud Mobile (B-1…B-6)** — emisión en 1 paso, Cobros/CxC, NeoScanAI (bandeja+conversión a gasto/compra/DTE recibido), Alertas/push, QR de cobro, verificación NIT.
- **Worker** — retransmisión contingencia, limpieza tokens, lotes, backups, entrega de webhooks, generación de alertas.

---

## 2. Análisis de la Web (panel)

**Fortalezas:** 25 controladores + ~71 vistas modernizadas al design system (Sprints 26–27), AppShell,
multiempresa con modo soporte, gestión completa de DTE/maestros/billing/hardening/branding/integraciones.

**Brechas detectadas:**
- 🔴 **Sin UI para los módulos nuevos del móvil:** Cobros/CxC, NeoScan (bandeja), Alertas, QR de cobro,
  cuentas de cobro y DTE recibidos **solo existen como API**. El admin no puede operarlos desde la web.
- 🟡 **NeoProfit** tiene dashboard pero los grids podrían enriquecerse (drill-down, export).
- 🟡 **Reportes fiscales** (libro IVA ventas/compras, F-07) no existen como vista.
- 🟡 **i18n/accesibilidad** sin formalizar; algunas vistas heredan emojis/legacy.
- 🟡 **Gestión de blobs** (logo/firma/scan/certificado) en columnas de BD; sin visor/gestor centralizado.

## 3. Análisis de la API

**Fortalezas:** 28 controladores, contrato uniforme `ApiResponse<T>`, JWT + permisos + módulos, rate limiting,
multi-tenant seguro (token manda), OpenAPI + Scalar, NeoConnect v1 por API key.

**Mejoras técnicas detectadas:**
- 🔴 **Integraciones externas en mock:** OCR/IA de NeoScan (`Scan:Provider=Mock`), push FCM (`Push:Provider=Mock`).
  Hooks listos; falta el proveedor real.
- 🟡 **Cobertura de pruebas:** 370 unit (muy bueno) pero **solo 2 de integración**. Faltan tests de
  controladores/host real (WebApplicationFactory) para los módulos nuevos.
- 🟡 **Blobs en BD:** certificado, logo/firma, imágenes de scan se guardan como `byte[]` en columnas →
  no escala; conviene storage externo (S3/Azure Blob) con `IStorageService` (ya existe la abstracción).
- 🟡 **Caché por instancia** en lookups → no compartida entre nodos; mover a caché distribuida (Redis) para escalar horizontal.
- 🟡 **Observabilidad:** Serilog OK; falta tracing distribuido (OpenTelemetry) y métricas (counts, latencias) expuestas.
- 🟡 **Versionado de API:** v1 solo para NeoConnect; el resto sin versión explícita. Definir estrategia de versionado.

---

## 4. Ejes de mejora transversales (M)

### M1 — Paridad Web ↔ API (UIs de los módulos del móvil) · **Prioridad: Alta**
Construir las vistas web que faltan, reusando el design system y los servicios ya existentes:
- **M1.1 Cobros/CxC** (`ICobranzaService`): dashboard de cartera, grid de pendientes/vencidas, ficha de saldo por cliente, registrar/confirmar/anular pago.
- **M1.2 Cuentas de cobro + QR** (`ICobroQrService`): CRUD de cuentas/pasarelas, generador de QR de pago.
- **M1.3 NeoScan** (`IScanService`): bandeja con preview, revisión/corrección de campos, conversión a gasto/compra/DTE recibido.
- **M1.4 Alertas** (`IAlertaService`): centro de notificaciones, preferencias, gestión de dispositivos.
- **M1.5 DTE recibidos** (`Dte_DocumentosRecibidos`): listado y detalle de documentos de proveedor.
- *Entregable:* 5 áreas de menú nuevas con CRUD/operación completa, gated por permisos.

### M2 — Encender integraciones reales (quitar mocks) · **Prioridad: Alta**
- **M2.1 OCR/IA real para NeoScan:** implementar `IScanExtractionService` real (Azure Document Intelligence / Google Document AI / LLM con visión). Toggle `Scan:Provider`. Reintentos + colas.
- **M2.2 Push FCM real:** `FcmPushSender` (HTTP v1 con service account). Toggle `Push:Provider`. Manejo de tokens inválidos.
- **M2.3 (cuando MH lo publique)** verificación NIT en línea (`Fuente=MH`).
- *Entregable:* la app y la web pasan de "captura manual / sin push" a extracción y push reales con solo cambiar config.

### M3 — Observabilidad y operación · **Prioridad: Media**
- **M3.1** OpenTelemetry (tracing + métricas) export a OTLP/Prometheus.
- **M3.2** Health checks detallados (BD, MH, correo, storage) en `/health/ready` y `/health/live`.
- **M3.3** Panel de salud y métricas SaaS (uso por empresa, DTE/min, errores MH) para SuperAdmin.
- **M3.4** Auditoría consultable desde la web (filtros, export).

### M4 — Rendimiento y escalabilidad · **Prioridad: Media**
- **M4.1** Mover blobs (certificado, logo/firma, scan) a **storage externo** vía `IStorageService` (S3/Azure Blob); en BD solo referencia.
- **M4.2** Caché distribuida (Redis) para catálogos/lookups y licencias.
- **M4.3** Índices y consultas: revisar planes de las queries de cobranza/profit/scan; paginación por cursor en listados grandes.
- **M4.4** Cola de trabajo (canal/Hangfire) para tareas pesadas (OCR, push masivo, generación de reportes).

### M5 — Calidad y testing · **Prioridad: Alta**
- **M5.1** Tests de **integración** (WebApplicationFactory + BD efímera) para los módulos nuevos (Cobros, Scan, Alertas, QR, emisión 1-paso).
- **M5.2** Tests de contrato de la API (OpenAPI) para evitar breaking changes.
- **M5.3** Pipeline **CI** (build + test + migraciones idempotentes) en cada PR.
- **M5.4** Cobertura objetivo y reporte.

### M6 — Seguridad y cumplimiento · **Prioridad: Media-Alta**
- **M6.1** Secretos en **secrets manager** (User Secrets local, Key Vault/variables en prod) — sacar credenciales de `appsettings.Local.json`.
- **M6.2** MFA opcional para todos los usuarios (hoy solo SuperAdmin); políticas de contraseña/bloqueo.
- **M6.3** Rotación de claves JWT y de firma; retención/borrado de datos (GDPR-like) y backups restaurables probados.
- **M6.4** RBAC más granular por módulo nuevo + revisión de auditoría de acciones críticas.

### M7 — UX / Design system · **Prioridad: Media**
- **M7.1** Completar modernización `ns-*` en vistas legacy; quitar emojis/estilos inline residuales.
- **M7.2** Accesibilidad (contraste, navegación por teclado, labels) e i18n (es/en).
- **M7.3** Componentes de grid/dashboard reutilizables (export CSV/PDF, filtros guardados).

### M8 — DevEx / CI-CD / despliegue · **Prioridad: Media**
- **M8.1** Dockerización (Web, Api, Worker) + compose para dev.
- **M8.2** Pipeline de despliegue por entorno (dev/staging/prod) con migraciones controladas (script + revisión).
- **M8.3** Datos demo/seed reproducibles y reseteo rápido.

---

## 5. Nuevos módulos a incorporar (N)

### N1 — NeoPOS (Punto de venta) · módulo `NEOPOS` (102) · **Esfuerzo: Alto**
- Pantalla de venta táctil (tablet), búsqueda/escaneo de producto, carrito, descuentos.
- Cajas, turnos (apertura/cierre), arqueo, métodos de pago (efectivo/tarjeta/QR).
- Emisión rápida del DTE al cerrar la venta (reusa `IConnectDteService.EmitirAsync`).
- Impresión/compartir ticket; modo offline básico con cola de emisión.
- *Tablas:* `Pos_Cajas`, `Pos_Turnos`, `Pos_Ventas`, `Pos_VentaPagos`. Alimenta NeoProfit y Cobros.

### N2 — Inventario · módulo `INVENTARIO` (110) · **Esfuerzo: Alto**
- Stock por producto/sucursal, movimientos (entrada/salida/ajuste/traslado), **kardex**.
- Costo promedio/PEPS → mejora el costo en NeoProfit (hoy `CostoUnitario` manual).
- Alertas de stock bajo (integra con Alertas B-4). Existencia visible al facturar/POS.
- *Tablas:* `Inv_Existencias`, `Inv_Movimientos`. Conecta con Compras (N3) y NeoScan.

### N3 — Compras y Proveedores (CxP) · módulo `COMPRAS` (111) · **Esfuerzo: Alto**
- Maestro de **proveedores**, órdenes de compra, recepción de mercadería.
- **Cuentas por pagar** (espejo de Cobros/CxC): saldos, vencimientos, pagos a proveedor.
- Integra con **NeoScan** (DTE recibido → compra) e **Inventario** (recepción → stock).
- *Tablas:* `Compras_Proveedores`, `Compras_Ordenes`, `Compras_Recepciones`, `Cxp_Pagos`.

### N4 — NeoBI / Reportes fiscales y contables · módulo `NEOBI` (105) · **Esfuerzo: Medio-Alto**
- **Libro de ventas / compras IVA**, resumen para **F-07** (declaración mensual), retenciones/percepciones.
- Reportes contables (ventas por periodo/cliente/producto/sucursal), exportación Excel/PDF.
- Dashboards avanzados (cohortes, tendencias) reusando NeoProfit + Cobros + Inventario.
- *Entregable:* generador de reportes con filtros y export; alerta F-07 (cierra B-4).

### N5 — NeoPortal Clientes (receptor) · módulo `NEOPORTAL` (107) · **Esfuerzo: Medio-Alto**
- Portal público/auth ligera para que el **receptor** consulte y descargue sus DTE (PDF/JSON).
- **Pago de facturas** desde el portal usando el QR/enlace de cobro (B-5) + pasarelas (Billing).
- Historial, estado de cuenta, reenvío. Branding por empresa.

### N6 — NeoScanAI real (OCR/IA productivo) · **Esfuerzo: Medio** (parte de M2.1)
- Proveedor real + bandeja avanzada (cola, reproceso, confianza por campo, plantillas por proveedor).
- Aprendizaje/corrección que mejora extracciones futuras.

### N7 — Conciliación bancaria · **Esfuerzo: Medio-Alto**
- Importar movimientos bancarios (CSV/API), conciliar contra pagos (Cobros) y CxP (Compras).
- Estados de cuenta y diferencias. Integra con Billing/transferencias.

### N8 — NeoCloud Mobile (app Flutter) · **Esfuerzo: Alto** (cliente, backend listo)
- Construcción de la app Android (tablet/celular) consumiendo la API documentada (B-1…B-6).
- Ver `NeoCloud-Mobile-Plan.md` (Sprints 0–9). Publicación en Play Store.

### N9 — Multi-moneda y exportación avanzada · **Esfuerzo: Medio**
- Soporte multi-moneda con tipo de cambio; factura de exportación (11) completa.

### N10 — NeoConnect avanzado · **Esfuerzo: Medio**
- Más endpoints v1 (eventos, reportes), SDKs cliente, ampliación de scopes, sandbox enriquecido.

### N11 — Recordatorios automáticos + WhatsApp · **Esfuerzo: Medio**
- Job del Worker que envía recordatorios de cobro (correo/WhatsApp Business API) según vencimientos.
- Plantillas configurables; integra con Cobros (B-2) y Alertas (B-4).

### N12 — SuperAdmin operativo avanzado · backlog Sprint 29 · **Esfuerzo: Medio**
- Métricas SaaS, gestión global de empresas/planes/precios, impersonación **auditada**, soporte avanzado.

---

## 6. Roadmap por versiones

> Orden recomendado por **valor temprano + bajo riesgo**, dejando lo más pesado/dependiente para después.

### V2.0 — Consolidación y paridad (estabiliza lo construido)
- **M1** UIs web de los módulos del móvil (Cobros, Scan, Alertas, QR, DTE recibidos).
- **M2** Encender integraciones reales (OCR + FCM).
- **M5** Tests de integración + CI.
- **N8** App Flutter **MVP** (Sprints 0–4) en paralelo (login, dashboard, consulta, emisión, CRM, config).
- **M3.2** Health checks detallados.

### V2.1 — Operación comercial (cierra el ciclo financiero)
- **N2** Inventario · **N3** Compras/Proveedores + CxP · **N4** NeoBI/Reportes fiscales (Libro IVA, F-07).
- **N11** Recordatorios automáticos de cobro.
- **N8** App Flutter Sprints 5–8 (Cobros, QR, Scan, Alertas).

### V2.2 — Canales y experiencia
- **N1** NeoPOS · **N5** NeoPortal Clientes.
- **M7** UX/i18n/accesibilidad · **N9** multi-moneda/exportación.
- **N8** App Flutter Sprint 9 (pulido + publicación).

### V2.3 — Escala y enterprise
- **M4** Rendimiento (blobs externos, Redis, colas) · **M6** Seguridad/cumplimiento · **M8** CI-CD/Docker.
- **N7** Conciliación bancaria · **N10** NeoConnect avanzado · **N12** SuperAdmin avanzado · **M3** observabilidad completa.

---

## 7. Priorización (impacto × esfuerzo)

| Iniciativa | Impacto | Esfuerzo | Versión |
|---|---|---|---|
| M1 UIs web módulos móvil | Alto | Medio | V2.0 |
| M2 Integraciones reales (OCR/FCM) | Alto | Medio | V2.0 |
| M5 Tests integración + CI | Alto | Medio | V2.0 |
| N8 App Flutter MVP | Alto | Alto | V2.0–V2.2 |
| N2 Inventario | Alto | Alto | V2.1 |
| N3 Compras/Proveedores + CxP | Alto | Alto | V2.1 |
| N4 NeoBI / Reportes fiscales (F-07) | Alto | Medio-Alto | V2.1 |
| N11 Recordatorios cobro | Medio-Alto | Medio | V2.1 |
| N1 NeoPOS | Alto | Alto | V2.2 |
| N5 NeoPortal | Medio-Alto | Medio-Alto | V2.2 |
| M4 Rendimiento/escala | Medio | Medio-Alto | V2.3 |
| M6 Seguridad/cumplimiento | Alto | Medio | V2.3 |
| N7 Conciliación bancaria | Medio | Medio-Alto | V2.3 |
| N12 SuperAdmin avanzado | Medio | Medio | V2.3 |

---

## 8. Definición de "completado" (por iniciativa)

- Build verde · tests (unit + integración del módulo) verdes · migración aplicada e idempotente.
- Multiempresa respetada (`EmpresaId`) · licenciamiento por módulo · permisos · auditoría de acciones críticas.
- UI con design system `ns-*` y datos reales (sin mocks salvo los pluggables documentados).
- `CONTEXTO-PROYECTO.md` / README / docs actualizados; secretos fuera del repo.

---

## 9. Quick wins (se pueden tomar ya, bajo riesgo)

1. **UI web de Cobros** (M1.1) — alto valor, reusa `ICobranzaService` 100%.
2. **`FcmPushSender` real** (M2.2) — desbloquea push; solo requiere service account.
3. **Health checks detallados** (M3.2) — operación más segura.
4. **Tests de integración** de los módulos nuevos (M5.1) — protege lo construido.
5. **Mover el App Password de correo a User Secrets** (M6.1) — higiene de secretos.
