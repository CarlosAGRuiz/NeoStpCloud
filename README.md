# NeoSTP Cloud

**Suite SaaS de facturación electrónica (DTE) y ERP/CRM para El Salvador.**
Monolito modular en **.NET 10** con Web (MVC/Razor), API REST y Worker de tareas en segundo plano.
Multi-empresa (multi-tenant por `EmpresaId`), licenciamiento por planes/módulos y RBAC granular.

> La app móvil (Flutter, repo aparte
> [`neocloud_mobile_android`](https://github.com/manuelberganza-dev/neocloud_mobile_android))
> consume **esta misma API**. En este repositorio solo se trabaja backend/API para mobile:
> contrato, permisos, datos demo y pruebas.
> Todo módulo nuevo se expone **API-first** (REST + UI web).

**Estado actualizado 2026-06-15: Fases V2/V2.5, API mobile AM-0..AM-6, HB-1, HB-3/HB-4, HB-5 y HB-6 cerrados
operativos.** El producto opera el ciclo completo de un
negocio salvadoreño: emite DTE certificados contra Hacienda, vende por POS, cobra, compra, maneja
inventario, paga planilla, concilia el banco, lleva libros fiscales y contabilidad mínima, y da
autoservicio al cliente final por un portal público.

---

## Tabla de contenido

- [Qué hace la suite](#qué-hace-la-suite)
- [Stack](#stack)
- [Arquitectura](#arquitectura)
- [Módulos](#módulos)
- [Fases del proyecto](#fases-del-proyecto)
- [Pruebas](#pruebas)
- [Puesta en marcha](#puesta-en-marcha)
- [Configuración y secretos](#configuración-y-secretos)
- [Base de datos y migraciones](#base-de-datos-y-migraciones)
- [API](#api)
- [Operación y escala](#operación-y-escala)
- [Documentación](#documentación)
- [Roadmap](#roadmap)
- [Convenciones](#convenciones)

---

## Qué hace la suite

El recorrido completo de un cliente, de punta a punta:

1. **Onboarding self-service**: checklist de activación derivado de datos reales (perfil, config
   DTE, certificado, catálogo, primer DTE) + asistente `/onboarding`.
2. **Factura**: 9 tipos de DTE (01, 03, 04, 05, 06, **07 Retención**, 11, 14, 15) firmados JWS y
   transmitidos a Hacienda (certificación apitest real completada en Sprint 12), con contingencia
   por lotes, eventos persistidos y diagnóstico de errores MH con causas/acciones sugeridas.
3. **Vende por POS**: carrito, tickets térmicos 58/80mm (PDF/ESC-POS red/correo), sesión y corte
   de caja, promoción de ticket a Factura/CCF electrónica en un clic.
4. **Cobra**: CxC con saldos derivados, pagos, QR/enlaces de cobro, **recordatorios automáticos**
   configurables por empresa (email + WhatsApp pluggable) con historial, y **portal del receptor**
   (`/portal/{token}`) donde el cliente final consulta su DTE (HTML/JSON/PDF) y estado de cuenta
   sin usuario ni contraseña.
5. **Compra y controla stock**: proveedores, facturas de compra, CxP, pagos; inventario con
   kardex y costo promedio ponderado; auto-integración compra→entrada, venta POS→salida, alerta
   de stock bajo.
6. **Paga planilla**: NeoRRHH con planilla quincenal ISSS/AFP/Renta 2026, recibos PDF, exportes
   CSV y cierre que fluye como gasto.
7. **Concilia el banco**: importa el estado de cuenta (CSV/Excel), matching automático por
   monto/fecha/referencia con confianza ALTA/MEDIA, **conciliación parcial N:1** (un depósito
   agrupado contra varios movimientos internos) y combinaciones sugeridas.
8. **Cierra el mes**: libros IVA (consumidor/contribuyentes/compras) + resumen **F-07** (NeoBI),
   asientos automáticos de doble partida con reversa espejo y balanza de comprobación (NeoConta),
   y P&L con rankings y tendencia (NeoProfit).
9. **Se integra**: NeoConnect (API pública v1 con API keys por scope + webhooks firmados HMAC),
   NeoScanAI (OCR Gemini de documentos → gasto/compra/DTE recibido), alertas + push FCM.
10. **Se administra**: billing multi-pasarela (Wompi/PayPal/Transferencia/Stripe/MercadoPago),
    branding por empresa (logo/firma en PDF y correo), SMTP propio por empresa, carga masiva
    Excel/CSV, auditoría, MFA TOTP, rate limiting y panel operativo SuperAdmin.

## Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 10 |
| Web | ASP.NET Core MVC + Razor, design system propio `ns-*` (neostp.css), Bootstrap 5, Material Symbols |
| API | ASP.NET Core Web API + OpenAPI/Scalar (`/scalar/v1`) |
| Datos | EF Core 10 + SQL Server 2022 (55 migraciones, ~89 tablas) |
| Worker | `BackgroundService` ×8 (contingencia, lotes, tokens, backups, webhooks, alertas, recordatorios, purga auditoría) |
| PDF | QuestPDF (Community) — facturas, recibos de nómina, tickets térmicos |
| Correo | MailKit (SMTP) con sender por empresa + fallback global; modo Mock para dev |
| WhatsApp | Meta Cloud API pluggable (`WhatsApp:Provider=Meta`), mock por defecto |
| Firma/DTE | XML/JWS RS512, integración Ministerio de Hacienda (MH) El Salvador |
| Seguridad | JWT, API keys SHA-256 por scope, DataProtection (`ISecretProtector`), password policy, MFA TOTP, IP allowlist, rate limiting |
| Observabilidad | Health checks (BD/correo/storage), Serilog estructurado, **OpenTelemetry OTLP opcional** + Meter `NeoSTP` |
| Escala | Caché distribuida Memory/**Redis** para lookups, storage externo opcional para blobs de scan |
| i18n / a11y | es (default) + en por cookie de cultura; skip-link, focus visible, aria-labels |
| Tests | xUnit + FluentAssertions + NSubstitute — **705 unitarias + 9 integración**, CI en GitHub Actions |

Solución: **`NeoSTP.slnx`**.

## Arquitectura

Monolito modular con separación por capas (Clean Architecture pragmática):

```
src/
  NeoSTP.Domain           Entidades y reglas de dominio (sin dependencias de infraestructura)
  NeoSTP.Application      Interfaces de servicios, DTOs, calculadoras puras, Result/PagedResult
  NeoSTP.Infrastructure   Implementaciones (EF Core, servicios, integraciones, persistencia, seed)
  NeoSTP.Api              Web API REST (controllers, autorización por módulo/permiso)
  NeoSTP.Web              App web MVC/Razor (panel de administración y operación + portal público)
  NeoSTP.Worker           Tareas en segundo plano (8 jobs)
  NeoSTP.Shared           Utilidades compartidas (ApiResponse, CsvExporter, etc.)
tests/
  NeoSTP.Tests.Unit         705 pruebas unitarias
  NeoSTP.Tests.Integration  9 pruebas de integración (API)
```

**Patrones clave**

- **Multi-tenant**: todo dato se aísla por `EmpresaId`. La Web usa `IEmpresaContext.CurrentEmpresaId`
  (con "modo soporte" para SuperAdmin); la API resuelve `ICurrentUser.EmpresaId ?? empresaId` de la petición.
- **Autorización**: `[RequireModule("X")]` (la licencia del plan habilita el módulo) + `[RequirePermiso("Y")]`.
  Roles seed: SUPERADMIN, ADMIN, OPERADOR, CONTADOR, READONLY con permisos granulares (300–420).
- **Result pattern**: `Result` / `Result<T>` en servicios; la API los mapea a `ApiResponse<T>` y a códigos HTTP.
- **Calculadoras puras y testeables** (`NominaCalculator`, `CobranzaCalculator`, `PosCalculator`,
  `LibroIvaCalculator`, `ConciliacionCalculator`, `ProfitCalculator`, `EscPosTicketBuilder`…):
  lógica de negocio sin dependencia de BD.
- **Proveedores pluggables por configuración**: correo (Mock/Smtp), Hacienda (Mock/Http),
  OCR (Mock/Gemini), push (Mock/Fcm), WhatsApp (Mock/Meta), caché (Memory/Redis), storage de
  scans (Database/FileSystem), telemetría (off/OTLP). **Los defaults funcionan sin servicios externos.**
- **Seed determinista** en `SeedData.cs` (`HasData`, capturado por migraciones) + provisioning idempotente
  de la empresa de pruebas (`EmpresaPruebaSeeder`, con *backfill* de módulos del plan y datos demo
  API/mobile/comerciales al arrancar).
- **Lookups centralizados** (`ILookupService` + `/api/lookups`): catálogos MH (CAT-001..033),
  cascada territorial Departamento→Municipio→Distrito y maestros, con caché de dos niveles.

## Módulos

Los módulos se habilitan por plan (Starter → Enterprise). Los 17 están **completos**:

| Cód | Módulo | Descripción |
|---|---|---|
| 100 | CORE | Empresas, sucursales, PV, usuarios, roles/permisos, catálogos MH, auditoría |
| 101 | NEODTE | Emisión DTE (9 tipos), certificación MH, firma JWS, diagnóstico de errores Hacienda |
| 102 | NEOPOS | Punto de venta: ventas, tickets, impresión red, promoción a DTE, corte de caja |
| 103 | NEOSCANAI | Captura/OCR de documentos (Gemini) → gasto/compra/DTE recibido |
| 104 | NEOPROFIT | P&L: ventas, costos, gastos/compras, rankings, tendencia |
| 105 | NEOBI | Reportes fiscales: libros IVA ventas/compras + resumen F-07 (+CSV) |
| 106 | NEOCONNECT | API pública v1 (API keys por scope, webhooks HMAC, rate limit) |
| 107 | NEOPORTAL | Portal del receptor: enlaces públicos con token a DTE y estado de cuenta |
| 108 | CONTINGENCIA | Contingencia avanzada y lotes (MOMENTO 3 completo) |
| 109 | EVENTOSDTE | Eventos DTE persistentes (invalidación, contingencia) |
| 110 | INVENTARIO | Existencias, kardex, costo promedio ponderado, auto-integración |
| 111 | COMPRAS | Proveedores + cuentas por pagar (CxP) |
| 112 | GASTOS | Control de gastos (parte de NeoProfit) |
| 113 | NEORRHH | Recursos humanos + nómina quincenal ES |
| 114 | NEOCRM | Contactos, pipeline kanban, actividades, cotizaciones → DTE |
| 115 | NEOTESORERIA | Cuentas banco/caja + movimientos + **conciliación bancaria N:1** |
| 116 | NEOCONTA | Contabilidad mínima: catálogo base, asientos automáticos, balanza |

Además, transversales: Billing SaaS multi-pasarela, Legal/compliance, Hardening (backups, cuotas,
IP allowlist, MFA), Branding, Correo por empresa, Onboarding, Alertas + push, carga masiva,
buscador global Ctrl+K y dashboard con KPIs de DTE + negocio (cartera vencida, tesorería, alertas).

## Fases del proyecto

| Fase | Contenido | Estado |
|---|---|---|
| Sprints 1–12 | Núcleo DTE: emisión, firma, transmisión, **certificación real contra apitest de Hacienda** | ✅ |
| Sprints 13–21 | Catálogos MH, certificación (módulo), eventos, contingencia, diagnóstico, legal, billing, hardening, UI/UX (design system `ns-*`) | ✅ |
| Sprints 22–30 | NeoProfit, NeoScanAI, NeoConnect, backend móvil (B-1..B-6: emisión 1 paso, cobros, scan, alertas, QR, NIT), pagos LATAM, lookups, carga masiva, onboarding, branding | ✅ |
| **Fase A/B (V2)** | ERP interno: NeoRRHH, Tesorería, Compras/CxP, Inventario + glue automático | ✅ |
| **Fase C (V2)** | Comercial: NeoPOS S1–S4, NEOCRM (pipeline + cotización→DTE), NeoPortal | ✅ |
| **Fase D (V2)** | Cierre fiscal/contable: NeoBI fiscal, NeoConta, recordatorios configurables, conciliación bancaria | ✅ |
| **Fase E / V2.5** | Conciliación parcial N:1, WhatsApp Meta real, OpenTelemetry + panel operativo + health ampliado, Redis + storage externo, purga de auditoría, i18n es/en + a11y | ✅ |
| Recorrido UX | Pruebas como cliente real → 7 bugs corregidos (permisos rotos, selects vacíos, layout) + 5 mejoras (dashboard, Ctrl+K, CTAs, historial recordatorios) | ✅ |

Planes detallados con estados y evidencia: [`docs/Plan-Cierre-Fase-V2.md`](docs/Plan-Cierre-Fase-V2.md)
y [`docs/Plan-V2.5.md`](docs/Plan-V2.5.md).

## Pruebas

```bash
dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj
dotnet test tests/NeoSTP.Tests.Integration/NeoSTP.Tests.Integration.csproj
```

- **705 pruebas unitarias + 9 de integración**, con CI en GitHub Actions para cada push/PR a main.
  Sin dependencias externas: EF InMemory, HTTP simulado y proveedores mock.
- **Validacion local 2026-06-15:** `dotnet build NeoSTP.slnx` termino con 0 warnings/0 errores y
  `dotnet test NeoSTP.slnx` paso con 705 unitarias + 9 integracion. HB-1, HB-3/HB-4, HB-5 y HB-6 quedaron
  cerrados operativos.
- **HB-3/HB-4 demo readiness**: `DemoReadinessContractTests` congela rutas API criticas, permisos,
  modulos licenciables, API publica NeoConnect v1, rutas Web y existencia de vistas Razor para demos.
- **HB-5 datos demo comerciales**: `EmpresaPruebaSeederTests` valida que el seed opcional sea
  idempotente y deje DTE, compras/CxP, inventario, tesoreria, portal, CRM, RRHH y Profit con datos
  utiles para demo.
- **HB-6 contratos API/versionado**: `ApiVersioningContractTests` congela la politica Tier A `/api/*`,
  NeoConnect `/api/v1`, content-types de descargas binarias y enlaces documentales.
- **Certificación DTE real**: matriz de escenarios transmitida y PROCESADA contra el ambiente de
  pruebas (apitest) de Hacienda — Sprint 12 y módulo de certificación.
- **Pruebas tipo cliente** (documentadas): recorrido de ~45 pantallas con sesión real de ADMIN,
  flujos E2E por HTTP (portal público con token, fiscal vs. contabilidad cuadrando al centavo,
  conciliación N:1 en vivo, i18n es↔en) — ver
  [`docs/Analisis-Pruebas-Cliente-V2.md`](docs/Analisis-Pruebas-Cliente-V2.md) y
  [`docs/Analisis-UX-Cliente.md`](docs/Analisis-UX-Cliente.md).
- Cobertura específica de calculadoras puras: POS, corte de caja, nómina, cobranza, CxP, costo
  promedio, ESC/POS, libro IVA, conciliación (1:1 y combinaciones), profit.

## Puesta en marcha

### Requisitos

- SDK de **.NET 10**
- **SQL Server 2022** (local, contenedor o LocalDB)
- (Opcional) Docker + Docker Compose

### Ejecutar localmente

```bash
# Restaurar y compilar
dotnet build NeoSTP.slnx -c Debug

# API (OpenAPI en /openapi/v1.json y Scalar en /scalar/v1)
dotnet run --project src/NeoSTP.Api

# Web (panel de administración/operación + portal público)
dotnet run --project src/NeoSTP.Web

# Worker (jobs en segundo plano)
dotnet run --project src/NeoSTP.Worker
```

La API y la Web aplican migraciones y siembran datos al arrancar (`DatabaseSeeder` +
`EmpresaPruebaSeeder`). La Web exige HTTPS para cookies de sesión.

### Docker

```bash
docker compose up --build
```

## Configuración y secretos

> **Los secretos viven solo en `appsettings.Local.json` (ignorado por git). Nunca se commitean.**

```bash
cp src/NeoSTP.Api/appsettings.Local.example.json src/NeoSTP.Api/appsettings.Local.json
```

Toggles principales (defaults = funcionan sin servicios externos):

| Sección | Para qué | Default |
|---|---|---|
| `ConnectionStrings:NeoStpDb` | Conexión a SQL Server | — |
| `Jwt:Key` | Clave de firma JWT (≥32 chars; igual en Api y Web) | placeholder |
| `Email` | Correo global `Mock` \| `Smtp` (+SMTP por empresa desde la UI/API) | Mock |
| `Hacienda:Client` | Transmisión `Mock` \| `Http` (apitest real) | Mock |
| `Dte` / `Dte:Territorial` | Firma, ambiente (00/01) y territoriales por defecto | — |
| `WhatsApp:Provider` | `Mock` \| `Meta` (Cloud API: Token + PhoneNumberId) | Mock |
| `Scan:Provider` / `Scan:Storage` | OCR `Mock` \| `Gemini`; blobs `Database` \| `FileSystem` | Mock/Database |
| `Push:Provider` | `Mock` \| `Fcm` (service account Firebase) | Mock |
| `Cache:Provider` | `Memory` \| `Redis` (multi-instancia, lookups con invalidación) | Memory |
| `Observability:Otlp:Endpoint` | Trazas + métricas OpenTelemetry (vacío = sin overhead) | vacío |
| `Billing` | Pasarela `Mock` \| Wompi/PayPal/Stripe/MercadoPago + transferencia manual | Mock |
| `Worker:*` | Jobs: recordatorios, alertas, backups, purga de auditoría (off por defecto) | off |
| `EmpresaPrueba` | Provisioning de la empresa demo con admin, usuarios mobile y datos comerciales opt-in | — |

Las contraseñas SMTP por empresa y los secretos sensibles se cifran con `ISecretProtector`
(DataProtection). Los tests que tocan SMTP leen variables de entorno, nunca valores hardcodeados.

## Base de datos y migraciones

```bash
# Crear una migración
dotnet ef migrations add NombreMigracion \
  --project src/NeoSTP.Infrastructure \
  --startup-project src/NeoSTP.Api \
  --output-dir Persistence/Migrations \
  --context NeoStpDbContext

# Aplicar (o se aplican solas al arrancar la API/Web)
dotnet ef database update \
  --project src/NeoSTP.Infrastructure --startup-project src/NeoSTP.Api
```

55 migraciones acumulativas; el seed (módulos, permisos, planes, catálogos) viaja en las
migraciones (`HasData`), nunca a mano. ~89 tablas con prefijo por área (`Core_`, `Dte_`, `Pos_`,
`Crm_`, `Tes_`, `Conta_`, `Rrhh_`, `Inv_`, `Compras_`, `Cobros_`, `Notif_`, `Scan_`, `Connect_`…).

## API

- **OpenAPI**: `/openapi/v1.json` · **Scalar** (explorador interactivo): `/scalar/v1`.
- **Autenticación**: JWT (Bearer) para usuarios; **API Key** por scope para NeoConnect (`/api/v1`).
- **App móvil**: emisión de DTE en un paso (`POST /api/dte/emitir`), cobros, scan, alertas, RRHH, POS, etc.
- **README técnico de la API** (catálogo completo de endpoints):
  [`src/NeoSTP.Api/README.md`](src/NeoSTP.Api/README.md).
- **Politica de contratos y versionado**:
  [`docs/API-Contratos-Versionado.md`](docs/API-Contratos-Versionado.md).

Áreas: `api/auth`, `api/dte/*`, `api/cobros/*` (incl. recordatorios), `api/portal/*`,
`api/conta/*`, `api/reportes/fiscal/*`, `api/tesoreria/*` (incl. conciliación), `api/compras/*`,
`api/inventario/*`, `api/pos/*` (incl. caja), `api/crm/*` (incl. cotizaciones), `api/rrhh/*`,
`api/profit/*`, `api/scanai/*`, `api/alertas/*`, `api/lookups/*`, `api/correo`, `api/connect/*`
y `api/v1/*` (NeoConnect público).

## Operación y escala

- **Health**: `/health/live` y `/health/ready` (BD + configuración de correo + storage escribible)
  en API y Web.
- **Panel operativo SuperAdmin** (`/Soporte/Operacion`): empresas activas, transmisión DTE
  24h/7d, top rechazos de Hacienda por empresa, alertas, recordatorios, portal y API keys.
- **Telemetría**: con `Observability:Otlp:Endpoint`, API/Web/Worker exportan trazas y métricas
  (ASP.NET Core, HttpClient, runtime y el Meter `NeoSTP`: DTE emitidos, errores MH, recordatorios,
  accesos al portal — etiquetados por empresa).
- **Runbook** (despliegue, backup/restore, rotación JWT/certificados, retención, checklists de
  secretos y release, incidentes comunes): [`docs/Runbook-V2.md`](docs/Runbook-V2.md).
- **CI**: GitHub Actions (build + tests) en cada push/PR a `main`.

## Documentación

| Documento | Contenido |
|---|---|
| [`CONTEXTO-PROYECTO.md`](CONTEXTO-PROYECTO.md) | Contexto maestro: estado, módulos, DTE/Hacienda a fondo, fases |
| [`src/NeoSTP.Api/README.md`](src/NeoSTP.Api/README.md) | API: auth, formato, catálogo completo de endpoints |
| [`docs/Plan-Cierre-Fase-V2.md`](docs/Plan-Cierre-Fase-V2.md) | Fase V2 por sprint con entregado y validación |
| [`docs/Plan-V2.5.md`](docs/Plan-V2.5.md) | Fase V2.5 (escala/proveedores reales) con evidencia de pruebas |
| [`docs/Plan-Hallazgos-Bugs-Demo.md`](docs/Plan-Hallazgos-Bugs-Demo.md) | Sprints de consolidación para hallazgos, bugs y preparación de demos |
| [`docs/Plan-Hallazgos-Api-Mobile.md`](docs/Plan-Hallazgos-Api-Mobile.md) | Hallazgos y sprints API contra la app Android existente |
| [`docs/Runbook-Api-Mobile-Demo.md`](docs/Runbook-Api-Mobile-Demo.md) | Preparacion y checklist de demo API mobile |
| [`docs/Plan-Pruebas-Web-Api-Demos.md`](docs/Plan-Pruebas-Web-Api-Demos.md) | Plan recurrente de pruebas Web/API para demos comerciales y técnicas |
| [`docs/API-Contratos-Versionado.md`](docs/API-Contratos-Versionado.md) | Politica HB-6 de contratos API, versionado, deprecacion y descargas binarias |
| [`docs/Runbook-V2.md`](docs/Runbook-V2.md) | Operación: despliegue, backup, rotación, retención |
| [`docs/Analisis-Pruebas-Cliente-V2.md`](docs/Analisis-Pruebas-Cliente-V2.md) | Pruebas E2E en vivo del cierre V2 |
| [`docs/Analisis-UX-Cliente.md`](docs/Analisis-UX-Cliente.md) | Recorrido UX completo: bugs encontrados y mejoras |
| [`docs/NeoConnect-API-v1.md`](docs/NeoConnect-API-v1.md) | API pública para integradores |
| [`docs/NeoCloud-Mobile-API.md`](docs/NeoCloud-Mobile-API.md) | Contrato API para la app Flutter |

## Roadmap

Prioridad inmediata antes de nuevos módulos: con API mobile, HB-1, HB-3/HB-4, HB-5 y HB-6 cerrados,
ejecutar HB-7 del plan de consolidación [`docs/Plan-Hallazgos-Bugs-Demo.md`](docs/Plan-Hallazgos-Bugs-Demo.md):
storage, secretos y retencion para documentos fiscales y ambientes demo/productivos.

Lo construible está construido; lo pendiente depende de insumos externos o es V3:

- **Credenciales reales** (solo configuración, el código ya está): WhatsApp Business (Meta) +
  plantilla aprobada, Redis productivo, collector OTLP, pasarelas de pago, certificado DTE de
  producción (ambiente 01) por cliente.
- **App Flutter** (`neocloud_mobile_android`): la app ya fue trabajada en repo aparte; aqui se mantiene
  el contrato API, pruebas y datos demo. API mobile AM-0..AM-6 esta cerrada operativa al 100%.
- **V3 (backlog)**: SSO/SAML, white label, marketplace, SDKs NeoConnect, multi-moneda, BI
  predictivo, catálogo contable personalizable, órdenes de compra, vacaciones/aguinaldo.

## Convenciones

- **Diseño**: design system `ns-*` (`ns-page-head`, `ns-toolbar`, `ns-badge--*`, `ns-kpi`, `ns-empty`…),
  sin emojis en la UI, íconos Material Symbols.
- **Commits**: se crean/pushean solo cuando se solicita.
- **Exportes CSV**: `CsvExporter` (RFC 4180 + BOM). **PDF**: QuestPDF.
- **Seguridad**: secretos solo en `appsettings.Local.json`; probar cada pantalla con un usuario
  ADMIN real (no SUPERADMIN, que bypassa permisos).

---

_NeoSTP Cloud — El Salvador-first. Construido para emitir DTE y operar el negocio completo
(ventas, cobros, compras, inventario, nómina, tesorería, contabilidad) desde una sola plataforma._
