# 📘 Contexto Maestro — NeoSTP Cloud · NeoSTP Business Suite

> Documento único de contexto del proyecto. Reúne: estado real del sistema (README),
> arquitectura, base de datos, **explicación detallada del funcionamiento DTE/Hacienda**,
> catálogos MH, módulos de mantenimiento, plan de trabajo para completar la suite,
> plan de mejora de UI, skills, y análisis/mejora de código.
>
> **Versión:** Sprint 12 · **Rama:** `main` · **Build:** ✅ 0 errores · **Tests:** 106/106
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
- **xUnit + FluentAssertions** (106 tests)

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
│   ├── NeoSTP.Tests.Unit       # 106 tests
│   └── NeoSTP.Tests.Integration
├── design/                     # Design system + 7 mockups (Stitch) — fuente de verdad UI
└── docs/                       # Runbooks (mocks→real, matriz pruebas)
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
automática · Toggles Mock/Real · 106 tests unit/integración.

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
`Sprint9_RetransmisionTracking` · `Sprint10_DteCorrelativos` · `Sprint12_DistritoCAT008`.

## SuperAdmin inicial
`superadmin` / `ChangeMe!2026` (cambiar en el primer login). El SuperAdmin no pertenece a
ninguna empresa; opera pantallas multi-tenant en **modo soporte** (selecciona empresa, se
guarda en cookie, `IEmpresaContext` scope los queries).

---

# 4. Base de datos

## Tablas actuales (24)

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

## Tablas propuestas (por módulo pendiente)

| Módulo | Tablas |
|---|---|
| **Eventos DTE** | `Dte_Eventos`, `Dte_EventoJson`, `Dte_EventoRespuestasHacienda`, `Dte_EventoDocumentosRelacionados` |
| **Certificación** | `Dte_CertificacionMatriz`, `Dte_CertificacionEscenarios`, `Dte_CertificacionPruebas`, `Dte_CertificacionErrores` |
| **Catálogos MH** | (extender `Core_Catalogos` con `Version`, `ParentCodigo`, `MetadataJson`) |
| **NeoProfit** | `Profit_Gastos`, `Profit_Compras`, `Profit_SnapshotsMensuales`, `Profit_Alertas` |
| **NeoScanAI** | `Scan_Documentos`, `Scan_DocumentoResultados`, `Scan_DocumentoCampos`, `Scan_DocumentoArchivos`, `Scan_DocumentoEventos`, `Dte_DocumentosRecibidos` |
| **NeoConnect** | `Connect_ApiKeys`, `Connect_Webhooks`, `Connect_WebhookDeliveries`, `Connect_ApiLogs`, `Connect_SandboxSettings` |
| **NeoPOS** | `Pos_Cajas`, `Pos_Aperturas`, `Pos_Ventas`, `Pos_VentaDetalles`, `Pos_Pagos`, `Pos_Cierres` |
| **NeoPortal** | `Portal_Accesos`, `Portal_Solicitudes`, `Portal_TokensPublicos` |
| **Mobile** | `Mobile_Dispositivos`, `Mobile_Sesiones`, `Mobile_SyncLog` |
| **Billing** | `Billing_Customers`, `Billing_Subscriptions`, `Billing_Payments`, `Billing_Invoices`, `Billing_WebhookEvents`, `Billing_PlanProviderMappings` |
| **Legal** | `Core_UserConsents` |
| **Hardening** | `Ops_BackupJobs`, `Core_ApiUsageLog`, `Core_ApiQuotas`, `Core_AdminIpAllowlist` |

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
| CAT-005 | Tipo Contingencia | ⚠️ |
| CAT-006 | Retención IVA | ❌ |
| CAT-007 | Tipo Generación Documento | ⚠️ |
| CAT-008 | **Distrito** (división 2024) | ⚠️ infra lista, datos parciales |
| CAT-009 | Tipo Establecimiento | ✅ |
| CAT-010 | Código Servicio Médico | ❌ |
| CAT-011 | Tipo Ítem | ⚠️ |
| CAT-012 | Departamento | ✅ (14, falta `00` extranjero) |
| CAT-013 | **Municipio** (división 2024) | ⚠️ 42 sembrados, faltan códigos MH reales (44) |
| CAT-014 | Unidad Medida | ⚠️ |
| CAT-015 | Tributos | ⚠️ (IVA `20`, export `C3`) |
| CAT-016 | Condición Operación | ✅ |
| CAT-017 | Forma Pago | ✅ |
| CAT-018 | Plazo | ❌ |
| CAT-019 | Actividad Económica | ❌ (lista grande) |
| CAT-020 | País | ⚠️ (EUA `9539`, SV `9300`) |
| CAT-021 | Otros Documentos Asociados | ❌ |
| CAT-022 | Tipo Documento Identificación | ✅ (mapeo interno→MH hecho) |
| CAT-023 | Tipo Especial | ❌ |
| CAT-024 | Motivo Evento (invalidación) | ⚠️ |
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

