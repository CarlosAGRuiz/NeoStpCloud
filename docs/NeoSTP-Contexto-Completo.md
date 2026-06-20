# NeoSTP Cloud — Contexto completo del proyecto y plan de trabajo

> Documento maestro de traspaso. Consolida **qué es el sistema, cómo está construido y
> configurado, cómo funciona el DTE de punta a punta, cómo se maneja la UI, las pruebas,
> las migraciones, el estado de cada módulo y el plan detallado de lo que falta**.
>
> **Estado histórico:** rama `main` · Build ✅ 0 errores · **259 tests unit + 2 integración** verde.
> Documento conservado como referencia antigua; la fuente actual es `CONTEXTO-PROYECTO.md`.
> Estado actualizado 2026-06-15: V2/V2.5 cerradas, API mobile AM-0..AM-6, HB-1, HB-3/HB-4, HB-5
> HB-0..HB-8 y V3-S1/V3-S2 cerrados, 725 unit + 9 integracion, build 0 warnings; siguiente foco
> V3-S3 vacaciones y aguinaldo NeoRRHH segun `docs/Plan-V3.md`.
> **Repositorio:** `github.com/CarlosAGRuiz/NeoStpCloud`
> **Último avance:** Sprints 13–21 + Pagos LATAM + Lookups/hardcodeos + Carga masiva.

---

## Índice

