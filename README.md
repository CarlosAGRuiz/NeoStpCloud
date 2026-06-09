# NeoSTP Cloud

**Suite SaaS de facturación electrónica (DTE) y ERP/CRM-mini para El Salvador.**
Monolito modular en **.NET 10** con Web (MVC/Razor), API REST y un Worker de tareas en segundo plano.
Multi-empresa (multi-tenant por `EmpresaId`), licenciamiento por planes/módulos y RBAC granular.

> La app móvil (Flutter, repo aparte `neocloud_mobile_android`) consume **esta misma API**.
> Todo módulo nuevo se expone **API-first** (REST + UI web).

---

## Tabla de contenido

- [Stack](#stack)
- [Arquitectura](#arquitectura)
- [Módulos](#módulos)
- [Puesta en marcha](#puesta-en-marcha)
- [Configuración y secretos](#configuración-y-secretos)
- [Base de datos y migraciones](#base-de-datos-y-migraciones)
- [Pruebas](#pruebas)
- [API](#api)
- [Roadmap](#roadmap)
- [Convenciones](#convenciones)

---

## Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 10 |
| Web | ASP.NET Core MVC + Razor, design system propio `ns-*` (neostp.css), Bootstrap 5, Material Symbols |
| API | ASP.NET Core Web API + OpenAPI/Scalar (`/scalar/v1`) |
| Datos | EF Core 10 + SQL Server 2022 |
| Worker | `BackgroundService` (generación de alertas, colas de trabajo) |
| PDF | QuestPDF (Community) — facturas, recibos de nómina, **tickets térmicos** |
| Correo | MailKit (SMTP) con sender por empresa + fallback global; modo Mock para dev |
| Firma/DTE | XML/JWS, integración Ministerio de Hacienda (MH) El Salvador |
| Seguridad | JWT, DataProtection (`ISecretProtector`), políticas de contraseña, MFA |
| Tests | xUnit + FluentAssertions + NSubstitute (**586 unitarias + integración**) |

Solución: **`NeoSTP.slnx`**.

---

## Arquitectura

Monolito modular con separación por capas (Clean Architecture pragmática):

```
src/
  NeoSTP.Domain           Entidades y reglas de dominio (sin dependencias de infraestructura)
  NeoSTP.Application      Interfaces de servicios, DTOs, calculadoras puras, Result/PagedResult
  NeoSTP.Infrastructure   Implementaciones (EF Core, servicios, integraciones, persistencia, seed)
  NeoSTP.Api              Web API REST (controllers, autorización por módulo/permiso)
  NeoSTP.Web              App web MVC/Razor (panel de administración y operación)
  NeoSTP.Worker           Tareas en segundo plano (alertas, colas)
  NeoSTP.Shared           Utilidades compartidas (ApiResponse, CsvExporter, etc.)
tests/
  NeoSTP.Tests.Unit         Pruebas unitarias
  NeoSTP.Tests.Integration  Pruebas de integración (API)
```

**Patrones clave**

- **Multi-tenant**: todo dato se aísla por `EmpresaId`. La Web usa `IEmpresaContext.CurrentEmpresaId`
  (con "modo soporte" para SuperAdmin); la API resuelve `ICurrentUser.EmpresaId ?? empresaId` de la petición.
- **Autorización**: `[RequireModule("X")]` (la licencia del plan habilita el módulo) + `[RequirePermiso("Y")]`.
  La Web filtra por permiso y visibilidad de menú.
- **Result pattern**: `Result` / `Result<T>` en servicios; la API los mapea a `ApiResponse<T>` y a códigos HTTP.
- **Calculadoras puras y testeables** (`NominaCalculator`, `CobranzaCalculator`, `CuentasPagarCalculator`,
  `PosCalculator`, `EscPosTicketBuilder`): lógica de negocio sin dependencia de BD.
- **Seed determinista** en `SeedData.cs` (`HasData`, capturado por migraciones) + provisioning idempotente
  de la empresa de pruebas (`EmpresaPruebaSeeder`, con *backfill* de módulos del plan al arrancar).

---

## Módulos

Los módulos se habilitan por plan (Starter → Enterprise). Códigos de módulo y estado actual:

| Cód | Módulo | Descripción | Estado |
|---|---|---|---|
| 100 | CORE | Empresas, sucursales, PV, usuarios, roles/permisos, catálogos, auditoría | ✅ |
| 101 | NEODTE | Emisión DTE (8 tipos), certificación MH, eventos, contingencia, diagnóstico | ✅ |
| 102 | **NEOPOS** | Punto de venta: ventas, tickets, impresión, correo, promoción a DTE, **corte de caja** | ✅ S1–S4 |
| 103 | NEOSCANAI | Captura/OCR de documentos (backend; UI en la app) | ✅ backend |
| 104 | NEOPROFIT | P&L: ventas, costos, gastos/compras, rankings | ✅ |
| 105 | NEOBI | Reportes/BI | ⏳ |
| 106 | NEOCONNECT | API pública (API keys, webhooks, `/api/v1`) | ✅ |
| 107 | NEOPORTAL | Portal receptor | ⏳ |
| 108 | CONTINGENCIA | Contingencia avanzada y lotes | ✅ |
| 109 | EVENTOSDTE | Eventos DTE persistentes | ✅ |
| 110 | **INVENTARIO** | Existencias, kardex, costo promedio ponderado | ✅ |
| 111 | **COMPRAS** | Proveedores + cuentas por pagar (CxP) | ✅ |
| 112 | GASTOS | Control de gastos (parte de NeoProfit) | ✅ |
| 113 | **NEORRHH** | Recursos humanos + nómina (planilla quincenal ES) | ✅ |
| 115 | **NEOTESORERIA** | Cuentas (banco/caja) + movimientos | ✅ |

### Destacados de la suite

- **Facturación electrónica (DTE)** — 8 tipos de documento, certificación MH, firma, contingencia,
  eventos persistidos, diagnóstico de errores de Hacienda. Branding por empresa (logo/firma en PDF y correo).
- **NeoPOS** — ventas con carrito, **tickets térmicos 58/80mm** en PDF, **vista imprimible** (`window.print()`),
  **ESC/POS por red** (TCP 9100) a impresoras térmicas, **envío del ticket por correo**, resumen del día.
  Ventas como comprobante no fiscal **promovibles a Factura/CCF electrónica** (DTE) en un clic.
  **Corte de caja**: apertura con fondo, ventas del turno ligadas a la sesión y cierre con efectivo esperado vs contado (diferencia).
- **Inventario** — existencias por producto, **kardex** de movimientos y **costo promedio ponderado**
  (actualiza el costo del producto para NeoProfit); entradas/salidas/ajustes manuales y **auto-integración**:
  compra→entrada, venta POS→salida (con devolución al anular) y **alerta de stock bajo** en el centro de notificaciones.
- **NeoRRHH** — empleados/contratos, **planilla quincenal** con tablas **ISSS/AFP/Renta 2026** parametrizables,
  recibos PDF, exportes ISSS/AFP (CSV), cierre → gasto PLANILLA en NeoProfit.
- **Tesorería** — cuentas de banco/caja con saldo corriente; movimientos de ingreso/egreso con origen
  (planilla, gasto, compra, cobro) para conciliación.
- **Compras / CxP** — proveedores, facturas de compra, pagos; saldos y vencimientos; integra NeoProfit
  (gasto) y Tesorería (egreso al pagar).
- **Cobros / CxC** — saldos por cliente/factura, registro de pagos, QR/enlaces de cobro.
- **NeoProfit** — dashboard financiero (ventas, IVA, costos, ganancia, rankings, tendencia).
- **NeoConnect** — API pública v1 con API keys por scope y webhooks.
- **Alertas + push (FCM)**, **NeoScanAI** (OCR Gemini), **billing** multi-pasarela (Wompi/PayPal/Transferencia),
  **carga masiva** (Excel/CSV de clientes y productos), **onboarding** self-service.
- **Correo por empresa** — cada empresa configura su SMTP (contraseña cifrada); si no, usa el correo global.
  Configurable desde la web (`/correo`) **y la API** (`/api/correo`); el envío de DTE/tickets usa el SMTP de la empresa.

---

## Puesta en marcha

### Requisitos

- SDK de **.NET 10**
- **SQL Server 2022** (local, contenedor o LocalDB)
- (Opcional) Docker + Docker Compose

### Ejecutar localmente

```bash
# Restaurar y compilar
dotnet build NeoSTP.slnx -c Debug

# API (incluye OpenAPI en /openapi/v1.json y Scalar en /scalar/v1)
dotnet run --project src/NeoSTP.Api

# Web (panel de administración/operación)
dotnet run --project src/NeoSTP.Web

# Worker (alertas y colas)
dotnet run --project src/NeoSTP.Worker
```

La API aplica migraciones y siembra datos al arrancar (`DatabaseSeeder` + `EmpresaPruebaSeeder`).

### Docker

```bash
docker compose up --build
```

Levanta Web, Api y Worker (ver `docker-compose.yml` y los `Dockerfile` de cada proyecto).

---

## Configuración y secretos

> **Los secretos viven solo en `appsettings.Local.json` (ignorado por git). Nunca se commitean.**

Copia el ejemplo y complétalo:

```bash
cp src/NeoSTP.Api/appsettings.Local.example.json src/NeoSTP.Api/appsettings.Local.json
```

Claves relevantes (todas con valores externos/propios):

| Sección | Para qué |
|---|---|
| `ConnectionStrings:Default` | Conexión a SQL Server |
| `EmpresaPrueba` | Provisioning de la empresa de pruebas (`Enabled`, `Nit`, `PlanCodigo`, admin…) |
| `Email` | Correo global (`Provider`: `Mock` \| `Smtp`, host/credenciales) |
| `Scan:Gemini:ApiKey` | OCR real con Gemini Flash (NeoScanAI) |
| `Push:Fcm` | Service account de Firebase Cloud Messaging |
| `Pos` | Parámetros POS: `IvaTasa`, `AnchoTicketMm`, `MonedaSimbolo`, `PieTicket` |
| `Nomina` | Tablas ISSS/AFP/Renta (por defecto 2026 en código, parametrizables) |
| `Dte` | Ambiente, datos territoriales, credenciales MH (password/PFX se cargan por UI) |

Las contraseñas SMTP por empresa y los secretos sensibles se cifran con `ISecretProtector` (DataProtection).
Los tests que tocan SMTP leen credenciales de variables de entorno (`NEOSTP_SMTP_USER/PASS/TO`), nunca hardcodeadas.

---

## Base de datos y migraciones

```bash
# Crear una migración
dotnet ef migrations add NombreMigracion \
  --project src/NeoSTP.Infrastructure \
  --startup-project src/NeoSTP.Api \
  --output-dir Persistence/Migrations \
  --context NeoStpDbContext

# Aplicar (o se aplican solas al arrancar la API)
dotnet ef database update \
  --project src/NeoSTP.Infrastructure --startup-project src/NeoSTP.Api
```

---

## Pruebas

```bash
dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj
dotnet test tests/NeoSTP.Tests.Integration/NeoSTP.Tests.Integration.csproj
```

**586 pruebas unitarias** + integración. Las calculadoras puras tienen cobertura específica
(`PosCalculator`, `CorteCajaCalculator`, `NominaCalculator`, `CobranzaCalculator`, `CuentasPagarCalculator`, `CostoPromedioCalculator`, `EscPosTicketBuilder`).

---

## API

- **OpenAPI**: `/openapi/v1.json` · **Scalar** (explorador interactivo): `/scalar/v1`.
- **Autenticación**: JWT (Bearer) para usuarios; **API Key** por scope para NeoConnect (`/api/v1`).
- **App móvil**: emisión de DTE en un paso (`POST /api/dte/emitir`), cobros, scan, alertas, RRHH, POS, etc.
- **README técnico de la API**: [`src/NeoSTP.Api/README.md`](src/NeoSTP.Api/README.md).

Endpoints por área (ejemplos): `api/dte/*`, `api/cobros/*`, `api/scanai/*`, `api/alertas/*`,
`api/profit/*`, `api/rrhh/*`, `api/tesoreria/*`, `api/compras/*`, `api/inventario/*`,
`api/pos/*` (incl. `api/pos/caja/*`), `api/correo`, `api/v1/*` (NeoConnect).

Documentación adicional en `docs/` (`NeoConnect-API-v1.md`, `NeoCloud-Mobile-API.md`, planes y notas).

---

## Roadmap

Plan vivo en **[`docs/Plan-V2-ERP-CRM.md`](docs/Plan-V2-ERP-CRM.md)** — NeoSTP como **ERP + CRM-mini**.

- **Fase A — ERP interno ✅**: NeoRRHH (nómina quincenal), Tesorería.
- **Fase B — Egresos/stock ✅**: Compras/CxP · Inventario · auto-integración (compra/venta) + stock bajo.
- **Fase C — Comercial**: NeoPOS ✅ (S1–S3) · NEOCRM ⏳ · NeoPortal ⏳.
- **Fase D — Cierre fiscal/contable**: Libro IVA/F-07, NEOCONTA ⏳.
- **Fase E — Escala**: storage externo, Redis, observabilidad, cumplimiento ⏳.

> Fuera de alcance de este repo: la app Flutter y la UI de NeoScan (viven en `neocloud_mobile_android`).

---

## Convenciones

- **Diseño**: design system `ns-*` (`ns-page-head`, `ns-toolbar`, `ns-badge--*`, `ns-kpi`, `ns-empty`…),
  sin emojis en la UI, íconos Material Symbols.
- **Commits**: se crean/pushean solo cuando se solicita; en rama, no directo a `main` por defecto.
- **Exportes CSV**: `CsvExporter` (RFC 4180 + BOM). **PDF**: QuestPDF.
- **Seguridad**: secretos solo en `appsettings.Local.json`; `.codex/` y binarios grandes ignorados por git.

---

_NeoSTP Cloud — El Salvador-first. Construido para emitir DTE y operar el negocio (ventas, nómina, tesorería,
compras) desde una sola plataforma._