## 6.2 Módulo de Mantenimiento de Catálogos (PROPUESTO) ⭐

Pantalla/API de administración para **gestionar todos los catálogos** del sistema sin recompilar.

**Funciones:**
- **Listar** catálogos y sus ítems (con búsqueda y filtros).
- **Crear / Editar / Borrar** ítems de catálogo (soft-delete con `Activo`).
- **Importar** desde CSV/Excel/JSON (carga masiva de catálogos MH oficiales).
- **Exportar** a CSV/Excel/JSON (respaldo y revisión).
- **Versionar** catálogos (campo `Version`) para rastrear cambios normativos MH.
- **Cascadas territoriales** (Departamento → Municipio → Distrito) con `ParentCodigo`.
- **Metadata** por ítem (`MetadataJson`: ej. `codigoMH`, `zona`, `parent`).
- **Mapeo interno ↔ MH** editable (ej. unidad medida interna → CAT-014).
- **Auditoría** de cambios (quién, cuándo, qué cambió).

**Endpoints propuestos:**
```
GET    /api/catalogos                          # ya existe (listado)
GET    /api/catalogos/{codigo}/items           # ya existe
POST   /api/catalogos                          # crear catálogo
PUT    /api/catalogos/{codigo}                 # editar metadata del catálogo
POST   /api/catalogos/{codigo}/items           # crear ítem
PUT    /api/catalogos/{codigo}/items/{id}      # editar ítem
DELETE /api/catalogos/{codigo}/items/{id}      # borrar/inactivar ítem
POST   /api/catalogos/{codigo}/import          # importar CSV/Excel/JSON
GET    /api/catalogos/{codigo}/export?format=  # exportar
POST   /api/catalogos/seed-mh                  # sembrar/actualizar catálogos MH oficiales
GET    /api/catalogos/{codigo}/items?parent=   # cascada (hijos de un padre)
```

**Tabla:** extender `Core_Catalogos` / `Core_CatalogoItems` con: `Version`, `ParentCodigo`,
`MetadataJson`, `Activo`, `EsSistema` (los del sistema no se borran), `CreatedBy`/`UpdatedBy`.

**Permisos:** `Core.Catalogos.Ver`, `Core.Catalogos.Administrar`, `Core.Catalogos.Importar`.

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
**Catálogos:** `GET /api/catalogos` · `GET /api/catalogos/{codigo}/items`
**Clientes/Productos:** `GET/POST/PUT /api/clientes` · `/api/productos` · `PATCH .../inactivar`
**Config DTE:** `GET/PUT /api/dte/configuracion` · `POST .../certificado` · `DELETE .../certificado` · `POST .../probar-conexion`
**Documentos DTE:** `GET /api/dte/documentos` · `POST /api/dte/{factura|credito-fiscal|nota-credito|nota-debito|sujeto-excluido|documentos}` · `POST .../{id}/{generar|validar|firmar|enviar|invalidar|reenviar}` · `GET .../{id}/{pdf|json}`
**Eventos DTE:** `POST /api/dte/evento/{contingencia|invalidacion|operaciones-especiales|retorno}`
**Dashboard:** `GET /api/dashboard/empresa` · `GET /api/dashboard/superadmin`
**Diagnóstico:** `GET /health` · `GET /openapi/v1.json` (Development)

## Propuestos (módulos pendientes)

