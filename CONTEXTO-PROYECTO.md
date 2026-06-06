# 📘 Contexto Maestro — NeoSTP Cloud · NeoSTP Business Suite

> Documento único de contexto del proyecto. Reúne: estado real del sistema (README),
> arquitectura, base de datos, **explicación detallada del funcionamiento DTE/Hacienda**,
> catálogos MH, módulos de mantenimiento, plan de trabajo para completar la suite,
> plan de mejora de UI, skills, y análisis/mejora de código.
>
> **Versión:** NeoConnect COMPLETO · NeoProfit (Sprint 22) · Onboarding · Branding · Backend NeoCloud Mobile (B-1…B-6) · Scalar · Plan-V2 · **Rama:** `main` · **Build:** ✅ 0 errores · **Tests:** 370 unit + 2 integración
> **Repositorio:** `github.com/CarlosAGRuiz/NeoStpCloud`

---

## Índice

1. [Visión general del ecosistema](#1-visión-general-del-ecosistema)
2. [Stack y arquitectura](#2-stack-y-arquitectura)
3. [Estado actual del sistema](#3-estado-actual-del-sistema)
4. [Base de datos — tablas actuales y propuestas](#4-base-de-datos)
5. [Funcionamiento detallado DTE / Hacienda](#5-funcionamiento-detallado-dte--hacienda) ⭐
6. [Catálogos MH y módulo de mantenimiento](#6-catálogos-mh-y-módulo-de-mantenimiento)
7. [Módulos de mantenimiento del Suite](#7-módulos-de-mantenimiento-del-suite)
8. [Catálogo de endpoints (actuales + propuestos)](#8-catálogo-de-endpoints)
9. [Módulos del Suite — estado y pendientes](#9-módulos-del-suite)
10. [Plan de trabajo para completar la Suite](#10-plan-de-trabajo-para-completar-la-suite)
11. [Plan de mejora UI/UX](#11-plan-de-mejora-uiux)
12. [Skills (Claude Code) del proyecto](#12-skills-del-proyecto)
13. [Análisis y mejora de código, comentarios y BD](#13-análisis-y-mejora-de-código)
14. [Objetivo final del producto](#14-objetivo-final-del-producto)

---

# 1. Visión general del ecosistema

**NeoSTP Cloud** es el sistema web principal de **NeoSTP Business Suite** — una plataforma
SaaS modular multiempresa, no solo un facturador electrónico. Cada cliente accede a **una
sola web** y, según su plan/licencia, usa diferentes módulos.

- **Web central:** NeoSTP Cloud
- **App móvil:** NeoSTP Mobile
- **API central:** NeoSTP API
- **Módulos activables por plan**, viviendo en una sola plataforma:

| Módulo | Qué hace |
|---|---|
| **Core / Administración** | Empresas, usuarios, roles, permisos, planes, módulos, licencias, sucursales, puntos de venta, auditoría |
| **NeoDTE** | Emisión de Documentos Tributarios Electrónicos El Salvador |
| **Certificación DTE** | Control de la matriz de pruebas exigida por Hacienda |
| **Eventos DTE** | Invalidación, Contingencia, Retorno, Operaciones Especiales |
| **NeoProfit / NeoBI** | Análisis financiero y rentabilidad |
| **NeoScanAI** | Escaneo inteligente (OCR/IA) de documentos, compras, gastos |
| **NeoConnect API** | API comercial para integradores/ERPs |
| **NeoPOS** | Punto de venta web integrado con DTE |
| **NeoPortal Clientes** | Portal para receptores/clientes finales |
| **NeoSTP Mobile / Mobile Management** | App móvil y gestión de dispositivos |
| **Billing SaaS** | Venta self-service (Stripe / MercadoPago) |
| **Legal / Compliance** | Términos, privacidad, consentimiento |
| **SuperAdmin NeoSTP** | Operación interna del proveedor SaaS |
| **Hardening / Operación** | Seguridad, backups, monitoreo |

**Principio de diseño:** los módulos se venden por separado pero el cliente **no debe sentir
que usa sistemas diferentes**. Cada módulo respeta: empresa actual, plan activo, módulos
contratados, permisos del usuario, auditoría, multiempresa, seguridad y **aislamiento por
`EmpresaId`**.

---

# 2. Stack y arquitectura

## Stack
- **.NET 10** (LTS hasta nov-2028)
- **ASP.NET Core MVC + Razor** (Web) · **ASP.NET Core Web API + OpenAPI** (Api)
- **SQL Server 2022** + **EF Core 10**
- **.NET Worker Service** (background jobs)
- **Serilog** (logs estructurados) · **QuestPDF 2025.1** · **MailKit 4.17**
- **Polly v8 / Microsoft.Extensions.Http.Resilience 10.6** (resiliencia HTTP)
- **JWT** (Api) + **Cookies** (Web) · **DataProtection** (cifrado de secretos) · **BCrypt** (passwords)
- **xUnit + FluentAssertions** (179 tests)
- **ClosedXML 0.104** (export/import Excel del módulo de catálogos)

## Arquitectura — modular monolith por capas

```
NeoSTP.slnx
├── src/
│   ├── NeoSTP.Web              # MVC/Razor (UI, Bootstrap → Tailwind gradual)
│   ├── NeoSTP.Api              # Web API (REST + OpenAPI)
│   ├── NeoSTP.Application      # Casos de uso, servicios, DTOs, abstracciones (interfaces)
│   ├── NeoSTP.Domain           # Entidades, reglas, enums, constantes
│   ├── NeoSTP.Infrastructure   # EF Core, SQL Server, clientes Hacienda, firma, PDF, correo
│   ├── NeoSTP.Worker           # Background jobs (contingencia, limpieza tokens)
│   └── NeoSTP.Shared           # ApiResponse, utilidades, constantes
├── tests/
│   ├── NeoSTP.Tests.Unit       # 370 tests
│   └── NeoSTP.Tests.Integration # 2 tests
├── design/                     # Design system + 7 mockups (Stitch) — fuente de verdad UI
└── docs/                       # Runbooks + NeoConnect-API-v1.md + NeoCloud-Mobile-{API,Plan}.md + Plan-V2.md
```

**Referencias:** Web/Api/Worker → Application + Infrastructure + Shared; Application → Domain + Shared;
Infrastructure → Application + Domain + Shared. Las **interfaces** (abstracciones) viven en Application;
las **implementaciones** en Infrastructure → inversión de dependencias limpia.

**Toggles Mock/Real** (en `appsettings.Local.json`, gitignored):
- `Hacienda:Client` = `Mock | Http`
- `Dte:Signer` = `Mock | Pkcs12 | HaciendaCert`
- `Email:Provider` = `Mock | Smtp`

> ⚠️ La **Web firma con sus propios servicios** (no llama a la Api). Su `appsettings.Local.json`
> también debe fijar `Dte:Signer=HaciendaCert` para firmar real (de lo contrario emite firma mock
> y el guardrail anti-mock la bloquea).

---

# 3. Estado actual del sistema

## ✅ Implementado y funcionando
Empresas · Usuarios · Roles · Permisos · Planes · Módulos · Licenciamiento · Sucursales ·
Puntos de venta · Clientes · Productos · Configuración DTE (cifrada) · Generación DTE ·
Firma JWS (RS512) · Transmisión a Hacienda · PDF con QR · Correo · Dashboard empresa/SuperAdmin ·
Worker de retransmisión de contingencia · Worker de limpieza de tokens · Empresa de pruebas
automática · Toggles Mock/Real · **Módulo de mantenimiento de Catálogos** (CRUD + import
CSV/JSON/XLSX + export + versionado + cascadas padre/hijo, vía API y UI MVC) · **Módulo de
Certificación DTE** (matriz 625 escenarios, progreso por tipo, asociación documentos a
escenarios, reintentos, snapshots de errores MH, dashboard con barras de progreso) ·
**Módulo de Eventos DTE persistentes** (4 tablas Dte_Eventos*, persistencia best-effort de los 4 flujos certificados, consulta + creación + PDF + UI MVC; integración con certificación vía marcar-completado-por-evento) ·
**Módulo de Diagnóstico de errores Hacienda** (tablas `Dte_ErrorCatalogo`/`Dte_ErrorOcurrencias`, entidades `DteErrorCatalogo`/`DteErrorOcurrencia`, servicio `DiagnosticoHaciendaService`, seed 11 códigos MH+internos, API REST `/api/dte/diagnostico`, UI MVC `/DiagnosticoHacienda` con resumen, filtros, detalle documento/evento, marcar resuelta, sincronización histórica; permiso `DTE.Diagnostico`) · **Módulo Legal + consentimiento** (tabla `Core_UserConsents`, entidad `UserConsent`, `LegalDocumentService`, páginas públicas `/legal/terms|privacy|cookies|dpa`, `LegalOptions` con placeholders, checkbox obligatorio en creación de usuario, footer con enlaces legales) · **Módulo de Hardening pre-producción** (tablas `Ops_BackupJobs`/`Core_ApiUsageLog`/`Core_ApiQuotas`/`Core_AdminIpAllowlist` + columnas MFA en `Core_Usuarios`; rate limiting con `ApiQuotaMiddleware` → 429 por ventana deslizante; MFA TOTP RFC 6238 con `TotpService`/`MfaService` y códigos de recuperación; IP allowlist con `AdminIpAllowlistMiddleware` (CIDR, fail-open); backups con `BackupService`/`BackupWorker`/`IStorageService` (LOCAL/AZURE_BLOB/S3); API `/api/hardening` + UI `/Hardening`; k6 baseline, GitHub Action OWASP ZAP, runbook DR; permisos `Ops.Hardening.Ver/.Administrar`) · **UI/UX moderna** (AppShell `neostp.css` con tokens del design system, sidebar/navbar nuevas, indicador de ambiente, responsive; re-tema global de Bootstrap; pulido de todos los listados; StepperDTE) · **Pagos LATAM** (Billing multi-proveedor: `IPaymentProviderResolver` + `WompiBillingProvider`/`PayPalBillingProvider`/`TransferenciaPaymentProvider`; transferencia con verificación manual `/billing/transferencias`) · **Lookups** (`ILookupService` + `/api/lookups`: catálogos, cascada territorial, datos maestros) y eliminación de hardcodeos territoriales (`TerritorialOptions`) · **Carga masiva** de clientes y productos (Excel/CSV, upsert + dry-run + reporte por fila, `/Clientes/Importar` y `/Productos/Importar`) · Fix modo soporte multiempresa (`EmpresasService.GetByIdAsync` corregido) · **NeoConnect API** (Sprint 24, en curso — sub-entregas 1-5 cerradas, falta sub-entrega 6 de tests): entidades `ConnectApiKey`/`ConnectWebhook`/`ConnectWebhookDelivery` (tablas `Connect_ApiKeys`/`Connect_Webhooks`/`Connect_WebhookDeliveries`), permisos `Connect.ApiKeys.Ver/.Administrar`/`Connect.Webhooks.Ver/.Administrar`/`Connect.Logs.Ver` (351-355); `ConnectApiKeyService` (API Key con hash SHA-256, prefijo visible, scopes, raw key mostrada una sola vez) + `ApiKeyAuthMiddleware` (header `X-Api-Key` → resuelve empresa/scopes en `HttpContext.Items`, engancha `ApiKeyId` al `ApiQuotaMiddleware`; JWT tiene precedencia); `ConnectController` (`/api/connect/{api-keys|webhooks|logs|usage}`); webhooks con dispatcher (`IConnectWebhookDispatcher` disparado desde `DteDocumentosService` al cambiar estado DTE a PROCESADO/RECHAZADO/CONTINGENCIA/INVALIDADO, best-effort) + `ConnectWebhookDeliveryWorker` (entrega firmada HMAC-SHA256 con reintentos y backoff exponencial 2/4/8/16 min, máx. 5 intentos); UI Web `/Integraciones` (AppShell + `ns-*`) · **endpoints de negocio v1** (`/api/v1`, `ConnectApiV1Controller`: emitir/consultar/descargar DTE, alta clientes/productos por API Key + scopes), OpenAPI público + docs · **NeoConnect COMPLETO** (gestión + negocio + tests).
· **NeoProfit** (Sprint 22, completo): `ProfitCalculator` puro (reglas PROCESADO/NC resta/ND suma/SE sin IVA/costo pendiente), `IProfitService`, `/api/profit/*` (`RequireModule("NEOPROFIT")`, permisos 370/371), dashboard `Profit/Index` + grids/CRUD `ProfitGastos`/`ProfitCompras`.
· **Onboarding self-service**: `IOnboardingService` (5 pasos derivados de datos reales), checklist en dashboard + asistente `/onboarding`.
· **Branding** (logo + firma por empresa): `Empresa.LogoBlob/FirmaBlob/FirmaTexto`, `IBrandingService`, UI `/branding`; usados en el PDF (banda + pie) y en el correo (logo CID).
· **Correo HTML** rediseñado (cuerpo con cuadro de datos DTE) + config `Email` (Mock/Smtp) + diagnóstico "Probar correo" en `/Hardening`.
· **Backend NeoCloud Mobile (B-1…B-6)**: emisión en 1 paso (`/api/dte/emitir`), **Cobros/CxC** (`ICobranzaService`, `/api/cobros/*`), **NeoScanAI** (`IScanService`, bandeja + conversión a gasto/compra/DTE recibido, `/api/scanai/*`), **Alertas/push** (`IAlertaService` + generación en el Worker, `/api/alertas/*`), **QR de cobro** (`ICobroQrService`), **verificación NIT** (`/api/lookups/verificar-nit`); proveedores OCR/FCM en mock pluggable.
· **Scalar** (explorador interactivo de la API en `/scalar/v1`).
· **370 tests unit + 2 integración.**

## 🏆 Certificación contra Hacienda (apitest real) — Sprint 12

**Hallazgo decisivo:** el ambiente **apitest valida contra esquemas v1/v3, NO v2/v4**. Los
archivos `svfe-json-schemas` (v2/v4) son más nuevos que lo desplegado en apitest. La
certificación se hace contra **v1/v3**.

| DTE | Versión apitest | Estado | Sello |
|---|---|---|---|
| **01 Factura** | v1 | ✅ PROCESADO | `20262…HIPM` |
| **11 Factura Exportación** | v3 | ✅ PROCESADO | `2026DDE…DRBC` |
| **04 Nota de Remisión** | v3 | ✅ PROCESADO | `2026F…6Z9W` |
| **14 Sujeto Excluido** | v1 | ✅ PROCESADO | `20267…EYBC` |
| **15 Donación** | v2 | ✅ PROCESADO | `20262…QD7Z` |
| 03 CCF · 05 NC · 06 ND | v3/v4 | ⏸️ esquema OK, requiere receptor con NRC | — |
| 07 Retención · 08 Liquidación · 09 DCL | v2 | ⏳ por implementar (requieren NRC) | — |

| Evento | Endpoint | Estado |
|---|---|---|
| **Contingencia** | `/fesv/contingencia` | ✅ PROCESADO |
| **Invalidación** | `/fesv/anulardte` | ✅ PROCESADO |
| **Operaciones Especiales** | `/fesv/recepciondte` | 🟡 estructura OK — bloqueo `095` (autorización de cuenta) |
| **Retorno** | `/fesv/recepciondte` | 🟡 estructura OK — bloqueo `codEstableMH` real |

> Los DTE/eventos en estado **🟡** ya **pasan la validación de esquema** de Hacienda; lo que
> falta para PROCESADO depende de **datos/autorizaciones de la cuenta de prueba** (NRC del
> receptor, código de establecimiento MH registrado, autorización Factura Simplificada/Control
> Interno) — **no de código**.

## Migraciones aplicadas
`InitialCreate` · `Sprint1_CoreCatalogosYSeguridad` · `Sprint3_ClientesYProductos` ·
`Sprint35_MunicipiosES` · `Sprint4_DteConfiguracion` · `Sprint5_DteDocumentos` ·
`Sprint9_RetransmisionTracking` · `Sprint10_DteCorrelativos` · `Sprint12_DistritoCAT008` ·
`Sprint13_CatalogosExtendido` · `Sprint13_PermisosCatalogos` · `Sprint13_SeedCatalogosMH` ·
`Sprint13_CatalogosMhOficial` · `Sprint14_CertificacionDte` · `Sprint14_PermisosCertificacion` ·
`Sprint15_DteEventos` · `Sprint15_PermisoEventos` · `Sprint15_CertificacionPruebaEvento` ·
`Sprint16_ContingenciaLotes` · `Sprint17_DiagnosticoErrores` · `Sprint17_SeedErrorCatalogo` · `Sprint18_LegalConsentimiento` · `Sprint19_BillingSelfService` · `Sprint20_HardeningSchema` · `PagosLatam_MetodosPago` · `Sprint24_NeoConnectSchema` · `Sprint22_NeoProfit` · `Sprint26_NotaInternaDte` · `Branding_LogoFirmaEmpresa` · `B2_CobranzaPagosCliente` · `B3_NeoScanAI` · `B4_AlertasNotificaciones` · `B5_CuentasCobroQr` · `Followups_EtiquetaCliente`. (UI/UX, Lookups, Carga masiva y Onboarding no requirieron migración.)

## SuperAdmin inicial
`superadmin` / `ChangeMe!2026` (cambiar en el primer login). El SuperAdmin no pertenece a
ninguna empresa; opera pantallas multi-tenant en **modo soporte** (selecciona empresa, se
guarda en cookie, `IEmpresaContext` scope los queries).

---

# 4. Base de datos

## Tablas actuales (36)

### Core / Administración
| Tabla | Contenido |
|---|---|
| `Core_Empresas` | Empresas (NIT, NRC, razón social, **departamento/municipio/distrito**, dirección, correo, teléfono) |
| `Core_Usuarios` | Usuarios (BCrypt password, estado) |
| `Core_Roles` / `Core_RolPermisos` / `Core_UsuarioRoles` | RBAC |
| `Core_Permisos` | Permisos del sistema (ej. `DTE.Emitir`, `DTE.Invalidar`) |
| `Core_Planes` / `Core_PlanModulos` | Planes comerciales y sus módulos |
| `Core_Modulos` | Catálogo de módulos del sistema |
| `Core_EmpresaPlan` / `Core_EmpresaModulos` | Licenciamiento por empresa |
| `Core_Sucursales` / `Core_PuntosVenta` | Establecimientos y puntos de venta |
| `Core_RefreshTokens` | Tokens JWT de refresco |
| `Core_Catalogos` / `Core_CatalogoItems` | Catálogos genéricos (incluye CAT MH) |
| `Core_Auditoria` | Bitácora de acciones (módulo, acción, resultado, detalle, entidadId) |

### DTE
| Tabla | Contenido |
|---|---|
| `Dte_Configuracion` | Config fiscal por empresa (1-a-1). Secretos cifrados con DataProtection |
| `Dte_Clientes` | Receptores (NIT/DUI, NRC, actividad, **DistritoCodigo**) |
| `Dte_Productos` | Productos/servicios (BIEN/SERVICIO, IVA, unidad medida) |
| `Dte_Documentos` | Cabecera DTE (tipo, numeroControl, codigoGeneracion, estado, sello, contingencia, **ReceptorDistritoCodigo**) |
| `Dte_DocumentoDetalles` | Líneas del DTE |
| `Dte_DocumentoJson` | JSON sin firmar, JWS firmado, respuesta cruda de Hacienda |
| `Dte_Correlativos` | Contador atómico de `NumeroControl` por (empresa, tipoDte) |

### DTE — Diagnóstico (Sprint 17)
| Tabla | Contenido |
|---|---|
| `Dte_ErrorCatalogo` | Catálogo de errores MH e internos con descripción amigable, causa probable y acción sugerida |
| `Dte_ErrorOcurrencias` | Ocurrencias concretas de error por empresa, con link a documento/evento, JSON enviado/respuesta y flag `Resuelta` |

### NeoConnect API (Sprint 24)
| Tabla | Contenido |
|---|---|
| `Connect_ApiKeys` | API Keys por empresa: `KeyHash` (SHA-256, único), `Prefix` visible, `Scopes`, `Activo`, `ExpiresAt`, `UltimoUsoAt`, `RevokedAt/By` |
| `Connect_Webhooks` | Endpoints suscritos a eventos DTE: `Url`, `SecretoHmac` (firma), `Eventos` (CSV), `ApiKeyId` opcional, `UltimaEntregaAt` |
| `Connect_WebhookDeliveries` | Entregas de webhook: `Evento`, `Payload`, `Estado` (PENDIENTE/ENTREGADO/FALLIDO), `HttpStatus`, `Intentos`, `ProximoIntento` (backoff) |

### NeoProfit (Sprint 22)
| Tabla | Contenido |
|---|---|
| `Profit_Gastos` | Gastos operativos por empresa (categoría, monto, fecha, IVA, descripción) que restan a la utilidad |
| `Profit_Compras` | Compras/costos por empresa (proveedor, monto, IVA, fecha) usados en el cálculo de utilidad |

### Cobranza / CxC (Backend Mobile B-2 / B-5)
| Tabla | Contenido |
|---|---|
| `Cobros_Pagos` | Pagos aplicados a DTE a crédito (`Monto`, `Fecha`, `Estado` Confirmado/PendienteRevision/Anulado, `Referencia`) — saldo y vencimiento calculados por `CobranzaCalculator` |
| `Cobros_CuentasCobro` | Cuentas/links de cobro con QR (`Monto`, `Referencia`, `Estado`, payload QR generado por `CobroQrService`) |

### NeoScanAI (Backend Mobile B-3)
| Tabla | Contenido |
|---|---|
| `Scan_Documentos` | Documentos escaneados (imagen/PDF base64, `Estado` RECIBIDO→…→CONFIRMADO/RECHAZADO, `Tipo`, campos extraídos por OCR pluggable) — al confirmar se convierten en gasto/compra/DTE recibido |
| `Dte_DocumentosRecibidos` | DTE recibidos de terceros (registro de compras a partir de un scan confirmado) |

### Notificaciones / Alertas (Backend Mobile B-4)
| Tabla | Contenido |
|---|---|
| `Notif_Alertas` | Alertas por empresa (`Tipo` DteRechazado/CertPorVencer/FacturaVencida/F07Proxima, `Severidad`, `Estado`, `Clave` para deduplicar) generadas por el Worker |
| `Notif_Dispositivos` | Dispositivos móviles registrados para push (token FCM, plataforma, activo) |
| `Notif_Preferencias` | Preferencias de notificación por usuario/empresa |

### Branding (columnas en `Core_Empresas`)
> `LogoBlob`, `FirmaBlob`, `FirmaTexto` agregadas a `Core_Empresas` (migración `Branding_LogoFirmaEmpresa`) — usadas en el PDF (banda + pie) y en el correo (logo CID).

## Tablas propuestas (por módulo pendiente)

| Módulo | Tablas |
|---|---|
| ~~**Eventos DTE**~~ | ✅ Sprint 15 — `Dte_Eventos`, `Dte_EventoJson`, `Dte_EventoRespuestasHacienda`, `Dte_EventoDocumentosRelacionados` + `CertificacionPrueba.EventoId` |
| ~~**Contingencia avanzada**~~ | ✅ Sprint 16 — `Dte_ContingenciaLotes`, `Dte_ContingenciaLoteDetalles`, `ContingenciaLoteService`, workers y UI |
| ~~**Diagnóstico errores Hacienda**~~ | ✅ Sprint 17 — `Dte_ErrorCatalogo`, `Dte_ErrorOcurrencias`, `DiagnosticoHaciendaService`, seed 11 errores, API + UI |
| ~~**Legal / Consentimiento**~~ | ✅ Sprint 18 — `Core_UserConsents`, `LegalDocumentService`, páginas `/legal/*`, checkbox en registro |
| ~~**Billing self-service**~~ | ✅ Sprint 19 — `Billing_Customers/Subscriptions/Payments/Invoices/WebhookEvents/PlanProviderMappings`, `BillingService`, providers Stripe/MercadoPago/Mock, webhooks idempotentes, activación licencias, emails transaccionales, UI `/billing` |
| ~~**Certificación**~~ | ✅ Sprint 14 — `Dte_CertificacionMatriz/Escenarios/Pruebas/Errores` con seed 625 escenarios |
| ~~**Catálogos MH**~~ | ✅ Sprint 13 — `Core_Catalogos.Version`/`MetadataJson` y `Core_CatalogoItems.ParentCodigo` agregados |
| ~~**NeoProfit**~~ | ✅ Sprint 22 — `Profit_Gastos`, `Profit_Compras`, `ProfitCalculator` puro + `IProfitService`, API `/api/profit/*`, dashboard + grids CRUD |
| ~~**NeoScanAI**~~ | ✅ Backend Mobile B-3 — `Scan_Documentos`, `Dte_DocumentosRecibidos`, `IScanService` (OCR pluggable mock), bandeja + conversión a gasto/compra/DTE recibido, API `/api/scanai/*` |
| ~~**Cobranza / CxC**~~ | ✅ Backend Mobile B-2/B-5 — `Cobros_Pagos`, `Cobros_CuentasCobro`, `ICobranzaService`/`ICobroQrService`, API `/api/cobros/*` |
| ~~**Alertas / Notificaciones**~~ | ✅ Backend Mobile B-4 — `Notif_Alertas`, `Notif_Dispositivos`, `Notif_Preferencias`, `IAlertaService` + generación en Worker + push pluggable, API `/api/alertas/*` |
| ~~**NeoConnect**~~ | ✅ Sprint 24 (en curso) — `Connect_ApiKeys`, `Connect_Webhooks`, `Connect_WebhookDeliveries` (logs de uso reusan `Core_ApiUsageLog` vía `ApiKeyId`). Pendiente sandbox dedicado. |
| **NeoPOS** | `Pos_Cajas`, `Pos_Aperturas`, `Pos_Ventas`, `Pos_VentaDetalles`, `Pos_Pagos`, `Pos_Cierres` |
| **NeoPortal** | `Portal_Accesos`, `Portal_Solicitudes`, `Portal_TokensPublicos` |
| **NeoCloud Mobile** | Backend ✅ (B-1…B-6) reusa `Notif_Dispositivos` para push; falta solo la app Flutter cliente |
| **Billing** | `Billing_Customers`, `Billing_Subscriptions`, `Billing_Payments`, `Billing_Invoices`, `Billing_WebhookEvents`, `Billing_PlanProviderMappings` |
| **Legal** | `Core_UserConsents` |
| ~~**Hardening**~~ | ✅ Sprint 20 — `Ops_BackupJobs`, `Core_ApiUsageLog`, `Core_ApiQuotas`, `Core_AdminIpAllowlist` + columnas MFA en `Core_Usuarios`; rate limiting (429), MFA TOTP SuperAdmin, IP allowlist, backups + worker, panel `/Hardening`, k6/ZAP/DR |

---

# 5. Funcionamiento detallado DTE / Hacienda ⭐

> Esta es la sección clave para entender cómo opera el corazón del sistema. Explica el ciclo
> de vida completo de un DTE, la firma, el JSON, el envío a Hacienda, los estados, la
> contingencia y los eventos (incluido el retorno).

## 5.1 ¿Qué es un DTE?

Un **Documento Tributario Electrónico** es un archivo **JSON** firmado electrónicamente (JWS)
que reemplaza al papel fiscal en El Salvador. Cada tipo tiene un código (CAT-002):

| Código | Documento | IVA |
|---|---|---|
| 01 | Factura (Consumidor Final) | **incluido** en el precio |
| 03 | Comprobante de Crédito Fiscal (CCF) | **separado** del precio |
| 04 | Nota de Remisión | traslado de bienes |
| 05 | Nota de Crédito | separado (resta) |
| 06 | Nota de Débito | separado (suma) |
| 07 | Comprobante de Retención | IVA retenido |
| 08 | Comprobante de Liquidación | comisiones |
| 09 | Documento Contable de Liquidación | liquidación a afiliados |
| 11 | Factura de Exportación | 0% (tributo `C3`) |
| 14 | Factura de Sujeto Excluido | sin IVA (no sujeta) |
| 15 | Comprobante de Donación | donación |

## 5.2 Ciclo de vida (máquina de estados)

```
BORRADOR → GENERADO → VALIDADO → FIRMADO → ENVIADO → PROCESADO
                                                    ↘ RECHAZADO   (re-emisión)
                                                    ↘ CONTINGENCIA (reintento por Worker)
                                                    ↘ ERROR
PROCESADO → INVALIDADO  (vía Evento de Invalidación)
```

| Estado | Significado |
|---|---|
| `BORRADOR` | Creado, aún editable |
| `GENERADO` | JSON construido |
| `VALIDADO` | Campos obligatorios verificados localmente |
| `FIRMADO` | JWS RS512 generado |
| `ENVIADO` | Transmitido a Hacienda, esperando respuesta |
| `PROCESADO` | Aceptado, con **selloRecibido** (es DTE legal) |
| `RECHAZADO` | MH lo rechazó (096 esquema, 095 autorización, etc.) |
| `CONTINGENCIA` | No se pudo transmitir; el Worker reintenta |
| `INVALIDADO` | Anulado vía evento de invalidación |
| `ERROR` | Falla técnica (firma, red, etc.) |

## 5.3 Construcción del JSON (DteGeneratorService)

`DteGeneratorService.Generar(DteDocumento, DteConfiguracion?)` arma el JSON según el tipo.
Bloques comunes:

- **`identificacion`**: `version` (entero, **1** factura/FSE, **3** CCF/NC/ND/NR, **2** Donación),
  `ambiente` (`00` pruebas / `01` producción), `tipoDte`, **`numeroControl`**, `codigoGeneracion`
  (UUID v4 mayúsculas), `tipoModelo` (1 previo / 2 diferido), `tipoOperacion` (1 normal / 2
  contingencia), `tipoContingencia`, `motivoContin`, `fecEmi`, `horEmi`, `tipoMoneda` (USD).
- **`emisor`**: NIT, NRC, nombre, actividad, dirección. Difiere por tipo:
  - Factura v1: con `extension`, `codEstableMH`, `tipoEstablecimiento`.
  - FSE: **sin** `tipoEstablecimiento`/`nombreComercial`, pero **con** `codEstableMH`.
  - Exportación/Donación v2/v3: con `distrito` (división 2024), sin variantes `*MH`.
- **`receptor`**: depende del tipo. Factura puede ser null (consumidor final); CCF/NC/ND requieren
  NIT + **NRC** + actividad económica.
- **`cuerpoDocumento`**: líneas. Factura lleva `ivaItem`; CCF/NC/ND **no** (IVA va en resumen).
- **`resumen`**: totales. Factura usa `ivaRete1`/`reteRenta`; CCF usa `ivaPerci1`; Exportación
  usa tributo `C3`.

### El `numeroControl` (formato crítico)

```
DTE-{tipoDte}-{bloqueEstablecimiento}-{correlativo 15 dígitos}
```
El **bloque de establecimiento** NO es `[A-Z0-9]{8}` (error inicial). El formato oficial es:
```
(M|B|S|P)([0-9]{3})(P)([0-9]{3})   →   M001P001
```
- Letra = tipo de establecimiento (CAT-009): **M**=Casa Matriz, **S**=Sucursal, **B**=Bodega, **P**=Predio.
- 3 dígitos de codEstable + literal `P` + 3 dígitos de codPuntoVenta.
- Ej: `DTE-01-M001P001-000000000000014`.

Lo construye `BuildBloqueEstablecimiento()`. El correlativo viene de `Dte_Correlativos`
(UPSERT atómico, evita race conditions).

### División territorial por versión (clave)

- **DTE v1** (Factura, FSE, NR): división **vieja** → Ayutuxtepeque = `municipio 03`, **sin distrito**.
- **DTE v2/v3** (Donación, Exportación): división **2024** → San Salvador Centro = `municipio 23`
  + `distrito 03` (Ayutuxtepeque).

Por eso la columna `Distrito` (CAT-008) está dormante para v1 pero **se usa** en v2/v3.

## 5.4 Firma electrónica — JWS RS512

`HaciendaCertMhDteSignerService` firma con el certificado **CertificadoMH XML** (`.crt`) emitido
por el portal de Hacienda (contiene clave privada PKCS#8 y pública SubjectPublicKeyInfo, **sin
password**).

**El estándar es RS512** (RSA + SHA-512), idéntico al `svfe-api-firmador` oficial. El header del
JWS es **mínimo**: `{"alg":"RS512"}` (sin `typ`, sin `x5t`).

```
JWS = base64url(header) . base64url(payload) . base64url(firma)
firma = RSA-SHA512( header_b64 + "." + payload_b64 )
```

El firmador hace **auto-verificación local** con la clave pública antes de devolver el JWS.

> ⚠️ **Guardrail anti-mock:** si el cliente de recepción es el real (`HttpHaciendaReceptionClient`)
> y el JWS tiene header `alg:none-mock`, se **bloquea el envío** (`FIRMA_MOCK_NO_ENVIABLE`) — evita
> mandar basura a Hacienda y desperdiciar intentos de la matriz.

## 5.5 Autenticación con Hacienda

```
POST {base}/seguridad/auth   (form-urlencoded: user + pwd)
→ body.token  (¡YA incluye el prefijo "Bearer "!)
```
`HttpHaciendaAuthClient` **recorta el prefijo** antes de cachear el token (cifrado con
DataProtection en `Dte_Configuracion`). El token se refresca automáticamente con 5 min de margen.

> Hay **dos passwords distintos**: el del **portal** (login web) ≠ el de la **API de recepción**
> (`passwordMh`). En la configuración DTE va el de la API.

## 5.6 Transmisión y respuesta

```
POST {base}/fesv/recepciondte
Headers: Authorization: Bearer {token}
Body:    { ambiente, idEnvio, version, tipoDte, documento(JWS), codigoGeneracion }
```

Respuesta de Hacienda:
```json
{ "estado":"PROCESADO", "selloRecibido":"2026…", "codigoMsg":"001",
  "descripcionMsg":"RECIBIDO", "observaciones":[] }
```

Mapeo a estado interno: `PROCESADO`→guarda sello+`ProcesadoAt`; `RECHAZADO`→permite re-emisión;
`CONTINGENCIA`→Worker reintenta. Errores externos → HTTP 502.

**Códigos de mensaje frecuentes:**
- `001` RECIBIDO · `002` RECIBIDO CON OBSERVACIONES (aviso, no rechazo)
- `006` campo FORMATO NO VÁLIDO · `095` contribuyente no autorizado · `096` no cumple normativa
  (esquema) · `802` firma no válida

## 5.7 Contingencia (3 momentos)

Cuando no se puede transmitir (fuerza mayor), se opera en **modelo diferido**:

- **MOMENTO 1** — Emitir DTE en contingencia: `tipoTransmision=2`, `tipoModelo=2` (diferido),
  `tipoContingencia` (CAT-005), `motivoContin` (requerido si tipo=5). Se guardan localmente
  (estado `CONTINGENCIA`).
- **MOMENTO 2** — Transmitir el **Evento de Contingencia** (≤ 24 h tras restablecer conexión):
  lista los `codigoGeneracion` de los DTE en contingencia. `POST /fesv/contingencia` → sello.
  - Emisor del evento usa esquema **asimétrico**: `codEstableMH` (con MH) + `codPuntoVenta` (sin MH).
- **MOMENTO 3** — Transmitir el **lote** de los DTE informados (≤ 72 h del sello del evento) vía
  `/fesv/recepcionlote` → cada DTE obtiene su sello individual. Se consulta con
  `/fesv/recepcion/consultadtelote/{codigoLote}`.

El `RetransmisionContingenciaWorker` automatiza el reintento de documentos en `CONTINGENCIA`
(intervalo, cooldown, máx. intentos, lote máximo).

## 5.8 Eventos DTE

Cuatro eventos. **Solo dos tienen endpoint propio** (Manual Técnico v2.0):

| Evento | tipoEvento | Esquema | Endpoint | Notas de apitest |
|---|---|---|---|---|
| **Invalidación** | — | `invalidacion-schema` | `/fesv/anulardte` | usa `fecAnula`/`horAnula`; `nomEstablecimiento` requerido; tipo 1/3 requieren documento de reemplazo (`codigoGeneracionR`), tipo 2 = rescindir (solo motivo) |
| **Contingencia** | — | `contingencia-schema` | `/fesv/contingencia` | ver 5.7 |
| **Operaciones Especiales** | 17 | `fe-eop` | `/fesv/recepciondte` | reporta Factura Simplificada / Control Interno; `tipoDocumento=97` (Control Interno); requiere autorización de cuenta |
| **Retorno** | 18 | `fe-eret` | `/fesv/recepciondte` | aplica a FE/FEXE/FSEE; referencia el DTE origen; requiere `codEstableMH` real registrado |

> Los esquemas con prefijo **`fe-`** (eop/eret) se transmiten como documentos por
> `/fesv/recepciondte`; los con sufijo **`-schema`** (contingencia/invalidación) tienen endpoint
> dedicado. Clientes en código: `IHaciendaContingenciaClient` (dedicado) + `IHaciendaEventoClient`
> (genérico, endpoint parametrizable).

## 5.9 Representación gráfica (PDF + QR)

`DtePdfService` (QuestPDF) genera el PDF con el **código QR** que apunta a la consulta pública del
DTE en el portal MH (`codigoGeneracion` + fecha). Se entrega al receptor junto al JSON.

## 5.10 Lecciones de integración real (resumen)

| Problema MH | Causa | Solución |
|---|---|---|
| 401 en recepción | token con `"Bearer "` duplicado | recortar prefijo |
| 802 firma no válida | firmábamos RS256 | usar **RS512** header `{"alg":"RS512"}` |
| numeroControl formato | asumíamos `[A-Z0-9]{8}` | `(M\|B\|S\|P)(3)P(3)` → `M001P001` |
| codEstableMH tamaño | enviábamos 3 dígitos | **4 caracteres** (`0001`) |
| receptor.tipoDocumento | código interno | mapear a **CAT-022** (DUI→13, NIT→36) |
| Factura v2 rechazada | apitest valida **v1** | emitir v1/v3, no v2/v4 |
| Web firmaba mock | faltaba `Dte:Signer` en Web Local | fijar `HaciendaCert` + guardrail |

---

# 6. Catálogos MH y módulo de mantenimiento

## 6.1 Catálogos oficiales (CAT-001 a CAT-033)

| Código | Catálogo | Estado |
|---|---|---|
| CAT-001 | Ambiente destino | ✅ |
| CAT-002 | Tipo Documento / Evento | ⚠️ parcial |
| CAT-003 | Modelo Facturación | ⚠️ |
| CAT-004 | Tipo Transmisión | ⚠️ |
| CAT-005 | Tipo Contingencia | ✅ Sprint 13 (5/5 oficial) |
| CAT-006 | Retención IVA | ✅ Sprint 13.7 (3/3 oficial) |
| CAT-007 | Tipo Generación Documento | ⚠️ |
| CAT-008 | **Distrito** (división 2024) | ⚠️ catálogo registrado Sprint 13; cargar vía import |
| CAT-009 | Tipo Establecimiento | ✅ |
| CAT-010 | Código Servicio Médico | ❌ |
| CAT-011 | Tipo Ítem | ⚠️ |
| CAT-012 | Departamento | ✅ (14, falta `00` extranjero) |
| CAT-013 | **Municipio** (división 2024) | ⚠️ 42 sembrados, faltan códigos MH reales (44) |
| CAT-014 | Unidad Medida | ✅ Sprint 13.7 (56 oficial completo, Codigo=codigoMH) |
| CAT-015 | Tributos | ⚠️ Sprint 13 (12 subset operativo; resto vía import) |
| CAT-016 | Condición Operación | ✅ |
| CAT-017 | Forma Pago | ✅ |
| CAT-018 | Plazo | ✅ Sprint 13.7 (3/3 oficial) |
| CAT-019 | Actividad Económica | ⚠️ Sprint 13 (17 top-level CIIU; resto vía import) |
| CAT-020 | País | ✅ Sprint 13.7 (275 oficial legacy v1.4) |
| CAT-021 | Otros Documentos Asociados | ✅ Sprint 13.7 (4/4 oficial) |
| CAT-022 | Tipo Documento Identificación | ✅ Sprint 13.7 (5/5 oficial, Codigo=codigoMH) |
| CAT-023 | Tipo Doc. en Contingencia | ✅ Sprint 13.7 (7/7 oficial) |
| CAT-024 | Motivo Evento (invalidación) | ✅ Sprint 13.7 (3/3 oficial con textos exactos) |
| CAT-025 | Título Remisión Bienes | ✅ Sprint 13.7 (5/5 oficial) |
| CAT-026 | Tipo Donación | ✅ Sprint 13.7 (3/3 oficial) |
| CAT-027 | Recinto Fiscal | ✅ Sprint 13.7 (45 oficial, Z.F. EMCO y Gigante incluidas) |
| CAT-029 | Tipo Persona | ✅ Sprint 13.7 (2/2 oficial) |
| CAT-030 | Transporte | ✅ Sprint 13.7 (7/7 oficial) |
| CAT-031 | INCOTERMS | ✅ Sprint 13.7 (16/16 oficial) |
| CAT-032 | Domicilio Fiscal | ✅ Sprint 13.7 (2/2 oficial) |
| CAT-025 | Título que remiten los bienes | ⚠️ |
| CAT-026 | Tipo Donación | ⚠️ |
| CAT-027 | Recinto Fiscal | ❌ |
| CAT-028 | Régimen | ❌ |
| CAT-029 | Tipo Persona | ⚠️ |
| CAT-030 | Transporte | ❌ |
| CAT-031 | INCOTERMS | ❌ |
| CAT-032 | Domicilio Fiscal | ⚠️ |
| CAT-033 | Tipo Régimen | ❌ |

> **Deuda técnica conocida:** varios catálogos están parciales o hardcodeados en los builders
> (ej. municipio `23`/distrito `03` para la empresa de prueba). Falta sembrar los catálogos
> completos con códigos MH oficiales y **derivar** los valores en vez de hardcodear.

## 6.2 Módulo de Mantenimiento de Catálogos — ✅ IMPLEMENTADO (Sprint 13) ⭐

Pantalla/API de administración para **gestionar todos los catálogos** del sistema sin recompilar.

**Funciones entregadas:**
- ✅ **Listar** catálogos y sus ítems con cascada por `ParentCodigo` (UI + API).
- ✅ **Crear / Editar** catálogos e ítems con auditoría (`IAuditoriaService`).
- ✅ **Borrar** ítems con reglas: `EsSistema` no se elimina (soft); con hijos no se elimina (regla de integridad referencial por código).
- ✅ **Importar** CSV/JSON/XLSX con modos `Upsert` / `InsertOnly` y `dryRun`. Reporte de filas con errores.
- ✅ **Exportar** CSV (UTF-8 con BOM para Excel) / JSON / XLSX. Filename con versión.
- ✅ **Versionar** catálogos (`Catalogo.Version`, default 1, índice único filtrado `(Codigo, EmpresaId, Version)`).
- ✅ **Cascadas territoriales** (Departamento → Municipio → Distrito) con `CatalogoItem.ParentCodigo`.
- ✅ **Metadata** por ítem (`MetadataJson`: `codigoMH`, `zona`, etc.).
- ✅ **Auditoría** (CREATE/UPDATE/DELETE_ITEM/IMPORT vía `IAuditoriaService`).
- ✅ **Multi-tenant**: catálogos del sistema (EmpresaId null, solo SuperAdmin) + de empresa.

**Endpoints entregados:**
```
GET    /api/catalogos                          # listado (Core.Catalogos.Ver)
GET    /api/catalogos/{codigo}                 # detalle del catálogo
GET    /api/catalogos/{codigo}/items?parent=   # cascada (hijos / __ROOT__ / todos)
POST   /api/catalogos                          # crear (Core.Catalogos.Administrar)
PUT    /api/catalogos/{codigo}                 # editar
POST   /api/catalogos/{codigo}/items           # crear ítem
PUT    /api/catalogos/{codigo}/items/{id}      # editar ítem
DELETE /api/catalogos/{codigo}/items/{id}      # eliminar ítem
POST   /api/catalogos/{codigo}/import          # importar (Core.Catalogos.Importar)
GET    /api/catalogos/{codigo}/export?format=  # exportar csv|json|xlsx
```

**UI Web (MVC):** `/Catalogos` (lista), `/Catalogos/Details/{codigo}` (ítems + filtro de padre +
dropdown exportar), `/Catalogos/Import/{codigo}` (upload con simulación), `/Catalogos/Export/{codigo}`.

**Tabla actualizada:** `Core_Catalogos` ahora tiene `Version` (int, default 1), `MetadataJson`.
`Core_CatalogoItems` ahora tiene `ParentCodigo` (nvarchar(50) nullable). Índices nuevos:
`IX_Core_Catalogos_Codigo_EmpresaId_Version` (único filtrado), `IX_Core_Catalogos_Codigo_EmpresaId_Activo`,
`IX_Core_CatalogoItems_CatalogoId_ParentCodigo`.

**Permisos:** `Core.Catalogos.Ver` (311), `Core.Catalogos.Administrar` (310), `Core.Catalogos.Importar` (312).
SUPERADMIN/ADMIN tienen los 3; OPERADOR/CONTADOR/READONLY tienen solo `Ver`.

**Tests:** 31 nuevos (5 esquema, 14 admin, 12 import/export).

---

# 7. Módulos de mantenimiento del Suite

Además de catálogos, el suite necesita **módulos de mantenimiento transversales** para operar
sin tocar código ni BD a mano:

| Módulo de mantenimiento | Qué administra | CRUD / Import / Export |
|---|---|---|
| **Catálogos** (⭐ §6.2) | Todos los catálogos MH + internos | CRUD + Import/Export + versionado |
| **Planes y Módulos** | Planes comerciales, módulos, límites, precios | CRUD + clonar plan |
| **Roles y Permisos** | Matriz de permisos por rol; permisos por módulo | CRUD + matriz visual |
| **Parámetros del sistema** | Toggles, límites globales, feature flags | Editar sin recompilar |
| **Plantillas** | Correos transaccionales, PDF, textos legales | CRUD + preview |
| **Territorial** | Departamentos/Municipios/Distritos en cascada | Import oficial MH |
| **Certificados** | Carga/renovación de certificados MH por empresa | Subir/reemplazar/validar vigencia |
| **Numeración / Correlativos** | Reset/ajuste de correlativos por tipo DTE | Editar con auditoría |
| **Usuarios y empresas** (existe) | Alta/baja/edición | CRUD (mejorar UI) |
| **Datos maestros DTE** | Productos, clientes (existe) | CRUD + **carga masiva** (pendiente) |
| **Backups / Mantenimiento BD** | Jobs de respaldo, limpieza, reindex | Programar/ejecutar |

**Principio:** todo dato que cambie por normativa, negocio o cliente debe ser **editable desde la
UI con auditoría**, no hardcodeado. Hoy hay hardcodeos (municipio/distrito en builders, mapeos de
catálogo) que deben migrar a estos módulos de mantenimiento.

---

# 8. Catálogo de endpoints

## Actuales (implementados)

**Auth:** `POST /api/auth/{login|refresh|logout|change-password}` · `GET /api/auth/me`
**Usuarios:** `GET/POST/PUT /api/usuarios` · `PATCH .../bloquear|desbloquear` · `POST .../reset-password`
**Roles:** `GET/POST/PUT /api/roles` · `GET /api/roles/permisos`
**Empresas:** `GET/POST/PUT /api/empresas` · `GET .../licencia` · `POST .../plan` · `POST .../modulos/{id}/activar|desactivar`
**Sucursales/PV:** `GET/POST/PUT /api/sucursales` · `/api/puntos-venta` · `PATCH .../inactivar`
**Planes/Módulos:** `GET /api/planes` · `GET /api/modulos`
**Catálogos:** `GET/POST /api/catalogos` · `GET/PUT /api/catalogos/{codigo}` · `GET/POST/PUT/DELETE /api/catalogos/{codigo}/items` (`?parent=` cascada) · `POST /api/catalogos/{codigo}/import` · `GET /api/catalogos/{codigo}/export?format=csv|json|xlsx`
**Certificación DTE:** `GET /api/certificacion/{resumen|matriz|errores}` · `GET /api/certificacion/tipos/{codigo}/escenarios` · `POST /api/certificacion/tipos/{codigo}/generar-prueba` · `POST /api/certificacion/documentos/{id}/{marcar-completado|reintentar}` · `POST /api/certificacion/eventos/{id}/marcar-completado`
**Eventos DTE:** `GET /api/dte/eventos?tipo=&estado=` · `GET /api/dte/eventos/{id}` · `GET /api/dte/eventos/{id}/json` · `GET /api/dte/eventos/{id}/pdf` · `POST /api/dte/eventos/{invalidacion|contingencia|retorno|operaciones-especiales}` (legacy `POST /api/dte/evento/{...}` se mantiene como adapter)
**Clientes/Productos:** `GET/POST/PUT /api/clientes` · `/api/productos` · `PATCH .../inactivar`
**Config DTE:** `GET/PUT /api/dte/configuracion` · `POST .../certificado` · `DELETE .../certificado` · `POST .../probar-conexion`
**Documentos DTE:** `GET /api/dte/documentos` · `POST /api/dte/{factura|credito-fiscal|nota-credito|nota-debito|sujeto-excluido|documentos}` · `POST .../{id}/{generar|validar|firmar|enviar|invalidar|reenviar}` · `GET .../{id}/{pdf|json}`
**DTE emisión 1-paso (Mobile B-1):** `POST /api/dte/emitir` (orquesta borrador→generar→validar→firmar→enviar vía `IConnectDteService`) · atajos `POST /api/dte/emitir/{factura|credito-fiscal|nota-credito|nota-debito|sujeto-excluido}`
**Eventos DTE:** `POST /api/dte/evento/{contingencia|invalidacion|operaciones-especiales|retorno}`
**Cobros / CxC (Mobile B-2/B-5):** `GET /api/cobros` (cuentas por cobrar + saldos/vencimiento) · `GET /api/cobros/{id}` · `GET/POST /api/cobros/{id}/pagos` · `PATCH /api/cobros/pagos/{id}/{confirmar|anular}` · `GET/POST /api/cobros/cuentas` · `GET /api/cobros/cuentas/{id}` · `GET /api/cobros/cuentas/{id}/qr` (QR de cobro vía `ICobroQrService`)
**NeoScanAI (Mobile B-3):** `GET/POST /api/scanai/documentos` · `GET /api/scanai/documentos/{id}` · `POST .../{id}/{confirmar|rechazar}` (al confirmar → gasto/compra/DTE recibido) — `[RequireModule("NEOSCANAI")]`, permisos `ScanAI.Ver/.Confirmar`
**Alertas (Mobile B-4):** `GET /api/alertas` · `PATCH /api/alertas/{id}/{leer|descartar}` · `GET/POST /api/alertas/dispositivos` · `DELETE /api/alertas/dispositivos/{id}` · `GET/PUT /api/alertas/preferencias`
**Lookups:** `GET /api/lookups/*` (catálogos, cascada territorial, maestros) · `GET /api/lookups/verificar-nit` (formato NIT/DUI + lookup local, hook MH, Mobile B-6)
**Profit (Sprint 22):** `GET /api/profit/{dashboard|productos|clientes|sucursales|tendencia|gastos|compras}` · `POST/PUT/DELETE /api/profit/{gastos|compras}` (`[RequireModule("NEOPROFIT")]`, permisos 370/371)
**Branding:** `GET/PUT /api/branding` · `POST/DELETE .../{logo|firma}` (logo+firma por empresa)
**Dashboard:** `GET /api/dashboard/empresa` · `GET /api/dashboard/superadmin`
**NeoConnect (Sprint 24):** `GET/POST /api/connect/api-keys` · `GET /api/connect/api-keys/{id}` · `PATCH /api/connect/api-keys/{id}/revoke` · `GET/POST /api/connect/webhooks` · `GET /api/connect/webhooks/{id}` · `DELETE /api/connect/webhooks/{id}` · `POST /api/connect/webhooks/{id}/test` · `GET /api/connect/logs` · `GET /api/connect/usage` (todos JWT + permisos `Connect.*`). Auth de integradores externos por header **`X-Api-Key`** vía `ApiKeyAuthMiddleware`.
**NeoConnect negocio v1:** `GET /api/v1/ping` · `POST /api/v1/dte` (emitir extremo-a-extremo) · `GET /api/v1/dte` · `GET /api/v1/dte/{id}` · `GET .../{id}/{pdf|json}` · `GET|POST /api/v1/clientes` · `GET|POST /api/v1/productos` (auth por API Key + scope por endpoint)
**Diagnóstico / Docs:** `GET /health` · `GET /openapi/v1.json` (siempre) · explorador interactivo **Scalar** en `/scalar/v1`

## Propuestos (módulos pendientes)

**NeoPOS:** `GET /api/pos/cajas` · `POST .../{apertura|cierre}` · `GET/POST /api/pos/ventas` · `POST .../{id}/emitir-dte`
**NeoPortal:** `GET /portal/documentos/{codigoGeneracion}/{pdf|json}` · `POST .../solicitud` · `GET /api/portal/clientes/{id}/{documentos|estado-cuenta}`
**Mobile:** `GET /api/mobile/devices` · `POST .../register` · `PATCH .../{authorize|revoke}`
**Billing:** `GET/POST /billing/{checkout|portal|change-plan}` · `POST /api/billing/webhooks/{stripe|mercadopago}`
**Legal:** `GET /legal/{terms|privacy|cookies|dpa}`

---

# 9. Módulos del Suite

> Estado y pendientes por módulo (síntesis del contexto maestro).

| # | Módulo | Estado | Pendientes clave | Prioridad |
|---|---|---|---|---|
| 1 | **Core / Administración** | ✅ avanzado | MFA SuperAdmin/IP allowlist ✅; onboarding self-service ✅ (checklist + asistente `/onboarding`); falta consumo por plan/upselling | Crítico |
| 2 | **NeoDTE** | ✅ avanzado | Hardcodeos territoriales eliminados (`TerritorialOptions`) ✅; falta sembrar CAT-008 Distrito para derivación 100% por catálogo | Crítico |
| 3 | **Certificación DTE** | ✅ Sprint 14 — módulo completo (matriz, progreso, escenarios, reintentos, errores) | Completar matriz oficial cuando MH publique descripción detallada por escenario | Media |
| 4 | **Eventos DTE** | ✅ Sprint 15 — persistencia + UI + PDF + integración certificación | Op-Especiales / Retorno aún bloqueados por autorización de cuenta MH | Media |
| 5 | **Contingencia/Worker** | ✅ Sprint 16 — MOMENTO 3 completo: lotes, consulta, worker, UI, API | — | Alta |
| 6 | **Clientes** | ✅ | Cascada Depto→Muni→Distrito + carga masiva Excel/CSV ✅; lookups vía `/api/lookups` | Alta |
| 7 | **Productos** | ✅ | Carga masiva Excel/CSV ✅; falta mapear unidad→CAT-014 y tributos por tipo | Media |
| 8 | **Catálogos MH** | ✅ Sprint 13 — módulo completo (CRUD/import/export/versión/cascada) | Sembrar resto vía import oficial (CAT-008 Distrito, CAT-019/020 completos) | Media |
| 9 | **Dashboard** | ✅ base | Integrar NeoProfit, certificación, alertas Hacienda | Alta |
| 10 | **NeoProfit / NeoBI** | ✅ Sprint 22 — `ProfitCalculator` puro + `IProfitService` + `/api/profit/*` + dashboard Web + grids/CRUD gastos/compras (permisos 370/371) | Snapshots mensuales + alertas de margen | Alta |
| 11 | **NeoScanAI** | ✅ Backend Mobile B-3 + **UI Web** (`ScanController` `/Scan`: bandeja, preview, corrección de campos, conversión a gasto/compra/DTE recibido, rechazo; permisos `ScanAI.Ver/.Confirmar`) + **DTE recibidos** (`IDteRecibidoService` + `DteRecibidosController` `/DteRecibidos`) + **OCR real Gemini** (`GeminiScanExtractionService`, M2.1, toggle `Scan:Provider=Gemini`) | — | Alta |
| 12 | **NeoConnect API** | ✅ Sprint 24 COMPLETO — API keys (hash SHA-256 + scopes), `X-Api-Key` middleware, webhooks firmados HMAC + worker, rate limit por ApiKey, UI `/Integraciones`, **endpoints de negocio v1** (`/api/v1`) + OpenAPI público + tests | Sandbox dedicado + más eventos | Media-alta |
| 13 | **NeoPOS** | ❌ | Caja, venta rápida, conversión a DTE | Media-alta |
| 14 | **NeoPortal Clientes** | ❌ | Consulta pública, estado de cuenta | Media |
| 15 | **NeoCloud Mobile** | ✅ backend (B-1…B-6) — emisión 1-paso, Cobros/CxC, ScanAI, Alertas/push, QR cobro, verificación NIT; docs `NeoCloud-Mobile-API.md` + `NeoCloud-Mobile-Plan.md` | App Flutter (cliente) por desarrollar; integraciones externas OCR/FCM/NIT-MH | Media |
| 16 | **Cobranza / CxC** | ✅ Backend Mobile B-2/B-5 — `ICobranzaService`/`ICobroQrService`, pagos + cuentas de cobro + QR, `/api/cobros/*` | UI Web (solo API hoy) | Media-alta |
| 17 | **Alertas / Notificaciones** | ✅ Backend Mobile B-4 + **UI Web** (`AlertasController` `/Alertas`: centro de notificaciones, recalcular, marcar leídas/resolver, preferencias; campana con badge en topbar vía `AlertasBadgeViewComponent`) + **Push FCM real** (`FcmPushSender` + `ServiceAccountTokenProvider`, M2.2, toggle `Push:Provider=Fcm`, desactiva tokens inválidos) — solo autenticado | — | Media |
| 18 | **SuperAdmin** | ✅ parcial | Billing, salud sistema, incidentes, churn, soporte | Alta |
| 19 | **Billing SaaS** | ✅ Sprint 19 + **Pagos LATAM** | Multi-proveedor (Wompi/PayPal/Transferencia/Stripe/MercadoPago), transferencia con verificación manual; falta cargar credenciales reales + monto por plan | Crítico (venta) |
| 20 | **Legal / Compliance** | ✅ Sprint 18 | Términos, privacidad, consentimiento | Crítico (venta) |
| 21 | **Hardening** | ✅ Sprint 20 | Rate limiting (429), MFA TOTP, IP allowlist, backups + worker, k6, OWASP ZAP, DR | Alto (pre-prod) |
| 22 | **UI/UX** | ✅ Sprint 21 | AppShell (`neostp.css`) + re-tema global + pantallas clave + pulido de todos los listados | Crítico (venta) |
| 23 | **Lookups / Datos** | ✅ | `ILookupService` + `/api/lookups`; carga masiva Excel/CSV de clientes y productos | Media |
| 24 | **Onboarding self-service** | ✅ | `IOnboardingService` (5 pasos derivados de datos reales) + checklist dashboard + asistente `/onboarding` | Alta (conversión) |
| 25 | **Branding** | ✅ | Logo + firma por empresa (`/branding`), usados en PDF (banda+pie) y correo (logo CID) | Media |

---

# 10. Plan de trabajo para completar la Suite

## Fase 1 — Certificación y cumplimiento DTE (CRÍTICO)
1. ✅ **Módulo de mantenimiento de Catálogos** (§6.2) — CRUD/Import/Export/versionado. **Sprint 13.**
2. ✅ **Catálogos MH oficiales** — Sprint 13.7 cargó el paquete completo Manual v1.4 (CAT-006/014/018/020/021/022/023/024/025/026/027/029/030/031/032 con `Codigo = codigoMH`). Solo CAT-008 Distrito queda como placeholder vacío (cargar via `/Catalogos/Import/DISTRITO_ES` cuando MH publique la lista oficial).
3. ✅ **Eliminar hardcodeos** (municipio/distrito en builders) → ahora salen de `TerritorialOptions` (`Dte:Territorial`) + distrito del emisor/documento; `LookupService.ResolverMunicipio2024Async` listo para derivación 100% por catálogo al sembrar CAT-008.
4. ✅ **Eventos DTE persistentes** (tablas `Dte_Eventos*`) + UI + PDF + integración con certificación. **Sprint 15.**
5. ✅ **Contingencia avanzada / Lotes** — MOMENTO 3: `DteContingenciaLote`/`DteContingenciaLoteDetalle`, `ContingenciaLoteService`, worker periódico, clientes HTTP reales + mocks, UI `/DteContingencia`. **Sprint 16.**
6. ✅ **Módulo de Certificación DTE** — matriz de progreso (15 tipos × 625 escenarios), generar prueba, reintentar, errores. **Sprint 14.**
6. **Completar matriz** (con datos de cuenta: NRC, codEstableMH real, autorizaciones).
7. **Diagnóstico de errores Hacienda** — pantalla que mapea códigos MH a explicaciones y acciones. *(Sprint 17)*

## Fase 2 — SaaS vendible (CRÍTICO comercial)
8. **Legal + consentimiento** (términos, privacidad, cookies, DPA, `Core_UserConsents`).
9. **Billing self-service** (trial 14 días, Stripe/MercadoPago, checkout, webhooks, activación auto de licencias).
10. **Emails transaccionales** (bienvenida, pago, vencimiento).
11. **SuperAdmin billing** (estado de pagos, suscripciones, churn).

## Fase 3 — Operación segura (pre-producción)
12. Backup off-site automático · k6 baseline · OWASP ZAP en CI.
13. Quotas API por plan/API Key · MFA SuperAdmin · IP allowlist · Disaster Recovery documentado.
14. Monitoreo + logs operativos + alertas.

## Fase 4 — Diferenciadores comerciales
15. **NeoProfit básico** (ventas, costos, márgenes, productos sin costo).
16. **NeoScanAI integrado** (bandeja, OCR/IA, registro compra/gasto/DTE recibido → alimenta NeoProfit).
17. **NeoConnect API comercial** (keys, webhooks, sandbox, docs públicas).
18. **UI/UX moderna** (§11).

## Fase 5 — Operación avanzada
19. NeoPOS · 20. NeoPortal Clientes · 21. NeoSTP Mobile · 22. Mobile Management.
23. Enterprise: SSO/SAML, SOC2/ISO, marca blanca, marketplace.

## Estado actual y plan comercial vigente (post-Sprint 21)

Tras cerrar los Sprints 13–21, se ejecutó un **plan re-secuenciado para priorizar la venta** en El Salvador. Estado:

| # | Iniciativa | Estado | Por qué |
|---|---|---|---|
| 1 | **Pagos LATAM** (Wompi · PayPal · Transferencia) | ✅ | Desbloquea cobro local (tarjeta Wompi + transferencia con verificación manual) |
| 2 | **Lookups + limpieza de hardcodeos** | ✅ | `ILookupService`/`/api/lookups`; sin literales territoriales mágicos |
| 3 | **Carga masiva** clientes/productos (Excel/CSV) | ✅ | Onboarding de datos de prospectos en minutos (upsert + dry-run + reporte) |
| 4 | **Onboarding self-service** | ✅ | `IOnboardingService` deriva 5 pasos de activación de datos reales (perfil, config DTE, certificado, catálogo base, primer DTE PROCESADO); checklist reactivo en el dashboard + asistente `/onboarding`; se oculta al 100% → sube conversión |
| 5 | **NeoConnect API comercial** | ✅ | Gestión (API keys + webhooks firmados + worker + UI `/Integraciones`) **y endpoints de negocio v1** (`/api/v1`: emitir/consultar/descargar DTE, alta/listado clientes y productos por API Key + scopes), sandbox por ambiente DTE, OpenAPI público + `docs/NeoConnect-API-v1.md` — **base para NeoBusiness y NeoScan**; COMPLETO (gestión + negocio + tests) |
| 6 | **NeoProfit** (Sprint 22) | ✅ | `ProfitCalculator` (PROCESADO, NC resta, ND suma, SE sin IVA, costo pendiente) + `IProfitService`/`/api/profit/*` + dashboard financiero Web + grids/CRUD de gastos y compras; permisos NEOPROFIT 370/371; migración `Sprint22_NeoProfit` |
| 7 | **Backend NeoCloud Mobile** (B-1…B-6) | ✅ | API lista para la app Flutter: emisión 1-paso, Cobros/CxC, NeoScanAI (OCR pluggable), Alertas/push, QR de cobro, verificación NIT; tenant-safe (mobile = empresa-only, sin SuperAdmin); docs `NeoCloud-Mobile-API.md` + `NeoCloud-Mobile-Plan.md`; explorador Scalar `/scalar/v1` |
| 8 | **NeoScanAI** | ✅ backend (B-3) + UI Web | Bandeja + conversión a gasto/compra/DTE recibido vía API (`/api/scanai/*`) y vía web (`/Scan`: bandeja, preview, corrección, conversión); OCR/IA real pendiente |
| 9 | **Plan V2** | 📋 | Roadmap de mejoras y nuevos módulos (paridad Web↔API, integraciones reales OCR/FCM, NeoPOS, Inventario, NeoBI, NeoPortal, App Flutter, etc.) — ver `docs/Plan-V2.md` |

**Checklist "vendible ya":** ✅ DTE certificado · ✅ Multiempresa/RBAC · ✅ Billing + pagos locales · ✅ Legal · ✅ Hardening · ✅ UI moderna · ✅ Lookups · ✅ Carga masiva · ✅ Onboarding self-service · ✅ NeoConnect (gestión + negocio) · ✅ NeoProfit · ✅ Backend Mobile (B-1…B-6) → **siguiente: ejecutar `docs/Plan-V2.md` (paridad Web↔API + app Flutter + integraciones reales)**.

### Detalle — Onboarding self-service ✅
1. **`IOnboardingService`** (`Application/Onboarding`, impl en `Infrastructure/Services/OnboardingService.cs`): única fuente de verdad; deriva el estado **sin persistir**, aislado por `EmpresaId`. 5 pasos: perfil empresa (NIT/NRC/actividad/dirección) → config DTE (credenciales MH + establecimiento) → certificado cargado → catálogo base (≥1 cliente y ≥1 producto activos) → primer DTE en estado `PROCESADO`.
2. **Checklist de activación** en el dashboard (`Views/Shared/_OnboardingChecklist.cshtml`): barra "X/5 · %", tarjeta por paso con enlace directo a lo pendiente; **se oculta al llegar al 100%**.
3. **Asistente de bienvenida** `/onboarding` (`OnboardingController` + `Views/Onboarding/Index.cshtml`): wizard guiado con "siguiente paso" destacado; acceso manual (no fuerza redirección post-login); entrada en el menú lateral "Asistente".
4. Cubierto por 7 tests unitarios (`OnboardingServiceTests`). Meta: del registro al primer DTE en **< 10 minutos** sin soporte.

### Detalle de NeoConnect API (habilitador NeoBusiness/NeoScan) — Sprint 24

**Hecho (sub-entregas 1-5):**
- ✅ Tablas `Connect_ApiKeys` (hash SHA-256 + prefijo + scopes + estado + expiración), `Connect_Webhooks` (URL + secreto HMAC + eventos), `Connect_WebhookDeliveries` (estado + intentos + backoff); logs de uso reusan `Core_ApiUsageLog` vía `ApiKeyId`. Permisos 351-355.
- ✅ Auth por **API Key** (`X-Api-Key`) → `ApiKeyAuthMiddleware` valida hash, resuelve empresa + scopes en `HttpContext.Items` y engancha `ApiKeyId` al `ApiQuotaMiddleware` (rate limit por key). JWT tiene precedencia si ambos vienen.
- ✅ Gestión: `ConnectController` (`/api/connect/api-keys|webhooks|logs|usage`) con `ConnectApiKeyService` (raw key mostrada una sola vez) y `ConnectWebhookService` (crear/probar/eliminar + usage agregado por key).
- ✅ Webhooks de cambio de estado DTE: `IConnectWebhookDispatcher` disparado (best-effort) desde `DteDocumentosService` al pasar a PROCESADO/RECHAZADO/CONTINGENCIA/INVALIDADO; `ConnectWebhookDeliveryWorker` entrega firmado HMAC-SHA256 (`X-NeoConnect-Signature`) con reintentos y backoff exponencial (2/4/8/16 min, máx. 5).
- ✅ UI Web `/Integraciones` (AppShell + `ns-*`): métricas de consumo, alta/revocación de keys, alta/test/borrado de webhooks, log de entregas.

**Hecho (endpoints de negocio v1):**
- ✅ `ConnectApiV1Controller` (`/api/v1`, `[AllowAnonymous]`, autenticado por API Key vía `ConnectApiControllerBase` que resuelve `ConnectApiKeyContext` de `HttpContext.Items` y exige scope por endpoint → 401 `APIKEY_REQUIRED` / 403 `APIKEY_SCOPE_MISSING`):
  - `GET /ping` (verifica key + scopes), `POST /dte` (emitir extremo-a-extremo vía `IConnectDteService.EmitirAsync` = borrador→generar→validar→firmar→enviar), `GET /dte`, `GET /dte/{id}`, `GET /dte/{id}/pdf`, `GET /dte/{id}/json`, `GET|POST /clientes`, `GET|POST /productos`.
- ✅ Scopes nuevos `Clientes:Write` / `Productos:Write` (la UI `/Integraciones` los expone automáticamente vía `ConnectScopes.All`).
- ✅ Sandbox: el ambiente (PRUEBAS/PRODUCCION) lo determina la **config DTE de la empresa**, sin cambios en el código del cliente.
- ✅ Cuotas: `/api/v1` cuenta contra el módulo `NEOCONNECT` por API Key (`ApiQuotaMiddleware`).
- ✅ OpenAPI público (`MapOpenApi` siempre activo, `GenerateDocumentationFile` enriquece el spec) + guía de desarrollador `docs/NeoConnect-API-v1.md`.
- ✅ Tests `ConnectDteServiceTests` (pipeline completo + cortes en cada fallo).
- ✅ Sub-entrega 6 — tests de gestión: `ConnectApiKeyServiceTests` (hash, validación, revocación, expiración, aislamiento), `ApiKeyAuthMiddlewareTests` (precedencia JWT, sin header, key válida→Items, key inválida→401), `ConnectWebhookDispatcherTests` (entregas solo a suscritos, backoff exponencial, fallido tras máx. intentos).
- **NeoBusiness** consumirá la API para emitir DTE desde sus ventas; **NeoScan** registrará compras/gastos/DTE recibidos.

**NeoConnect: COMPLETO** (gestión + endpoints de negocio + tests).

## Reglas de negocio transversales (NeoProfit) — ✅ implementadas
- Solo contar DTE **PROCESADO**; excluir RECHAZADO e INVALIDADO.
- Nota de Crédito **resta**, Nota de Débito **suma**.
- Sujeto Excluido **no genera IVA**.
- Producto sin costo → marcar **"Costo pendiente"**.

> Implementación: `ProfitCalculator` (puro, en `Application/Profit`) con estas reglas; `ProfitService` proyecta DTE + costos de producto (`CostoUnitario`) + `Profit_Gastos`/`Profit_Compras`. API `/api/profit/*` (gated `RequireModule("NEOPROFIT")` + permisos `Profit.Ver`/`Profit.Gestionar`). Web: dashboard `Profit/Index` (KPIs `ns-kpi` + charts + rankings), grids/CRUD `ProfitGastos`/`ProfitCompras`. Utilidad neta = ganancia bruta − gastos; IVA neto = generado − crédito (gastos deducibles + compras). Tests: `ProfitCalculatorTests` (9) + `ProfitServiceTests` (5).

---

# 11. Plan de mejora UI/UX

**Fuente de verdad:** `/design/` — design system (`design-system/DESIGN.md`) + 7 mockups (Stitch).

## Design system (resumen)
- **Marca:** Trustworthy · Sophisticated · Modular. Estilo Corporate/Modern con tarjetas.
- **Color:** Deep Tech Blue (`#0F172A`) navegación/acciones; **Modern Violet** (`#6b38d4`/`#8B5CF6`)
  acento de IA (NeoScanAI) e interacción.
- **Semántica DTE:** `processed #10B981`, `rejected #EF4444`, `draft #64748B`, `contingency #F59E0B`.
- **Tipografía:** Hanken Grotesk (headlines) · Inter (UI/body) · JetBrains Mono (datos: UUID, montos).
- **Layout:** sidebar 260px + contenido fluido, grid 12 col, baseline 4px, radios 8/12px.

## Estrategia (implementado en Sprint 21)
1. **Secuencia:** primero certificación DTE backend (bloqueante), luego el design system. ✅
2. **CSS:** se implementó como **`wwwroot/css/neostp.css`** — CSS nativo con los tokens del
   design system (sin Node/Tailwind, production-ready), en **coexistencia con Bootstrap**
   (re-tema global + componentes `ns-*`). Fuentes Google + Material Symbols por CDN.
3. **Componentes:** AppShell + `_StepperDte` partial; helpers `ns-card/badge/pill/toolbar/empty/metric`.

## Componentes a construir
`AppShell` · `Sidebar` (oscura, indicador violeta activo) · `Navbar` (con **Environment Indicator**
Mock/Pruebas/Producción) · `MetricCard` · `ModuleCard` · `StatusBadge` · `DataTable` · `FilterPanel` ·
`FormSection` · `StepperDte` (Borrador→Validado→Firmado→Enviado→Procesado) · `CertificationProgressBar` ·
`LicenseUsageCard` · `AlertPanel` · `ConfirmModal` · `AiConfidenceBadge` · `IntegrationStatusCard` ·
`EmptyState` / `ErrorState` / `LoadingState`.

## Mockups disponibles (mapeo a módulos)
| Mockup (`/design/mockups/`) | Módulo |
|---|---|
| `dashboard-dte` | Centro de Control DTE |
| `certificacion-dte` | Certificación DTE (matriz + progreso) ⭐ |
| `nuevo-dte` | Stepper de emisión |
| `plan-licencia` | Core / Billing |
| `neopos` | NeoPOS |
| `neoprofit` | NeoProfit/NeoBI |
| `neoscanai` | NeoScanAI |
| `superadmin` | SuperAdmin |

## Orden de pantallas
AppShell global → Dashboard DTE → Stepper Nuevo DTE → **Certificación DTE** → Config DTE →
Clientes → Productos → Plan/Licencia → NeoProfit → NeoScanAI → NeoConnect → SuperAdmin.

---

# 12. Skills del proyecto

## Skill local del proyecto
- **`/neostp`** (`.claude/skills/neostp/`) — atajos de consola: compilar, levantar Web/Api/Worker,
  migraciones, tests, formatear, limpiar. Invocar como `/neostp <subcomando>`.

## Skills de Claude Code útiles para este proyecto
| Skill | Uso |
|---|---|
| `/run`, `/verify` | Levantar la app y verificar cambios en vivo |
| `/code-review`, `/simplify` | Revisión de bugs y limpieza del diff |
| `/security-review` | Revisión de seguridad de los cambios |
| `/review` | Revisar un PR |
| `/init` | Inicializar/actualizar `CLAUDE.md` del repo |
| `/loop`, `/schedule` | Tareas recurrentes (polling, agentes programados) |
| `claude-api` | Construir/optimizar integraciones con la API de Claude (NeoScanAI) |
| `update-config`, `fewer-permission-prompts` | Configurar el harness y permisos |

## Skills de documentos (anthropic-skills) para entregables
`docx` (Word) · `pdf` (manipular PDF, OCR) · `pptx` (decks) · `xlsx` (Excel) · `skill-creator`
(crear/optimizar skills) · `consolidate-memory`.

> Recomendación: crear skills propias del dominio — ej. `/dte` (generar/firmar/enviar un DTE de
> prueba end-to-end), `/cert-matrix` (estado de la matriz), `/seed-catalogo` (sembrar/actualizar un
> catálogo MH).

---

# 13. Análisis y mejora de código

## 13.1 Calidad de código y comentarios
- **Mantener** el estilo de comentarios actual: docstrings `<summary>` en servicios/métodos clave
  y comentarios que explican el **porqué** (ej. en `HaciendaCertMhDteSignerService` se documenta que
  RS512 es obligatorio). Evitar comentarios obvios.
- **Documentar las particularidades de Hacienda** en el código donde se aplican (numeroControl,
  división territorial por versión, esquema asimétrico de eventos) — ya iniciado.
- **Eliminar hardcodeos** y reemplazarlos por catálogos/configuración:
  - municipio `23` / distrito `03` hardcodeados en builders de Donación/Exportación/eventos.
  - mapeos de catálogo dispersos → centralizar en un `CatalogoMapper`.
- **Centralizar** el mapeo interno→MH (tipoDocumento, unidad, forma de pago, actividad, tipo
  establecimiento) hoy parcial e inline.

## 13.2 Refactors sugeridos
- **`DteGeneratorService`** crece por tipo: considerar un patrón por tipo (estrategia/builder por
  `TipoDteCodigo`) para reducir el switch y los métodos `BuildX` paralelos.
- **Clientes Hacienda**: hay 3 (auth, reception, contingencia) + 1 genérico (evento). Unificar bajo
  una abstracción común con endpoint + body parametrizables reduciría duplicación.
- **Eventos**: hoy viven como métodos en `DteDocumentosService`. Extraer a un
  `DteEventoService` dedicado + persistirlos en tablas `Dte_Eventos*`.
- **Versiones por tipo**: el `switch` de `VersionDte` y los `BuildIdentificacion(d, N)` deberían
  leer de una tabla/config de versiones por tipo (preparar el corte a v2/v4 cuando apitest lo adopte).

## 13.3 Base de datos
- **Índices**: revisar índices en `Dte_Documentos` (EmpresaId+EstadoCodigo, EmpresaId+TipoDteCodigo,
  CodigoGeneracion, NumeroControl) para los queries de dashboard/listado/consulta.
- **`Core_Auditoria`**: campo `Detalle` se usa para guardar respuestas crudas (incluye `RAW` de
  eventos). Considerar una tabla específica de respuestas Hacienda para no inflar auditoría.
- **Soft-delete** consistente (`Activo`/`EstadoCodigo`) en catálogos y datos maestros.
- **Versionado de catálogos** (`Version`) para trazar cambios normativos MH.
- **Migraciones**: mantener el patrón `SprintN_Tema`; aplicar siempre con build fresco (el
  `--no-build` causó un `PendingModelChangesWarning` falso).

## 13.4 Seguridad y operación
- Guardrail anti-mock ✅ implementado. Extender la idea: validar **ambiente** (no enviar pruebas a
  producción) y **vigencia del certificado** antes de firmar.
- MFA para SuperAdmin · IP allowlist para panel admin · quotas API por plan (Hardening).
- Cifrado de secretos ✅ (DataProtection). Documentar rotación de llaves (cambiarlas invalida los
  secretos cifrados → reingresar).

## 13.5 Testing
- 259 tests unit + 2 integración verde (106 base + 31 catálogos Sprint 13 + 24 certificación Sprint 14 + 18 eventos Sprint 15 + 43 hardening Sprint 20 + billing/pagos/lookups/carga masiva + smoke). **NeoConnect (Sprint 24) aún sin tests** — sub-entrega 6 pendiente: `ConnectApiKeyService` (hash/validación/revocación/expiración), `ApiKeyAuthMiddleware` (precedencia JWT, key inválida → 401) y dispatcher (creación de deliveries + backoff). Faltan además tests del **generador v1/v3 por tipo** (snapshot del JSON esperado) y de
  los **eventos** (estructura). Agregar tests de regresión que validen el JSON contra los esquemas
  `svfe-json-schemas` y contra lo que apitest realmente exige (v1/v3).

---

# 14. Objetivo final del producto

NeoSTP Cloud debe convertirse en el **centro operativo de la empresa**: cumplimiento fiscal,
emisión DTE, certificación Hacienda, ventas, POS, clientes, productos, rentabilidad, IA documental,
integraciones, portal de clientes, app móvil, billing SaaS y operación multiempresa — todo desde
**una sola plataforma**.

**Promesa comercial:**
> NeoSTP Cloud no solo factura electrónicamente. Ayuda a cumplir con Hacienda, vender, analizar
> ganancias, automatizar documentos y conectar sistemas externos desde una sola plataforma.

**Capacidades objetivo:**
1. Emitir DTE · 2. Certificarse con Hacienda · 3. Administrar clientes y productos ·
4. Analizar ventas y ganancias · 5. Escanear documentos con IA · 6. Integrarse con sistemas externos ·
7. Operar POS · 8. Dar acceso móvil · 9. Ofrecer portal a clientes · 10. Administrarse como SaaS con
billing, planes y licencias.

---

> **Documento mantenido junto al código.** Actualizar cuando cambie el estado de módulos,
> la matriz de certificación o el plan de trabajo. Fuentes: `README.md`, `/design/README.md`,
> runbooks en `docs/`, y los esquemas oficiales `svfe-json-schemas`.