1. [Visión general](#1-visión-general)
2. [Stack y arquitectura](#2-stack-y-arquitectura)
3. [Configuración del sistema](#3-configuración-del-sistema)
4. [Funcionamiento DTE / Hacienda (completo)](#4-funcionamiento-dte--hacienda-completo)
5. [Catálogos MH](#5-catálogos-mh)
6. [Cómo se maneja la UI](#6-cómo-se-maneja-la-ui)
7. [Billing y Pagos (multi-proveedor LATAM)](#7-billing-y-pagos-multi-proveedor-latam)
8. [Lookups y datos maestros](#8-lookups-y-datos-maestros)
9. [Carga masiva (Excel/CSV)](#9-carga-masiva-excelcsv)
10. [Hardening / operación](#10-hardening--operación)
11. [Pruebas](#11-pruebas)
12. [Migraciones](#12-migraciones)
13. [Estado de los módulos](#13-estado-de-los-módulos)
14. [Lo que falta trabajar (detallado)](#14-lo-que-falta-trabajar-detallado)
15. [Plan inmediato — Onboarding self-service](#15-plan-inmediato--onboarding-self-service)
16. [Plan — NeoConnect API (NeoBusiness / NeoScan)](#16-plan--neoconnect-api-neobusiness--neoscan)
17. [Convenciones para nuevo código](#17-convenciones-para-nuevo-código)

---

# 1. Visión general

**NeoSTP Cloud** es el sistema web central de **NeoSTP Business Suite**: una plataforma
**SaaS modular multiempresa** para El Salvador, no solo un facturador electrónico. Cada
cliente entra a **una sola web** y, según su plan/licencia, usa distintos módulos.

| Módulo | Qué hace | Estado |
|---|---|---|
| Core / Administración | Empresas, usuarios, roles, permisos, planes, módulos, licencias, sucursales, PV, auditoría | ✅ |
| NeoDTE | Emisión de Documentos Tributarios Electrónicos El Salvador | ✅ |
| Certificación DTE | Control de la matriz de pruebas de Hacienda | ✅ |
| Eventos DTE | Invalidación, Contingencia, Retorno, Operaciones Especiales | ✅ |
| Billing SaaS + Pagos | Venta self-service: Stripe/MercadoPago/**Wompi/PayPal/Transferencia** | ✅ |
| Legal / Compliance | Términos, privacidad, consentimiento | ✅ |
| Hardening | Rate limiting, MFA, IP allowlist, backups | ✅ |
| UI/UX | AppShell + design system | ✅ |
| NeoProfit / NeoBI | Análisis financiero y rentabilidad | ❌ pendiente |
| NeoScanAI | OCR/IA de documentos, compras, gastos | ❌ pendiente |
| NeoConnect API | API comercial para integradores/ERPs | ❌ pendiente |
| NeoPOS | Punto de venta web integrado con DTE | ❌ pendiente |
| NeoPortal Clientes | Portal para receptores | ❌ pendiente |
| NeoSTP Mobile | App móvil + gestión de dispositivos | ❌ pendiente |

**Principio de diseño:** los módulos se venden por separado pero el cliente no debe sentir
que usa sistemas distintos. Todo respeta: empresa actual, plan activo, módulos contratados,
permisos del usuario, auditoría, multiempresa y **aislamiento por `EmpresaId`**.

**SuperAdmin inicial:** `superadmin` / `ChangeMe!2026` (cambiar al primer login). No pertenece
a ninguna empresa; opera multi-tenant en **modo soporte** (selecciona empresa → cookie →
`IEmpresaContext` hace scope de los queries).

---

# 2. Stack y arquitectura

## Stack
- **.NET 10** (LTS hasta nov-2028) · ASP.NET Core MVC/Razor (Web) + Web API/OpenAPI (Api)
- **SQL Server 2022** + **EF Core 10** · **.NET Worker Service** (jobs)
- **Serilog** · **QuestPDF 2025.1** (PDF) · **MailKit 4.17** (correo)
- **Polly v8 / Microsoft.Extensions.Http.Resilience** (resiliencia HTTP)
- **JWT** (Api) + **Cookies** (Web) · **DataProtection** (cifrado de secretos) · **BCrypt** (passwords)
- **ClosedXML 0.104** (Excel import/export) · **Stripe.net 47** + **mercadopago-sdk 3.1**
- **xUnit + FluentAssertions + NSubstitute + EF InMemory** (pruebas)

## Capas (modular monolith)
```
NeoSTP.slnx
├── src/
│   ├── NeoSTP.Web              # MVC/Razor (UI)  — AppShell + neostp.css
│   ├── NeoSTP.Api              # Web API (REST + OpenAPI)
│   ├── NeoSTP.Application      # Casos de uso, servicios (interfaces), DTOs, opciones
│   ├── NeoSTP.Domain           # Entidades, reglas, enums, constantes
│   ├── NeoSTP.Infrastructure   # EF Core, SQL Server, clientes Hacienda, firma, PDF, correo, pagos
│   ├── NeoSTP.Worker           # Background jobs
│   └── NeoSTP.Shared           # ApiResponse, utilidades, constantes
├── tests/{NeoSTP.Tests.Unit, NeoSTP.Tests.Integration}
├── design/                     # Design system + mockups (fuente de verdad UI)
├── ops/k6/                     # Pruebas de carga k6
└── docs/                       # Runbooks (mocks→real, matriz, Disaster Recovery, este doc)
```

**Referencias:** Web/Api/Worker → Application + Infrastructure + Shared; Application → Domain +
Shared; Infrastructure → Application + Domain + Shared. **Las interfaces (abstracciones) viven en
Application; las implementaciones en Infrastructure** → inversión de dependencias limpia.

**Patrones clave:**
- `Result` / `Result<T>` para retornos de servicio (éxito/error + código + validaciones).
- `ApiResponse` / `ApiResponse<T>` como envoltura de respuestas Api; `ApiControllerBase.MapError`
  mapea códigos de error a HTTP (404/409/422/502…).
- `[RequirePermiso("...")]` (Api) + claims; en Web se chequea `User.HasClaim("permiso", ...)` o
  `_currentUser.HasPermiso(...)` con bypass de SuperAdmin.
- Seed EF vía `HasData` en `SeedData.cs` (parciales por área); permisos con rangos de IDs.

---

# 3. Configuración del sistema

## Connection string
`appsettings.Local.json` (gitignored) en `Api`, `Web` y `Worker`:
```json
{
  "ConnectionStrings": { "NeoStpDb": "Server=.;Database=NeoSTP_Cloud;User Id=sa;Password=...;TrustServerCertificate=True;MultipleActiveResultSets=True" },
  "Jwt": { "Key": "dev-only-replace-me-32+-chars", "Issuer": "...", "Audience": "..." }
}
```

## Toggles Mock/Real (desarrollar sin credenciales)
| Toggle | Valores | Real |
|---|---|---|
| `Hacienda:Client` | `Mock` \| `Http` | clientes HTTP a apitest con Polly |
| `Dte:Signer` | `Mock` \| `Pkcs12` \| `HaciendaCert` | firma RS512 con certificado MH |
| `Email:Provider` | `Mock` \| `Smtp` | MailKit (mock deja `.eml` en `logs/email-outbox/`) |
| `Billing:Provider` | `Mock` \| `Stripe` \| `MercadoPago` \| `Wompi` \| `PayPal` \| `Transferencia` | proveedor **por defecto** (el cliente elige método en checkout) |
| `Hardening:Backup:StorageProvider` | `LOCAL` \| `AZURE_BLOB` \| `S3` | almacenamiento de backups |

## Secciones de opciones (Options)
- `Billing` → `BillingOptions` (Provider, TrialDays, Stripe, MercadoPago, **Wompi, PayPal, Transferencia**)
- `Hardening:RateLimit` → `RateLimitOptions` (Enabled)
- `Hardening:Backup` → `BackupOptions` (StorageProvider, LocalPath, WorkerEnabled, IntervaloHoras)
- `Dte:Territorial` → `TerritorialOptions` (MunicipioDivision2024Default, DistritoDefault)
- `Legal` → `LegalOptions` (placeholders de documentos legales)
- `Worker` → `WorkerOptions` (RetransmisionContingencia, LimpiezaTokens, ContingenciaLote)
- `EmpresaPrueba` → provisioning idempotente de una empresa de pruebas al arrancar la Api

## Cifrado de secretos
`ISecretProtector` (DataProtection, purpose `NeoSTP.DteSecrets.v1`) cifra: password MH, password
del certificado, token MH cacheado y el **secreto TOTP de MFA**. Llaves en
`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` (Win) o `/var/aspnet/DataProtection-Keys` (Linux).
**Cambiar/borrar la llave invalida los secretos cifrados** → hay que reingresarlos / re-enrolar MFA.

## Cómo correr
```powershell
dotnet build NeoSTP.slnx
dotnet ef database update --project src/NeoSTP.Infrastructure --startup-project src/NeoSTP.Api
dotnet run --project src/NeoSTP.Web    # Web (cookies)
dotnet run --project src/NeoSTP.Api    # Api (JWT, OpenAPI en /openapi/v1.json en Dev)
dotnet run --project src/NeoSTP.Worker # jobs
```
Skill local `/neostp` con atajos. Skill `/neostp-sprints` con el backlog 13–30.

---

# 4. Funcionamiento DTE / Hacienda (completo)

## 4.1 Qué es un DTE
Archivo **JSON** firmado electrónicamente (JWS) que reemplaza al papel fiscal en El Salvador.
Tipos (CAT-002): 01 Factura · 03 CCF · 04 Nota de Remisión · 05 Nota de Crédito · 06 Nota de
Débito · 07 Retención · 08 Liquidación · 09 DCL · 11 Factura de Exportación · 14 Sujeto Excluido ·
15 Donación.

## 4.2 Ciclo de vida (máquina de estados)
```
BORRADOR → GENERADO → VALIDADO → FIRMADO → ENVIADO → PROCESADO
                                                   ↘ RECHAZADO    (re-emisión)
                                                   ↘ CONTINGENCIA (reintento Worker)
                                                   ↘ ERROR
PROCESADO → INVALIDADO  (vía evento de invalidación)
```
`PROCESADO` = aceptado con **selloRecibido** (es DTE legal).

## 4.3 Construcción del JSON — `DteGeneratorService`
`Generar(DteDocumento, DteConfiguracion?)` arma el JSON por tipo. Bloques:
- **identificacion**: `version` (entero: 1 factura/FSE, 3 CCF/NC/ND/NR, 2 Donación), `ambiente`
  (`00` pruebas / `01` producción), `tipoDte`, **numeroControl**, `codigoGeneracion` (UUID v4
  mayúsculas), `tipoModelo`, `tipoOperacion`, `tipoContingencia`, `motivoContin`, `fecEmi`, `horEmi`,
  `tipoMoneda` (USD).
- **emisor**: difiere por tipo (Factura v1 con `extension`/`codEstableMH`/`tipoEstablecimiento`;
  FSE sin `tipoEstablecimiento`; Exportación/Donación v2/v3 con `distrito` de división 2024).
- **receptor**: Factura puede ser null (consumidor final); CCF/NC/ND requieren NIT + **NRC** + actividad.
- **cuerpoDocumento**: Factura lleva `ivaItem`; CCF/NC/ND no (IVA en resumen).
- **resumen**: totales (Factura `ivaRete1`/`reteRenta`; CCF `ivaPerci1`; Exportación tributo `C3`).

### numeroControl (formato crítico)
```
DTE-{tipoDte}-{bloqueEstablecimiento}-{correlativo 15 dígitos}
bloqueEstablecimiento = (M|B|S|P)([0-9]{3})(P)([0-9]{3})   →   M001P001
```
Letra = tipo de establecimiento (CAT-009): M=Casa Matriz, S=Sucursal, B=Bodega, P=Predio.
Correlativo desde `Dte_Correlativos` (UPSERT atómico). `codEstableMH`/`codPuntoVentaMH` = **4 caracteres** (`0001`).

### División territorial por versión
- **DTE v1** (Factura, FSE, NR): división **vieja** (Ayutuxtepeque = municipio `03`, sin distrito).
- **DTE v2/v3** (Donación, Exportación): división **2024** (San Salvador Centro `23` + distrito `03`).
- El valor de municipio/distrito v2/v3 **ya no está hardcodeado**: sale de `TerritorialOptions`
  (`Dte:Territorial`) + distrito del emisor/documento; `LookupService.ResolverMunicipio2024Async`
  queda listo para derivarlo 100% del catálogo DISTRITO_ES (CAT-008) cuando se siembre.

## 4.4 Firma — JWS RS512
`HaciendaCertMhDteSignerService` firma con el **CertificadoMH XML** (`.crt`) de Hacienda (PKCS#8 +
SubjectPublicKeyInfo, sin password). Estándar **RS512** (RSA+SHA-512), header mínimo `{"alg":"RS512"}`.
```
JWS = base64url(header).base64url(payload).base64url(firma)
firma = RSA-SHA512(header_b64 + "." + payload_b64)
```
Auto-verificación local con la clave pública antes de devolver. **Guardrail anti-mock:** si el cliente
de recepción es real y el JWS es `alg:none-mock`, se bloquea (`FIRMA_MOCK_NO_ENVIABLE`).

## 4.5 Autenticación con Hacienda
```
POST {base}/seguridad/auth (form: user + pwd) → body.token (ya incluye prefijo "Bearer ")
```
`HttpHaciendaAuthClient` recorta el prefijo antes de cachear (cifrado). Token refresca con 5 min de
margen. **Dos passwords distintos:** portal web ≠ API de recepción (`passwordMh` es el de la API).

## 4.6 Transmisión
```
POST {base}/fesv/recepciondte  (Bearer token)
Body: { ambiente, idEnvio, version, tipoDte, documento(JWS), codigoGeneracion }
→ { estado: "PROCESADO", selloRecibido, codigoMsg, descripcionMsg, observaciones }
```
Mapeo: PROCESADO→guarda sello + ProcesadoAt; RECHAZADO→re-emisión; CONTINGENCIA→Worker reintenta.
Códigos: 001 RECIBIDO · 002 con observaciones · 006 formato · 095 no autorizado · 096 esquema · 802 firma.

## 4.7 Contingencia (3 momentos)
1. **Emitir** en contingencia (`tipoTransmision=2`, `tipoModelo=2`, `tipoContingencia` CAT-005).
2. **Evento de Contingencia** (≤24h, `/fesv/contingencia`) → sello.
3. **Lote** (≤72h, `/fesv/recepcionlote`) → sello individual por DTE; consulta `/fesv/recepcion/consultadtelote/{codigoLote}`.
`RetransmisionContingenciaWorker` + `ContingenciaLoteWorker` automatizan reintento y lotes.
Tablas `Dte_ContingenciaLotes`/`Dte_ContingenciaLoteDetalles` + `ContingenciaLoteService` + UI `/DteContingencia`.

## 4.8 Eventos DTE
Cuatro eventos; **solo Contingencia e Invalidación tienen endpoint propio**:
| Evento | Endpoint | Estado apitest |
|---|---|---|
| Invalidación | `/fesv/anulardte` | ✅ PROCESADO |
| Contingencia | `/fesv/contingencia` | ✅ PROCESADO |
| Operaciones Especiales (`fe-eop`, tipoEvento 17) | `/fesv/recepciondte` | 🟡 estructura OK — bloqueo `095` (autorización de cuenta) |
| Retorno (`fe-eret`, tipoEvento 18) | `/fesv/recepciondte` | 🟡 estructura OK — bloqueo `codEstableMH` real |
Persistencia en `Dte_Eventos*` (best-effort, no rompe el flujo certificado); UI `/DteEventos` + PDF;
integración con certificación (`POST /api/certificacion/eventos/{id}/marcar-completado`).

## 4.9 PDF + QR · Diagnóstico
`DtePdfService` (QuestPDF) genera el PDF con **QR** a la consulta pública MH. Módulo de **Diagnóstico**
(`/DiagnosticoHacienda`) mapea códigos de error MH a explicaciones y acciones (`Dte_ErrorCatalogo`/
`Dte_ErrorOcurrencias`).

## 4.10 Certificación lograda (apitest real)
**El ambiente apitest valida v1/v3 (NO v2/v4).** PROCESADOS: 01 Factura (v1), 11 Exportación (v3),
04 Nota de Remisión (v3), 14 Sujeto Excluido (v1), 15 Donación (v2) + eventos Contingencia e
Invalidación. Pendiente de **datos de cuenta** (no de código): 03/05/06 y 07/08/09 requieren receptor
inscrito en IVA (NIT+NRC reales); Op-Especiales/Retorno requieren autorizaciones.

## 4.11 Módulo de Certificación DTE
Matriz oficial **15 tipos × 625 escenarios**; `CertificacionDteService` (resumen, matriz, escenarios,
generar-prueba, marcar-completado, reintentar, errores). UI `/Certificacion`. Promueve a COMPLETADO
solo con sello + PROCESADO; valida cruzado tipo↔matriz.

---

# 5. Catálogos MH

`Core_Catalogos` / `Core_CatalogoItems` (con `Version`, `MetadataJson`, `ParentCodigo` para cascadas).
Módulo de mantenimiento completo (Sprint 13): CRUD + import CSV/JSON/XLSX + export + versionado +
cascadas padre/hijo, vía API (`/api/catalogos`) y UI (`/Catalogos`). 36 catálogos oficiales (Manual de
Estructuras v1.4) con `Codigo = codigoMH`. **Pendiente:** CAT-008 Distrito (placeholder vacío — cargar
vía `/Catalogos/Import/DISTRITO_ES` cuando MH publique la lista; habilita la derivación territorial 100%).

Permisos: `Core.Catalogos.Ver/.Administrar/.Importar`.

---

# 6. Cómo se maneja la UI

## Fuente de verdad
`/design/` — `design-system/DESIGN.md` (tokens) + mockups Stitch (dashboard-dte, certificacion-dte,
nuevo-dte, plan-licencia, neopos, neoprofit, neoscanai, superadmin).

## Implementación (Sprint 21)
- **`wwwroot/css/neostp.css`** — CSS **nativo** con los tokens del design system (NO Tailwind/Node;
  production-ready). Variables: Deep Tech Blue `#131b2e` (navegación/acción), Modern Violet `#6b38d4`
  (acento/IA), semántica DTE (processed `#10B981`, rejected `#EF4444`, draft `#64748B`, contingency
  `#F59E0B`), border-subtle `#E2E8F0`, radios 8/12px, sombras suaves.
- Fuentes por CDN: **Hanken Grotesk** (headlines), **Inter** (UI/body), **JetBrains Mono** (datos:
  UUID, montos), **Material Symbols** (íconos), **Bootstrap Icons** (Billing).
- **AppShell** en `Views/Shared/_Layout.cshtml`: sidebar oscura fija (260px) agrupada por permisos,
  navbar sticky con **indicador de ambiente** (Mock/Pruebas según `Hacienda:Client`) + chip de modo
  soporte + menú de usuario; **responsive** con drawer off-canvas en móvil. Login con su propio layout.
- **Re-tematización global de Bootstrap** dentro de `.ns-content` (cards, botones, tablas, badges,
  forms, paginación) → toda pantalla existente adopta el look sin reescribirse.
- Helpers reutilizables: `ns-card`, `ns-badge--{processed|rejected|draft|contingency}`, `ns-pill--{on|off}`,
  `ns-toolbar`, `ns-empty`, `ns-metric`, `ns-kv`, `ns-stepper`, `ns-totals`, `ns-env`.
- **`_StepperDte`** partial: Borrador→Validado→Firmado→Enviado→Procesado (en el detalle del DTE).
- Listados estandarizados: toolbar (búsqueda/filtros + acción) + tabla-en-card + estado vacío con ícono.

## Convención para vistas nuevas
- Usar el AppShell (layout por defecto) y las clases `ns-*` + tokens CSS (`var(--ns-*)`).
- Datos para selects/cascadas/autocompletes: pedir a `/api/lookups/*` o `ILookupService` (ver §8).
- Estados DTE/eventos: usar los badges semánticos. Tablas con datos numéricos/códigos en `ns-mono`.

---

# 7. Billing y Pagos (multi-proveedor LATAM)

## Arquitectura
- `IBillingService`: trial, checkout, portal, cambio de plan, cancelación, consultas, **transferencia**.
- `IPaymentProvider` (abstracción por proveedor) + **`IPaymentProviderResolver`** (resuelve por nombre
  de método; el cliente elige en el checkout; fallback al default `Billing:Provider`).
- Proveedores: `MockPaymentProvider`, `StripeBillingProvider`, `MercadoPagoBillingProvider`,
  **`WompiBillingProvider`** (wompi.sv: OAuth2 + EnlacePago hospedado vía REST/Polly),
  **`PayPalBillingProvider`** (Orders v2: OAuth2 Basic + link de aprobación),
  **`TransferenciaPaymentProvider`** (offline).
- `IBillingWebhookHandler` (idempotente por `Billing_WebhookEvents`): ramas Stripe, MercadoPago,
  **Wompi, PayPal** → registran pago y activan suscripción.
- Tablas `Billing_Customers/Subscriptions/Payments/Invoices/WebhookEvents/PlanProviderMappings`.
  `BillingPayment` con `Metodo`, `ComprobanteUrl`, `VerificadoPor`, `VerificadoAt`.

## Transferencia bancaria (offline, verificación manual)
`IniciarTransferenciaAsync` (pago PENDIENTE_VERIFICACION + instrucciones bancarias de
`TransferenciaOptions`) → cliente sube comprobante (`RegistrarComprobanteAsync`) → admin
**confirma** (`ConfirmarTransferenciaAsync` → activa licencia + email) o **rechaza**. Bandeja
SuperAdmin `/billing/transferencias`.

## UI
`/billing/checkout` con selector de método; `/billing/transferencia` (instrucciones + comprobante);
`/billing` portal. Activación automática de licencias (`Core_EmpresaPlan`) y emails transaccionales.

## PCI / cumplimiento
Todos usan **checkout hospedado**: los datos de tarjeta los procesa la pasarela, **nunca** nuestros
servidores.

## Pendiente operativo (no de código)
Cargar credenciales reales (Wompi AppId/Secret, PayPal ClientId/Secret, datos bancarios) en
`appsettings.Local.json` y **enlazar el monto por plan** en los providers (hoy va 0 hasta conectar el
mapping de precios real / `BillingPlanProviderMappings`).

---

# 8. Lookups y datos maestros

- **`ILookupService` / `LookupService`** (caché por instancia, scoped): catálogos por código/padre,
  cascada territorial (`GetDepartamentos`→`GetMunicipios`→`GetDistritos`),
  **`ResolverMunicipio2024Async`** (distrito → municipio división 2024 vía `ParentCodigo`),
  y búsqueda de clientes/productos/sucursales.
- **API** `/api/lookups/{catalogo|departamentos|municipios|distritos|clientes|productos|sucursales}`
  devuelve `LookupItem{Value, Label, Parent?, Meta?}`.
- Base reutilizable para vistas nuevas y mobile. Elimina consultas dispersas y centraliza el acceso.

---

# 9. Carga masiva (Excel/CSV)

- **`TabularParser`** genérico (CSV/XLSX) → filas indexadas por encabezado (minúsculas).
- `BulkImportRequest` (Format, Content, **DryRun**) / `BulkImportResult` (Total/Inserted/Updated/
  Skipped/Errors con número de fila).
- **`IClientesService.ImportAsync`** (upsert por tipo+número de documento) e
  **`IProductosService.ImportAsync`** (upsert por código interno): validación por fila (reusa los
  validadores), dedup intra-archivo, **dry-run** (previsualiza sin guardar), auditoría.
- UI `/Clientes/Importar` y `/Productos/Importar`: subida + casilla "Simular" + reporte
  (métricas + tabla de errores); botón "Carga masiva" en los listados.

---

# 10. Hardening / operación

- **Rate limiting**: `ApiQuotaService` (ventana deslizante por empresa/usuario/plan/módulo/apikey/
  global) + `ApiQuotaMiddleware` → **429** con `Retry-After`/`X-RateLimit-*`. SuperAdmin exento.
  Reglas data-driven en `Core_ApiQuotas`; uso en `Core_ApiUsageLog`.
- **MFA SuperAdmin (TOTP RFC 6238)**: `TotpService` (sin dependencia externa) + `MfaService`
  (enrolar/confirmar/verificar, 10 códigos de recuperación hash SHA-256, secreto cifrado).
  Endpoints `/api/auth/mfa/{enroll|confirm|disable}`; el login exige código si MFA está activo.
- **IP allowlist**: `AdminIpAllowlistService` (exacta + CIDR, **fail-open** si vacía) +
  `AdminIpAllowlistMiddleware` (restringe SuperAdmin por IP). `Core_AdminIpAllowlist`.
- **Backups**: `IStorageService` (LOCAL/AZURE_BLOB/S3) + `BackupService` (manifiesto + checksum
  SHA-256) + `BackupWorker`. `Ops_BackupJobs`. UI/API `/Hardening` + `/api/hardening`.
- Ops/CI: `ops/k6/baseline.js`, `.github/workflows/zap-baseline.yml` (OWASP ZAP), runbook
  `docs/Sprint20-Disaster-Recovery.md`.
- Permisos `Ops.Hardening.Ver/.Administrar`.

---

# 11. Pruebas

```powershell
dotnet test NeoSTP.slnx                 # 259 unit + 2 integración
dotnet test tests/NeoSTP.Tests.Unit     # solo unit (~10s)
```
Cobertura por área (resumen): Auth/BCrypt, Empresas (límites), Clientes (validadores), DTE
(DataProtection, cálculo, generación JSON v1/v3, firma JWS, recepción mock, PDF, correo, **territorial
configurable**), Dashboard, Workers (retransmisión, limpieza), Provisioning, Catálogos
(esquema/CRUD/import), Certificación, Eventos, **Hardening** (schema/quotas/TOTP/MFA/IP/backups),
**Billing/Pagos** (resolver, Wompi, PayPal, transferencia), **Lookups**, **Carga masiva**
(clientes/productos). Integración: smoke cross-service de hardening.

**Definición de completado (cada incremento):** build verde · tests verde sin regresiones · migración
aplicada si aplica · sin secretos en repo · multiempresa respetado · acciones críticas auditadas ·
README/CONTEXTO actualizados.

---

# 12. Migraciones (orden)

`InitialCreate` · `Sprint1_CoreCatalogosYSeguridad` · `Sprint3_ClientesYProductos` ·
`Sprint35_MunicipiosES` · `Sprint4_DteConfiguracion` · `Sprint5_DteDocumentos` ·
`Sprint9_RetransmisionTracking` · `Sprint10_DteCorrelativos` · `Sprint12_DistritoCAT008` ·
`Sprint13_CatalogosExtendido/PermisosCatalogos/SeedCatalogosMH/CatalogosMhOficial` ·
`Sprint14_CertificacionDte/PermisosCertificacion` ·
`Sprint15_DteEventos/PermisoEventos/CertificacionPruebaEvento` · `Sprint16_ContingenciaLotes` ·
`Sprint17_DiagnosticoErrores/SeedErrorCatalogo` · `Sprint18_LegalConsentimiento` ·
`Sprint19_BillingSelfService` · `Sprint20_HardeningSchema` · **`PagosLatam_MetodosPago`**.

> UI/UX, Lookups y Carga masiva **no requirieron migración**.

---

# 13. Estado de los módulos

| Módulo | Estado | Pendientes clave |
|---|---|---|
| Core / Administración | ✅ avanzado | Onboarding self-service, consumo por plan/upselling |
| NeoDTE | ✅ avanzado | Sembrar CAT-008 Distrito (derivación territorial 100% por catálogo) |
| Certificación DTE | ✅ | Completar matriz con datos de cuenta (NRC, codEstableMH real) |
| Eventos DTE | ✅ | Op-Especiales/Retorno bloqueados por autorización de cuenta MH |
| Contingencia/Worker | ✅ | — |
| Clientes / Productos | ✅ | Carga masiva ✅; mapear unidad→CAT-014, tributos por tipo |
| Catálogos MH | ✅ | CAT-008 Distrito |
| Dashboard | ✅ base | Integrar NeoProfit, alertas Hacienda |
| Billing SaaS + Pagos | ✅ | Credenciales reales + monto por plan en providers |
| Legal / Hardening / UI-UX | ✅ | — |
| Lookups / Carga masiva | ✅ | Adopción gradual de `/api/lookups` en más formularios |
| **NeoProfit / NeoBI** | ❌ | Análisis financiero, gastos/compras, márgenes |
| **NeoScanAI** | ❌ | Bandeja, OCR/IA, registro compra/gasto/DTE recibido |
| **NeoConnect API** | ❌ | API keys, webhooks, sandbox, docs (infra de cuotas lista) |
| **NeoPOS / NeoPortal / Mobile** | ❌ | Operación avanzada |

---

# 14. Lo que falta trabajar (detallado)

Plan comercial re-secuenciado (priorizar venta en El Salvador):

| # | Iniciativa | Estado | Valor |
|---|---|---|---|
| 1 | Pagos LATAM | ✅ | Cobro local |
| 2 | Lookups + hardcodeos | ✅ | Robustez con clientes reales |
| 3 | Carga masiva | ✅ | Onboarding de datos |
| **4** | **Onboarding self-service** | 🔜 siguiente | Conversión de prospectos (ver §15) |
| 5 | **NeoConnect API** | 🔜 | Producto vendible + base NeoBusiness/NeoScan (ver §16) |
| 6 | NeoProfit / NeoScanAI | 🔜 | Diferenciadores |
| 7 | NeoPOS / NeoPortal / Mobile | 🔜 | Operación avanzada |
| 8 | Enterprise (SSO/SAML, SOC2/ISO, marca blanca) | 🔜 | Cuentas grandes |

**Tareas técnicas transversales pendientes:** sembrar CAT-008 (territorial), enlazar monto por plan en
pagos, cargar credenciales reales de pasarelas, completar matriz de certificación con cuenta real,
tests de snapshot del JSON v1/v3 por tipo.

**Reglas de negocio (NeoProfit):** solo contar DTE PROCESADO (excluir RECHAZADO/INVALIDADO); NC resta,
ND suma; Sujeto Excluido no genera IVA; producto sin costo → "Costo pendiente".

---

# 15. Plan inmediato — Onboarding self-service

**Objetivo:** que un prospecto pase del registro al **primer DTE en < 10 minutos**, sin soporte.

**Alcance (sub-entregas sugeridas):**
1. **Servicio de estado de onboarding** — `IOnboardingService.GetEstadoAsync(empresaId)` que evalúa
   pasos: (a) datos de empresa completos, (b) configuración DTE (ambiente + credenciales MH +
   certificado), (c) ≥1 cliente, (d) ≥1 producto, (e) primer DTE emitido. Devuelve `% completado` +
   lista de pasos con `hecho/pendiente` + enlace a la acción. + tests.
2. **Wizard de bienvenida** post-registro (`/Onboarding`) con barra "X de N": navega los pasos
   reutilizando los formularios existentes (config DTE, clientes/productos o carga masiva, emitir DTE
   de prueba). Diseño con el AppShell + `ns-stepper`.
3. **Checklist de activación** en el dashboard ("Completa tu cuenta: 3/5") con enlaces directos a lo
   que falta; se oculta al 100%.
4. **Estados de preparación** visibles para el cliente y para SuperAdmin (panel de cuentas en
   activación → soporte proactivo).

**Criterios:** sin migración nueva si se derivan los estados de datos existentes; respeta permisos y
multiempresa; UI moderna; no rompe los flujos actuales.

---

# 16. Plan — NeoConnect API (NeoBusiness / NeoScan)

**Objetivo:** API comercial para que sistemas externos (incluidos **NeoBusiness** y **NeoScan**) emitan
y consulten DTE; producto vendible por volumen.

**Arquitectura:**
- Tablas `Connect_ApiKeys` (hash de la key + scopes + empresa + estado + rate-limit),
  `Connect_Webhooks` / `Connect_WebhookDeliveries` (entregas firmadas con reintentos). Logs reusando
  `Core_ApiUsageLog`.
- **Auth por API Key** (`X-Api-Key`) → middleware resuelve empresa + scopes + cuota (engancha con el
  `ApiQuotaMiddleware` ya existente del Sprint 20; el campo `ApiKeyId` ya está previsto).
- **Endpoints v1:** emitir DTE (factura/CCF/…), consultar estado, descargar PDF/JSON, alta de
  clientes/productos, **webhooks** de cambio de estado de DTE (PROCESADO/RECHAZADO/CONTINGENCIA).
- **Sandbox** (ambiente PRUEBAS) + **documentación pública** (OpenAPI).

**Consumo:**
- **NeoBusiness** (ERP/suite) → emite DTE desde sus ventas vía la API.
- **NeoScanAI** → registra compras/gastos/DTE recibidos vía la API y alimenta NeoProfit. Construir la
  parte IA con la skill `claude-api` (prompt caching).

**Modelo de negocio:** NeoConnect se vende aparte (integradores/ERPs pagan por volumen de API),
aprovechando cuotas por plan/API Key ya implementadas.

---

# 17. Convenciones para nuevo código

- **Interfaces en Application, implementaciones en Infrastructure.** Servicios devuelven `Result`/`Result<T>`.
- **Multiempresa:** todo dato lleva `EmpresaId`; datos de sistema `EmpresaId = null` (solo SuperAdmin).
- **Permisos:** `[RequirePermiso("...")]` en Api; en Web chequear claim/`HasPermiso` con bypass SuperAdmin.
- **Auditoría:** acciones críticas vía `IAuditoriaService`.
- **Migraciones:** patrón `SprintN_Tema` o nombre descriptivo; revisar el `.cs` generado (sin UpdateData
  espurios); aplicar con build fresco.
- **UI:** AppShell + `neostp.css` (tokens `var(--ns-*)`, clases `ns-*`); lookups vía `/api/lookups`.
- **Toggles Mock/Real** para integraciones externas; **secretos cifrados** con DataProtection, nunca en repo.
- **Definición de completado** (§11) antes de cerrar cualquier incremento.
- **Trabajo por sub-entregas pequeñas** (regla del proyecto): no entregar todo en un solo prompt;
  confirmar antes de avanzar a la siguiente sub-entrega; commit/push al cerrar (cuando se solicite).

---

> Documento generado como traspaso integral. Para el detalle operativo de comandos usar la skill
> `/neostp`; para el backlog 13–30 la skill `/neostp-sprints`. Fuentes: código en `src/`, `README.md`,
> `CONTEXTO-PROYECTO.md`, runbooks en `docs/`, design system en `/design/`.