**Certificación:** `GET /api/certificacion/{progreso|matriz|resumen|errores}` · `GET .../tipos/{codigo}/escenarios` · `POST .../tipos/{codigo}/generar-prueba` · `POST .../documentos/{id}/{marcar-completado|reintentar}`
**Contingencia:** `GET /api/dte/contingencia/{documentos|resumen|lotes}` · `POST .../documentos/{id}/reintentar` · `POST .../evento` · `GET/POST .../lotes/{id}/consultar`
**NeoProfit:** `GET /api/profit/{dashboard|productos|clientes|sucursales|tendencia|gastos|compras}` · `POST /api/profit/{gastos|compras}`
**NeoScanAI:** `GET/POST /api/scanai/documentos` · `POST .../{id}/{resultado|confirmar|rechazar|registrar-gasto|registrar-compra|registrar-dte-recibido}`
**NeoConnect:** `GET/POST /api/connect/api-keys` · `PATCH .../revoke` · `GET/POST /api/connect/webhooks` · `POST .../test` · `GET /api/connect/{logs|usage|docs}`
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
| 1 | **Core / Administración** | ✅ avanzado | Mejor UI admin, consumo por plan, upselling, MFA SuperAdmin, IP allowlist | Crítico |
| 2 | **NeoDTE** | ✅ avanzado | Catálogos MH completos, mejorar diagnóstico de errores | Crítico |
| 3 | **Certificación DTE** | ⏳ pruebas reales hechas, falta módulo UI | Matriz visual, progreso, reintentos, solicitar autorización | Muy alta |
| 4 | **Eventos DTE** | 🟡 4 implementados (2 PROCESADO, 2 estructura-OK) | Persistir eventos (tablas), UI, PDF de evento | Alta |
| 5 | **Contingencia/Worker** | ✅ parcial | UI cola, reintento manual, MOMENTO 3 (lote), consulta de lotes, alertas | Alta |
| 6 | **Clientes** | ✅ | Cascada Depto→Muni→Distrito, normalizar numDocumento, mapeo CAT-022 (hecho) | Alta |
| 7 | **Productos** | ✅ | Mapear unidad→CAT-014, tributos por tipo, carga masiva | Media |
| 8 | **Catálogos MH** | ⚠️ parcial | **Módulo de mantenimiento** (§6.2), sembrar completos, versionar | Crítico |
| 9 | **Dashboard** | ✅ base | Integrar NeoProfit, certificación, alertas Hacienda | Alta |
| 10 | **NeoProfit / NeoBI** | ❌ | Análisis financiero, gastos/compras, márgenes | Alta |
| 11 | **NeoScanAI** | ❌ (proyecto aparte) | Bandeja, OCR/IA, registro compra/gasto/DTE recibido | Alta |
| 12 | **NeoConnect API** | ⚠️ API existe | API keys, webhooks, rate limits, sandbox, docs | Media-alta |
| 13 | **NeoPOS** | ❌ | Caja, venta rápida, conversión a DTE | Media-alta |
| 14 | **NeoPortal Clientes** | ❌ | Consulta pública, estado de cuenta | Media |
| 15 | **NeoSTP Mobile** | ❌ | App MVP (login, DTE básico, escaneo) | Media |
| 16 | **Mobile Management** | ❌ | Gestión de dispositivos | Media-baja |
| 17 | **SuperAdmin** | ✅ parcial | Billing, salud sistema, incidentes, churn, soporte | Alta |
| 18 | **Billing SaaS** | ❌ | Trial, Stripe/MercadoPago, webhooks, licencias auto | Crítico (venta) |
| 19 | **Legal / Compliance** | ❌ | Términos, privacidad, consentimiento | Crítico (venta) |
| 20 | **Hardening** | ❌ | Backups, k6, OWASP ZAP, quotas, MFA, DR | Alto (pre-prod) |

---

# 10. Plan de trabajo para completar la Suite

## Fase 1 — Certificación y cumplimiento DTE (CRÍTICO)
1. **Módulo de mantenimiento de Catálogos** (§6.2) — CRUD/Import/Export/versionado.
2. **Sembrar catálogos MH completos** (CAT-013/008 territorial real, CAT-014/015/019/020/024…).
3. **Eliminar hardcodeos** (municipio/distrito en builders) → derivar de catálogo.
4. **Persistir eventos DTE** (tablas `Dte_Eventos*`) + UI + PDF de evento.
5. **Módulo de Certificación DTE** — matriz de progreso (625 pruebas), generar prueba, reintentar, errores.
6. **Completar matriz** (con datos de cuenta: NRC, codEstableMH real, autorizaciones).
7. **Diagnóstico de errores Hacienda** — pantalla que mapea códigos MH a explicaciones y acciones.

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

## Reglas de negocio transversales (NeoProfit)
- Solo contar DTE **PROCESADO**; excluir RECHAZADO e INVALIDADO.
- Nota de Crédito **resta**, Nota de Débito **suma**.
- Sujeto Excluido **no genera IVA**.
- Producto sin costo → marcar **"Costo pendiente"**.

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

## Estrategia (decisiones acordadas)
1. **Secuencia:** primero certificación DTE backend (bloqueante), luego el design system.
2. **CSS:** Tailwind para lo nuevo + AppShell, en **coexistencia gradual** con Bootstrap (retiro
   página por página). Build con Tailwind CLI → `wwwroot/css/app.css` (no CDN en prod).
3. **Componentes** como ViewComponents/TagHelpers de Razor.

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
- 106 tests verde. Faltan tests del **generador v1/v3 por tipo** (snapshot del JSON esperado) y de
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
