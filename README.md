# NeoSTP Cloud Web

Plataforma SaaS multiempresa para emisión de Documentos Tributarios Electrónicos (DTE) en El Salvador y suite de módulos de negocio asociados.

> **Versión actual: Sprint 14 — Módulo de Certificación DTE + 36 catálogos MH oficiales** ✅  
> **Rama:** `main` · **Build:** ✅ 0 errores · **Tests:** 155/155 pasando  
> El provisioning de la empresa de pruebas es automático e idempotente (`EmpresaPruebaSeeder`): crea empresa + plan + módulos + sucursal + punto de venta + usuario admin + configuración DTE base con un solo toggle. Los runbooks en `docs/` guían el paso de mocks a integraciones reales (Hacienda apitest, firma Pkcs12) y la matriz de pruebas.
>
> 🎉 **Hito:** **5 tipos de DTE + 2 eventos PROCESADOS** por Hacienda en el flujo real **Validar → Firmar (RS512) → Enviar** contra `https://apitest.dtes.mh.gob.sv`:
> DTE **01 Factura** · **11 Exportación** · **04 Nota de Remisión** · **14 Sujeto Excluido** · **15 Donación**; eventos **Contingencia** · **Invalidación**. Ver [§ Integración real con Hacienda](#integración-real-con-hacienda-lecciones-del-sprint-11b).
>
> 🎨 El sistema de diseño y mockups de la suite viven versionados en [`/design`](design/README.md) (incorporación UI gradual, post-certificación).

## Stack

- **.NET 10** (LTS, soporte hasta nov-2028)
- **ASP.NET Core MVC + Razor** (Web)
- **ASP.NET Core Web API** + **OpenAPI nativo** (Api)
- **SQL Server 2022** + **Entity Framework Core 10**
- **Serilog** (logs estructurados a consola y archivo)
- **.NET Worker Service** (procesos en segundo plano)
- **xUnit + FluentAssertions** (155 pruebas, todas pasando)
- **ClosedXML 0.104** (import/export Excel de catálogos)
- **Polly v8 / Microsoft.Extensions.Http.Resilience 10.6** para resiliencia HTTP
- **JWT** (Api) + **Cookies** (Web) para autenticación
- **DataProtection** para cifrado de secretos DTE
- **QuestPDF 2025.1** + **MailKit 4.17** para representación gráfica y correo
- **BCrypt.Net-Next** (work factor 11) para hash de passwords

## Arquitectura

Modular monolith con separación por capas:

```
NeoSTP.slnx
├── src/
│   ├── NeoSTP.Web              # MVC/Razor (UI)
│   ├── NeoSTP.Api              # Web API (REST + OpenAPI)
│   ├── NeoSTP.Application      # Casos de uso, servicios, DTOs, abstracciones
│   ├── NeoSTP.Domain           # Entidades, reglas, enums
│   ├── NeoSTP.Infrastructure   # EF Core, SQL Server, integraciones, PDF, correo
│   ├── NeoSTP.Worker           # Background jobs
│   └── NeoSTP.Shared           # Utilidades, ApiResponse, constantes
└── tests/
    ├── NeoSTP.Tests.Unit
    └── NeoSTP.Tests.Integration
```

### Referencias

| Proyecto         | Referencia a                          |
| ---------------- | ------------------------------------- |
| Web              | Application, Infrastructure, Shared   |
| Api              | Application, Infrastructure, Shared   |
| Application      | Domain, Shared                        |
| Infrastructure   | Application, Domain, Shared           |
| Worker           | Application, Infrastructure, Shared   |
| Tests            | Application, Domain, Infrastructure   |

## Requisitos

- .NET SDK 10.0.x
- SQL Server 2022 (local o remoto)
- `dotnet ef` global tool (`dotnet tool install --global dotnet-ef`)

## Configuración

### Connection string

Cadena de conexión en `appsettings.Local.json` de los proyectos `Api`, `Web` y `Worker`
(el archivo está en `.gitignore`):

```json
{
  "ConnectionStrings": {
    "NeoStpDb": "Server=.;Database=NeoSTP_Cloud;User Id=sa;Password=jda;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "Jwt": { "Key": "dev-only-replace-me-with-a-strong-32-plus-char-secret-key" }
}
```

> Las credenciales en `appsettings.Local.json` son solo para desarrollo. En producción
> usar User Secrets, variables de entorno o Azure Key Vault.

### Configuración del Worker

El proceso `NeoSTP.Worker` se configura en la sección `Worker` de `appsettings.json`:

```json
{
  "Worker": {
    "RetransmisionContingencia": {
      "IntervaloMinutos": 5,
      "CooldownMinutos": 30,
      "MaxIntentos": 5,
      "LoteMaximo": 50
    },
    "LimpiezaTokens": {
      "IntervaloHoras": 24,
      "RetentionDias": 30
    }
  }
}
```

| Parámetro                                | Descripción                                                                      |
| ---------------------------------------- | -------------------------------------------------------------------------------- |
| `RetransmisionContingencia.IntervaloMinutos` | Cada cuántos minutos el Worker busca DTE en CONTINGENCIA (default 5)         |
| `RetransmisionContingencia.CooldownMinutos`  | Tiempo de espera mínimo entre intentos para el mismo documento (default 30)  |
| `RetransmisionContingencia.MaxIntentos`      | Intentos máximos antes de abandonar un documento (default 5)                 |
| `RetransmisionContingencia.LoteMaximo`       | Máximo de documentos procesados por ciclo (default 50)                       |
| `LimpiezaTokens.IntervaloHoras`              | Cada cuántas horas se ejecuta la limpieza de tokens (default 24)             |
| `LimpiezaTokens.RetentionDias`               | Días de retención de tokens expirados/revocados antes de eliminarlos (default 30) |

### Toggles de integraciones externas

Todas las integraciones externas tienen un toggle Mock/Real para poder desarrollar
sin credenciales productivas. En `appsettings.Local.json`:

```json
{
  "Hacienda": {
    "Client": "Http",                              // Mock | Http
    "PruebasBaseUrl": "https://apitest.dtes.mh.gob.sv",
    "ProduccionBaseUrl": "https://api.dtes.mh.gob.sv",
    "TimeoutSeconds": 30
  },
  "Dte": {
    "Signer": "Mock"                               // Mock | Pkcs12
  },
  "Email": {
    "Provider": "Mock",                            // Mock | Smtp
    "MockOutbox": "logs/email-outbox",
    "From": { "Address": "noreply@neostp.local", "DisplayName": "NeoSTP Cloud" },
    "Smtp": {
      "Host": "smtp.example.com",
      "Port": 587,
      "UseStartTls": true,
      "Username": "user",
      "Password": "pass"
    }
  }
}
```

| Toggle           | Default | Implementación real                     |
| ---------------- | ------- | --------------------------------------- |
| `Hacienda:Client`| `Mock`  | `HttpHaciendaAuthClient` + `HttpHaciendaReceptionClient` (POST `/seguridad/auth` y `/fesv/recepciondte`) con Polly: 3 reintentos, circuit breaker, timeouts configurados |
| `Dte:Signer`     | `Mock`  | `Pkcs12DteSignerService` (RS256 con PFX en `DteConfiguracion`)                     |
| `Email:Provider` | `Mock`  | `SmtpEmailSender` (MailKit, STARTTLS/SSL)                                          |

### Resiliencia Polly (Sprint 9)

Los dos clientes HTTP de Hacienda (`auth` y `recepcion`) tienen una pipeline de resiliencia estándar de Polly v8:

| Cliente           | Reintentos | Delay base | Timeout total | Timeout por intento |
| ----------------- | ---------- | ---------- | ------------- | ------------------- |
| Auth (token MH)   | 3          | 1 s        | 90 s          | 25 s                |
| Recepción DTE     | 3          | 2 s        | 120 s         | 35 s                |

La pipeline incluye automáticamente: **retry** (exponential backoff + jitter), **circuit breaker** (abre después de fallos consecutivos), **hedging** para peticiones lentas y **timeout** de total y por intento. Configurado con `AddStandardResilienceHandler` de `Microsoft.Extensions.Http.Resilience`.

El `MockEmailSender` deja los correos como `.eml` en `logs/email-outbox/` para inspección
sin SMTP real.

## Cómo correr

```powershell
# Compilar todo
dotnet build NeoSTP.slnx

# Levantar la Web
dotnet run --project src/NeoSTP.Web

# Levantar la Api (OpenAPI en /openapi/v1.json en Development)
dotnet run --project src/NeoSTP.Api

# Levantar el Worker
dotnet run --project src/NeoSTP.Worker

# Correr pruebas (101 unit tests)
dotnet test NeoSTP.slnx
```

También hay una skill local `.claude/skills/neostp/` con todos los comandos cotidianos.
Invócala como `/neostp <subcomando>` dentro de Claude Code.

## Quickstart — flujo E2E con mocks

Con la configuración por defecto (todos los toggles en `Mock`) puedes ejercitar
el ciclo completo de emisión DTE sin certificado, sin credenciales MH y sin
servidor SMTP. En PowerShell, desde la raíz del repo:

```powershell
# 1. Crear la BD y aplicar las 15 migraciones
dotnet ef database update --project src/NeoSTP.Infrastructure --startup-project src/NeoSTP.Api

# 2. Levantar la Web (en otra ventana)
dotnet run --project src/NeoSTP.Web

# 3. En el navegador: https://localhost:7044
#    Login: superadmin / ChangeMe!2026
#    SuperAdmin → Soporte → seleccionar empresa demo
#    → Empresas → crear "Demo S.A. de C.V." con plan PRO (si no existe)
#    → Clientes → crear un cliente con correo
#    → Productos → crear al menos un producto
#    → DTE → "Nuevo DTE" → Factura (01) → agregar línea → Guardar
#    → En Details: Validar → Firmar → Enviar a Hacienda
#    → Descargar PDF · Descargar JSON · Reenviar por correo
#    → El correo se guarda como .eml en logs/email-outbox/
```

Cada paso de la cadena `Validar → Firmar → Enviar` cambia el estado del
documento y deja registro en `Core_Auditoria`. El sello recibido del mock
se muestra en la sección "Respuesta de Hacienda" del detalle.

Para activar las integraciones reales basta con cambiar los toggles en
`appsettings.Local.json` (ver sección anterior).

## Base de datos

Migraciones aplicadas en orden:

1. `InitialCreate`
2. `Sprint1_CoreCatalogosYSeguridad` — catálogos, usuarios, roles, permisos, refresh tokens
3. `Sprint3_ClientesYProductos` — clientes, productos, departamentos ES
4. `Sprint35_MunicipiosES` — 42 municipios post-reforma 2024
5. `Sprint4_DteConfiguracion` — configuración DTE por empresa con cifrado
6. `Sprint5_DteDocumentos` — documentos DTE, detalles y JSON
7. `Sprint9_RetransmisionTracking` — columnas `IntentoRetransmision` y `UltimoIntentoRetransmisionAt` en `Dte_Documentos`
8. `Sprint10_DteCorrelativos` — tabla `Dte_Correlativos` para contador atómico de `NumeroControl`
9. `Sprint12_DistritoCAT008` — columnas `Distrito*Codigo` para la división territorial 2024
10. `Sprint13_CatalogosExtendido` — `Catalogo.Version/MetadataJson`, `CatalogoItem.ParentCodigo` para cascadas
11. `Sprint13_PermisosCatalogos` — permisos `Core.Catalogos.Ver/.Administrar/.Importar`
12. `Sprint13_SeedCatalogosMH` — seed inicial de catálogos MH prioritarios (CAT-005/015/019/020/024 + CAT-008 placeholder)
13. `Sprint13_CatalogosMhOficial` — paquete oficial Manual v1.4: 11 catálogos nuevos (CAT-006/018/021/023/025/026/027/029/030/031/032) + reemplazo de UNIDAD_MEDIDA/PAIS/TIPO_DOC_IDENTIDAD/MOTIVO_INVALIDACION con `Codigo=codigoMH` (275 países, 56 unidades, 45 recintos fiscales…)
14. `Sprint14_CertificacionDte` — tablas `Dte_CertificacionMatriz/Escenarios/Pruebas/Errores` + matriz oficial seedeada (15 tipos × 625 escenarios numerados)
15. `Sprint14_PermisosCertificacion` — permisos `Core.Certificacion.Ver/.Operar`

```powershell
# Crear una nueva migración
dotnet ef migrations add NombreMigracion `
  --project src/NeoSTP.Infrastructure `
  --startup-project src/NeoSTP.Api `
  --output-dir Persistence/Migrations

# Aplicar migraciones a la BD
dotnet ef database update `
  --project src/NeoSTP.Infrastructure `
  --startup-project src/NeoSTP.Api
```

## Endpoints disponibles

Todos los endpoints viven bajo `/api`. Los autenticados requieren `Authorization: Bearer <jwt>`.

### Auth (anónimo + autenticado)

```
POST  /api/auth/login              { usernameOrEmail, password }
POST  /api/auth/refresh            { refreshToken }
POST  /api/auth/logout             { refreshToken? }
GET   /api/auth/me
POST  /api/auth/change-password    { currentPassword, newPassword }
```

### Usuarios (requiere permisos `Core.Usuarios.*`)

```
GET    /api/usuarios?page=&pageSize=&search=
GET    /api/usuarios/{id}
POST   /api/usuarios
PUT    /api/usuarios/{id}
PATCH  /api/usuarios/{id}/bloquear
PATCH  /api/usuarios/{id}/desbloquear
POST   /api/usuarios/{id}/reset-password
```

### Roles (requiere `Core.Roles.Administrar`)

```
GET   /api/roles
GET   /api/roles/{id}
POST  /api/roles
PUT   /api/roles/{id}
GET   /api/roles/permisos
```

### Catálogos (Sprint 13)

Módulo de mantenimiento completo: CRUD admin, import/export CSV/JSON/XLSX, versionado de catálogos, cascadas padre/hijo (`ParentCodigo`), `metadata.codigoMH` por ítem. 36 catálogos del sistema instalados (paquete oficial Manual de Estructuras CAT v1.4).

Permisos:
- `Core.Catalogos.Ver` — lectura
- `Core.Catalogos.Administrar` — CRUD
- `Core.Catalogos.Importar` — bulk import/export

```
GET    /api/catalogos
GET    /api/catalogos/{codigo}
GET    /api/catalogos/{codigo}/items?parent=                  # ?parent=SS para cascada Departamento→Municipio
GET    /api/catalogos/{codigo}/items?parent=__ROOT__          # solo nivel raíz
POST   /api/catalogos
PUT    /api/catalogos/{codigo}
POST   /api/catalogos/{codigo}/items
PUT    /api/catalogos/{codigo}/items/{id}
DELETE /api/catalogos/{codigo}/items/{id}
POST   /api/catalogos/{codigo}/import?format=csv|json|xlsx&dryRun=true&mode=Upsert|InsertOnly
GET    /api/catalogos/{codigo}/export?format=csv|json|xlsx
```

**Reglas**: catálogos `EsSistema` no se inactivan; ítems `EsSistema` no se borran físicamente (solo se inactivan); padre debe existir o estar en el mismo lote de import; un ítem con hijos no se puede eliminar.

**Catálogos oficiales MH** (con `Codigo = codigoMH`): CAT-001 Ambiente, CAT-002 Tipo Documento, CAT-005 Tipo Contingencia, CAT-006 Retención IVA, CAT-009 Tipo Establecimiento, CAT-012 Departamento (14), CAT-013 Municipio (44), CAT-014 Unidad Medida (56), CAT-016 Condición Operación, CAT-017 Forma Pago, CAT-018 Plazo, CAT-019 Actividad Económica, CAT-020 País (275), CAT-021 Otros Doc Asociados, CAT-022 Tipo Doc Identidad, CAT-023 Tipo Doc Contingencia, CAT-024 Motivo Invalidación, CAT-025 Título Remisión, CAT-026 Tipo Donación, CAT-027 Recinto Fiscal (45), CAT-029 Tipo Persona, CAT-030 Transporte, CAT-031 INCOTERMS (16), CAT-032 Domicilio Fiscal. Solo CAT-008 Distrito queda como placeholder vacío para import oficial.

### Empresas y licenciamiento (Sprint 2)

SuperAdmin ve todas las empresas; un usuario de empresa solo la suya.

```
GET   /api/empresas?page=&pageSize=&search=
GET   /api/empresas/{id}
POST  /api/empresas                            # solo SuperAdmin
PUT   /api/empresas/{id}
GET   /api/empresas/{id}/licencia              # plan vigente + módulos + consumo
POST  /api/empresas/{id}/plan                  { planId, fechaInicio?, fechaFin? }
POST  /api/empresas/{id}/modulos/{moduloId}/activar
POST  /api/empresas/{id}/modulos/{moduloId}/desactivar
```

### Sucursales y Puntos de Venta (Sprint 2)

```
GET    /api/sucursales
GET    /api/sucursales/{id}
POST   /api/sucursales
PUT    /api/sucursales/{id}
PATCH  /api/sucursales/{id}/inactivar
GET    /api/puntos-venta
GET    /api/puntos-venta/{id}
POST   /api/puntos-venta
PUT    /api/puntos-venta/{id}
PATCH  /api/puntos-venta/{id}/inactivar
```

Crear sucursales/PV consume los **límites del plan**: si el conteo actual
ya está en el límite, retorna `409 LIMIT_EXCEEDED`.

### Planes y módulos (lectura, Sprint 2)

```
GET  /api/planes                  # catálogo de planes con sus módulos
GET  /api/planes/{id}
GET  /api/modulos                 # catálogo de módulos del sistema
```

Para proteger endpoints según módulo contratado por la empresa, decora
con `[RequireModule("NEODTE")]` — la policy resuelve dinámicamente y
consulta `Core_EmpresaModulos`. SuperAdmin siempre pasa.

### Clientes y productos (Sprint 3)

```
GET    /api/clientes?page=&pageSize=&search=
GET    /api/clientes/{id}
POST   /api/clientes                          # valida formato DUI/NIT, NRC para contribuyente
PUT    /api/clientes/{id}
PATCH  /api/clientes/{id}/inactivar

GET    /api/productos?page=&pageSize=&search=
GET    /api/productos/{id}
POST   /api/productos                          # BIEN o SERVICIO con IVA configurable
PUT    /api/productos/{id}
PATCH  /api/productos/{id}/inactivar
```

**Validaciones fiscales** (en `NeoSTP.Application.Clientes.ClienteValidator`):
- DUI: `########-#` (12345678-9)
- NIT: `####-######-###-#` o 14 dígitos sin separadores
- NRC: 1-7 dígitos, opcionalmente con guion (1234567 o 123456-7)
- Contribuyentes requieren NRC + código de actividad económica
- Correo se valida si está presente

Catálogo `DEPARTAMENTO_ES` con los 14 departamentos de El Salvador.
Catálogo `MUNICIPIO_ES` con 42 municipios/zonas post-reforma territorial 2024
(Decreto 290). Cada item lleva metadata
`{"departamento":"CODIGO","zona":"NORTE|SUR|ESTE|OESTE|CENTRO|COSTA"}`
para permitir cascada UI.

### Configuración DTE / Hacienda (Sprint 4)

Configuración fiscal por empresa (1-a-1 con `Core_Empresas` en tabla `Dte_Configuracion`).
Permisos requeridos: `DTE.Configurar`.

```
GET     /api/dte/configuracion              # password/cert nunca se devuelven
PUT     /api/dte/configuracion               { ambienteCodigo, usuarioMh, passwordMh?, ... }
POST    /api/dte/configuracion/certificado   { nombre, contenidoBase64, password?, emitido?, vence? }
DELETE  /api/dte/configuracion/certificado
POST    /api/dte/configuracion/probar-conexion
```

**Cifrado de secretos** — `ISecretProtector` (impl. `DataProtectionSecretProtector`)
cifra password de Hacienda, password del certificado y token cacheado de MH con
**ASP.NET Core DataProtection** (purpose `NeoSTP.DteSecrets.v1`). Las llaves se
guardan en `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` (Windows) o
`/var/aspnet/DataProtection-Keys` (Linux). Cambiar la llave invalida los passwords
cifrados — habrá que reingresarlos.

### Documentos DTE — generación (Sprint 5)

Endpoints para emisión de documentos electrónicos. Estados:
`BORRADOR → GENERADO → VALIDADO → FIRMADO → ENVIADO → PROCESADO / RECHAZADO / CONTINGENCIA / INVALIDADO / ERROR`

```
GET    /api/dte/documentos?page=&search=&tipoDteCodigo=&estadoCodigo=&desde=&hasta=
GET    /api/dte/documentos/{id}

POST   /api/dte/factura                      # 01 Factura Consumidor Final
POST   /api/dte/credito-fiscal               # 03 CCF
POST   /api/dte/nota-credito                 # 05 Nota de Crédito
POST   /api/dte/nota-debito                  # 06 Nota de Débito
POST   /api/dte/sujeto-excluido              # 14 Factura Sujeto Excluido
POST   /api/dte/documentos                   # genérico (TipoDteCodigo en body)

POST   /api/dte/documentos/{id}/generar      # construye JSON
POST   /api/dte/documentos/{id}/validar      # valida campos obligatorios
POST   /api/dte/documentos/{id}/invalidar    { motivo }
```

Permisos: `DTE.Emitir` para crear/generar/validar, `DTE.Invalidar` para invalidar.

**Reglas de cálculo** (en `DteCalculator`):
- Factura 01 → IVA **incluido** en gravada (`IVA = bruto × 0.13/1.13`, informativo).
- CCF / NC / ND → IVA **separado** (`IVA = gravada × 0.13`, sumado al total).
- Sujeto Excluido 14 → **sin IVA**, va como No Sujeta.
- Total en letras estilo MH (`DOSCIENTOS CINCUENTA 35/100 DÓLARES`).
- Número de control: `DTE-{tipo}-{estab(4)}{punto(4)}-{15 dígitos}`.
- `CodigoGeneracion`: UUID v4 mayúsculas.

El JSON sigue el esquema oficial MH v1 (factura/sujeto excluido) y v3 (CCF/NC/ND).

### Documentos DTE — firma y transmisión (Sprint 6)

```
POST   /api/dte/documentos/{id}/firmar       # produce JWS (header.payload.signature)
POST   /api/dte/documentos/{id}/enviar       # POST a Hacienda con Bearer token
```

- **Firma JWS RS256** desde el PFX guardado en `DteConfiguracion`.
  Header incluye `x5t` (huella SHA-1 del certificado). Toggle `Dte:Signer = Mock | Pkcs12`.
- **Transmisión** a `POST {base}/fesv/recepciondte` con token de Hacienda.
  El token se refresca automáticamente con `IHaciendaAuthClient` cuando expira
  (margen de 5 minutos).
- Respuesta MH se mapea al estado interno:
  - `PROCESADO` → guarda `SelloRecibido` y `ProcesadoAt`.
  - `RECHAZADO` → estado `RECHAZADO` (permite re-emisión).
  - `CONTINGENCIA` → estado `CONTINGENCIA` (permite reintento).
- Errores externos: `FIRMA_FAILED` y `HACIENDA_AUTH_FAILED` → HTTP **502**.

### Integración real con Hacienda (lecciones del Sprint 11b)

Al ejecutar el flujo completo contra `https://apitest.dtes.mh.gob.sv` con un NIT y
certificado de prueba reales, se corrigieron cuatro discrepancias entre nuestra
implementación y el comportamiento real de MH hasta lograr `estado=PROCESADO`:

| Problema (respuesta MH) | Causa | Solución |
|---|---|---|
| HTTP 401 en recepción | `body.token` de `/seguridad/auth` ya incluye el prefijo `"Bearer "`; lo duplicábamos | `HttpHaciendaAuthClient` recorta el prefijo antes de cachear/usar el token |
| `802 Firma no válida` | Firmábamos con RS256/SHA-256 | Hacienda exige **RS512** (RSA + SHA-512) con header mínimo `{"alg":"RS512"}`, idéntico al `svfe-api-firmador` oficial |
| `identificacion.numeroControl no cumple el formato requerido` | Asumíamos `[A-Z0-9]{8}` para el bloque de establecimiento | El formato oficial es `(M\|B\|S\|P)([0-9]{3})(P)([0-9]{3})` → `M001P001`. Construido por `BuildBloqueEstablecimiento()` con la letra de tipo de establecimiento (CAT-009) |
| `emisor/codEstableMH no cumple el tamaño mínimo` | Enviábamos `codEstableMH`/`codPuntoVentaMH` con 3 dígitos | Los códigos asignados por MH requieren **exactamente 4 caracteres** (ej. `0001`) |

> **Credenciales (dos passwords distintos):** el del **portal** (login web) ≠ el de la
> **API de recepción** (`passwordMh`). Usar el de la API en la configuración DTE.
> Los secretos de prueba viven en `appsettings.Local.json` (gitignored), nunca en el repo.

Evidencia del primer DTE aceptado: `selloRecibido=202610EB9EA7841B405899A4D149D56AFF3CBWDE`,
`numeroControl=DTE-01-M001P001-000000000000014`, `codigoMsg=001` (RECIBIDO), `observaciones=[]`.

#### Matriz de certificación — Sprint 12 (5 tipos PROCESADOS)

Hallazgo decisivo: **el ambiente apitest valida contra los esquemas v1/v3, NO v2/v4.**
Los archivos `svfe-json-schemas` (v2/v4) son más nuevos que lo desplegado en apitest.
Se envió una Factura v2 (con `distrito`, `ivaRete`, sin `extension`) y MH la rechazó
exigiendo lo contrario; al volver a v1 → PROCESADO. **La certificación se hace contra
v1/v3**, que es lo que el generador emite. La infraestructura de `distrito` (CAT-008)
queda lista pero dormante para Factura v1 — aunque **sí se usa** en los tipos v2/v3.

| Tipo DTE | Versión apitest | Estado | Aprendizaje clave |
|---|---|---|---|
| 01 Factura | v1 | ✅ PROCESADO | `extension`, `ivaRete1`+`reteRenta`, `codEstableMH`+`tipoEstablecimiento`, división territorial **vieja** (municipio `03`) |
| 14 Sujeto Excluido | v1 | ✅ PROCESADO | emisor **sin** `tipoEstablecimiento`/`nombreComercial` |
| 04 Nota de Remisión | v3 | ✅ PROCESADO | `extension` sin `placaVehiculo`; con línea **NO_SUJETA** se acepta receptor sin NRC (cod 002) |
| 15 Donación | v2 | ✅ PROCESADO | emisor=Donatario (`tipoDocumento`), `pagos` requerido, división territorial **2024** (municipio `23` + distrito `03`) |
| 11 Factura Exportación | v3 | ✅ PROCESADO | receptor extranjero `codPais` CAT-020 (`9539`=EUA, `9300`=El Salvador), tributo **`C3`** (IVA export 0%), división 2024 |

**Patrón territorial por versión:** los DTE v1 usan la división vieja (Ayutuxtepeque = municipio `03`);
los v2/v3 usan la división 2024 (San Salvador Centro `23` + distrito Ayutuxtepeque `03`).

**Salvaguardas añadidas:** mapeo interno→CAT-022 en `receptor.tipoDocumento` (DUI→13, NIT→36…) y
un **guardrail anti-mock** que bloquea enviar una firma `none/mock` al Hacienda real
(`FIRMA_MOCK_NO_ENVIABLE`) — la Web firma con sus propios servicios, así que su
`appsettings.Local.json` también debe fijar `Dte:Signer=HaciendaCert`.

> ⏳ **Pendiente de matriz (DTE):** 03 CCF / 05 NC / 06 ND y 07/08/09 requieren receptor
> **inscrito en IVA** (NIT + NRC reales). Migración a v2/v4 solo cuando apitest la adopte.

#### Eventos DTE — Sprint 12

Subsistema de eventos completo (generación + firma RS512 + transmisión). El Manual Técnico v2.0
confirma que **solo Contingencia e Invalidación tienen endpoint propio**; Retorno y Operaciones
Especiales (esquemas `fe-eret`/`fe-eop`, prefijo `fe-` como los DTE) se transmiten por
`/fesv/recepciondte`.

| Evento | Endpoint | Estado | Nota |
|---|---|---|---|
| **Contingencia** | `/fesv/contingencia` | ✅ PROCESADO | emisor `codEstableMH`+`codPuntoVenta` (asimétrico); DTE en contingencia con `tipoTransmision=2` |
| **Invalidación** | `/fesv/anulardte` | ✅ PROCESADO | `fecAnula`/`horAnula` (no fecEmi); `nomEstablecimiento` requerido; tipo 2 = rescindir |
| **Operaciones Especiales** | `/fesv/recepciondte` | 🟡 estructura OK | `tipoEvento=17`, `tipoDocumento=97` (Control Interno); bloqueado por `095` (cuenta no autorizada) |
| **Retorno** | `/fesv/recepciondte` | 🟡 estructura OK | `tipoEvento=18`, referencia FE/FEXE/FSEE; bloqueado por `codEstableMH` (requiere código MH registrado real) |

Endpoints API: `POST /api/dte/evento/{contingencia|invalidacion|operaciones-especiales|retorno}`.
Clientes: `IHaciendaContingenciaClient` (dedicado) + `IHaciendaEventoClient` (genérico, endpoint parametrizable).

> ⏳ **Bloqueos de cuenta (no de código):** Op-Especiales necesita autorización del contribuyente
> para Factura Simplificada/Control Interno; Retorno necesita el `codEstableMH` real registrado.
> Ambas estructuras ya pasan la validación de esquema de Hacienda.

### Certificación DTE (Sprint 14)

Módulo para controlar la matriz oficial Hacienda de 625 escenarios (15 tipos: 90 Factura, 75 CCF, 50 NR, 50 NC, 25 ND, 50 Retención, 75 Liquidación, 50 DCL, 90 Exportación, 25 SE, 25 Donación, 5 cada uno de Invalidación/Contingencia/Retorno/OpEspeciales). Permite visualizar progreso por tipo, asociar DTE emitidos a escenarios, reintentar, y saber cuándo solicitar autorización.

Permisos:
- `Core.Certificacion.Ver` — consulta resumen / matriz / escenarios / errores
- `Core.Certificacion.Operar` — generar prueba / marcar completado / reintentar

```
GET   /api/certificacion/resumen                                # totales + % progreso + lista para autorización
GET   /api/certificacion/matriz                                 # 15 tipos con conteos
GET   /api/certificacion/tipos/{codigo}/escenarios              # estado actual de cada escenario para la empresa
GET   /api/certificacion/errores?codigoMh=                      # últimos 500 errores Hacienda filtrables

POST  /api/certificacion/tipos/{codigo}/generar-prueba          # abre prueba EN_PROGRESO para el siguiente escenario PENDIENTE
POST  /api/certificacion/documentos/{id}/marcar-completado      { escenarioId, notas? }
POST  /api/certificacion/documentos/{id}/reintentar             # marca prueba ERROR y abre nuevo intento PENDIENTE
```

**Reglas**:
- `MarcarCompletado` promueve a `COMPLETADO` solo si el DTE tiene `SelloRecibido` y estado `PROCESADO`.
- Valida cruzado tipo DTE ↔ matriz (no permite asociar un CCF a la matriz de Factura).
- El cálculo de progreso considera solo el **último intento** por escenario, no la suma.
- `Reintentar` abre intento `N+1` sin DTE asociado; el usuario emite uno nuevo y lo asocia con `marcar-completado`.

### Documentos DTE — descarga y correo (Sprint 7)

```
GET    /api/dte/documentos/{id}/pdf          # representación gráfica QuestPDF
GET    /api/dte/documentos/{id}/json         # JSON DTE sin firmar
POST   /api/dte/documentos/{id}/reenviar     { destinatario? }    # PDF+JSON adjuntos
```

Permisos: `DTE.Consultar` para descargas, `DTE.Reenviar` para correo.
Si no se pasa `destinatario`, se usa el correo del receptor. `EMAIL_FAILED` → HTTP **502**.

### Dashboard (Sprint 8)

```
GET  /api/dashboard/empresa?empresaId=      # KPIs del mes para la empresa del token
                                             # SuperAdmin: pasar ?empresaId=N
GET  /api/dashboard/superadmin              # métricas globales (solo SUPERADMIN)
```

El endpoint de empresa devuelve:
- `dteHoy`, `dteMes`, `totalPagarMes` (procesados)
- `procesados`, `rechazados`, `contingencias`, `pendientes` (todos los estados no terminales)
- `planNombre`, `limiteDteMensual`, `porcentajeUsoDte`
- `porEstado` — array `{estado, cantidad, totalPagar}` del mes
- `porTipo`   — array `{tipoCodigo, tipoNombre, cantidad, totalPagar}` del mes
- `tendenciaDiaria` — array de 30 días `{fecha, cantidad, totalPagar}`

El endpoint superadmin devuelve:
- `empresasActivas`, `empresasTotal`, `usuariosActivos`
- `dteTotalMes`, `facturacionTotalMes` (procesados)
- `resumenPorPlan` — array `{planNombre, empresasCount, ingresosMensuales}`
- `alertasPlanProximoVencer` — planes que vencen en los próximos 30 días
- `topEmpresasDteMes` — top 10 empresas ordenadas por cantidad de DTE

### Worker Jobs (Sprint 9)

El proceso `NeoSTP.Worker` ejecuta dos `BackgroundService` de forma independiente:

#### RetransmisionContingenciaWorker

- Se ejecuta cada `IntervaloMinutos` (default 5 min).
- Consulta documentos con `EstadoCodigo = CONTINGENCIA` que:
  - No hayan superado `MaxIntentos` intentos.
  - Cuyo último intento fue hace más de `CooldownMinutos` (o nunca intentados).
- Toma hasta `LoteMaximo` documentos por ciclo (evita timeouts en BD muy cargada).
- Llama a `IDteDocumentosService.EnviarAsync` para cada documento.
- Incrementa `IntentoRetransmision` ANTES de enviar (evita loops infinitos si el proceso muere a mitad).
- Resultado: `{ Procesados, Exitosos, Fallidos, Omitidos }` logueado con Serilog.

#### LimpiezaTokensWorker

- Se ejecuta cada `IntervaloHoras` (default 24 h).
- Elimina de `Core_RefreshTokens` los tokens cuya `ExpiresAt < (ahora - RetentionDias)` o cuya `RevokedAt < (ahora - RetentionDias)`.
- Previene crecimiento indefinido de la tabla de tokens.

Ambos workers usan `IServiceScopeFactory` para resolver servicios scoped (EF Core `DbContext`) desde el contexto singleton del `BackgroundService`.

### Diagnóstico

```
GET  /health
GET  /openapi/v1.json     # solo en Development
```

## Vistas Web

Bajo el dominio `/` con auth por cookie:

| Ruta                          | Sprint | Notas                                                    |
| ----------------------------- | ------ | -------------------------------------------------------- |
| `/Account/Login`              | 1      | Login con username/password                              |
| `/Home`                       | 1      | Dashboard básico con permisos del usuario                |
| `/Usuarios`                   | 1      | CRUD de usuarios + bloqueo                               |
| `/Empresas`                   | 2      | CRUD de empresas (SuperAdmin) + activar/desactivar módulos|
| `/Soporte`                    | 2      | SuperAdmin: entrar en modo soporte de una empresa        |
| `/Clientes`                   | 3      | CRUD clientes con cascada departamento → municipio       |
| `/Productos`                  | 3      | CRUD productos                                           |
| `/DteConfiguracion`           | 4      | Form ambiente + credenciales MH + carga PFX              |
| `/DteDocumentos`              | 5–7    | Listado, alta, detalle con todas las acciones de estado  |
| `/DteDocumentos/Create`       | 5      | Form con líneas dinámicas y recálculo en cliente         |
| `/DteDocumentos/Details/{id}` | 5–7    | Totales, JSON, JWS firmado, sello MH, descargas y reenvío|
| `/Planes`                     | 2      | Lectura del catálogo de planes y módulos                 |
| `/Home` (empresa)             | 8      | Dashboard con KPIs, tendencia 30d, donut de estados y tabla por tipo |
| `/Home` (SuperAdmin)          | 8      | Panel global: KPIs, alertas de planes, top empresas, resumen MRR |
| `/Sucursales`                 | 10     | CRUD sucursales + botón directo a puntos de venta de cada sucursal |
| `/Sucursales/PuntosVenta`     | 10     | CRUD puntos de venta con filtro por sucursal |
| `/Catalogos`                  | 13     | Lista de catálogos con conteo de ítems, versión y badge Sistema/Empresa |
| `/Catalogos/Details/{codigo}` | 13     | Ítems con filtro por padre y dropdown exportar CSV/JSON/XLSX |
| `/Catalogos/Import/{codigo}`  | 13     | Upload con simulación, modos Upsert/InsertOnly, reporte de errores por fila |
| `/Certificacion`              | 14     | Dashboard: 6 cards de resumen + barras por tipo + indicador "listo para autorización" |
| `/Certificacion/Matriz`       | 14     | Matriz completa con totales y % por tipo |
| `/Certificacion/Tipo/{codigo}`| 14     | Detalle por tipo con badges de estado, botón generar prueba y reintentar |
| `/Certificacion/Errores`      | 14     | Listado de errores MH con respuesta cruda colapsable |

## SuperAdmin inicial

Al primer arranque de la Api, `DatabaseSeeder` aplica migraciones pendientes y, si no hay
ningún usuario, crea un SuperAdmin:

| Username     | Password         |
| ------------ | ---------------- |
| `superadmin` | `ChangeMe!2026`  |

**Cambia la contraseña en el primer login** vía `POST /api/auth/change-password`
o desde el menú de usuario en la Web.

### Modo soporte (SuperAdmin)

El SuperAdmin no pertenece a ninguna empresa. Para operar pantallas
multi-tenant (Clientes, Productos, DTE…) entra en **modo soporte** seleccionando
una empresa en `/Soporte`. La selección se guarda en una cookie y
`IEmpresaContext` la usa para scope los queries.

## Empresa de pruebas (provisioning automático, Sprint 11)

`EmpresaPruebaSeeder` corre al arrancar la **Api** y crea —de forma **idempotente**—
una empresa completa lista para pruebas reales: empresa + plan + módulos + sucursal
Casa Matriz + punto de venta Principal + usuario admin + configuración DTE base.

Se activa con la sección `EmpresaPrueba` en `appsettings.Local.json` de la Api:

```json
{
  "EmpresaPrueba": {
    "Enabled": true,
    "Nit": "06140000000000",
    "RazonSocial": "NeoSTP Pruebas, S.A. de C.V.",
    "PlanCodigo": "ENTERPRISE",
    "Admin": { "Username": "admin.prueba", "Password": "ChangeMe!2026" },
    "Sucursal": { "Codigo": "0001", "Nombre": "Casa Matriz" },
    "PuntoVenta": { "Codigo": "0001", "Nombre": "Principal" },
    "Dte": { "AmbienteCodigo": "PRUEBAS", "UsuarioMh": "06140000000000" }
  }
}
```

> Los **secretos** (password MH, certificado PFX) NO se siembran aquí: se cargan vía
> `/DteConfiguracion` para quedar cifrados con DataProtection. Si la empresa ya existe
> (por NIT) el seeder no hace nada. **Pon `Enabled: false` tras la primera creación.**

Guías paso a paso en `docs/`:
- **`Sprint11-Runbook-Mocks-a-Real.md`** — cambiar de Mock a Hacienda HTTP + firma Pkcs12.
- **`Sprint11-Matriz-Pruebas-DTE.md`** — checklist E2E de los 5 tipos DTE (01/03/05/06/14).

## Notas para pruebas manuales en PowerShell 5.1

`Invoke-RestMethod` en PowerShell 5.1 **no codifica UTF-8** los `Body` con
caracteres acentuados (`é`, `ñ`, etc.) — los envía como Latin-1 y el JSON
parser de ASP.NET retorna 400. Para POST/PUT con texto en español usa raw
`WebRequest` con `Encoding.UTF8`:

```powershell
function PostJson($url, $body, $headers) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
    $req = [System.Net.WebRequest]::Create($url)
    $req.Method = "POST"
    $req.ContentType = "application/json; charset=utf-8"
    $req.Headers.Add("Authorization", $headers.Authorization)
    $req.ContentLength = $bytes.Length
    $s = $req.GetRequestStream(); $s.Write($bytes, 0, $bytes.Length); $s.Close()
    # ... GetResponse, ConvertFrom-Json
}
```

PowerShell 7+ o la Web del proyecto no tienen este problema.

## Skill Claude Code

Hay una skill local en `.claude/skills/neostp/` que envuelve los comandos más usados del día a día. Invócala como `/neostp` dentro de Claude Code.

## Roadmap

| Sprint | Tema                                   | Estado |
| ------ | -------------------------------------- | ------ |
| 0      | Setup técnico                          | ✅     |
| 1      | Seguridad y Core (login, RBAC, JWT)    | ✅     |
| 2      | Empresa y licenciamiento               | ✅     |
| 3      | Catálogos, clientes, productos         | ✅     |
| 3.5    | Municipios El Salvador (post-reforma)  | ✅     |
| 4      | Configuración DTE (cifrado + Hacienda) | ✅     |
| 5      | Generación DTE (5 tipos)               | ✅     |
| 6      | Firma JWS y transmisión a Hacienda     | ✅     |
| 7      | PDF, correo y descarga del DTE         | ✅     |
| 8      | Dashboard operativo y SuperAdmin avzdo | ✅     |
| 9      | Worker jobs y resiliencia              | ✅     |
| 10     | Backlog: Sucursales UI, QR PDF, AtomicCounter | ✅     |
| 11     | Empresa de pruebas real + Ambiente Hacienda   | ✅     |
| 12     | Certificación apitest 5 DTE + 2 eventos       | ✅     |
| 13     | Catálogos MH (CRUD + import/export + 36 catálogos oficiales v1.4) | ✅     |
| 14     | Certificación DTE (matriz, progreso, escenarios) | ✅     |
| 15     | Eventos DTE persistentes + UI + PDF de evento | 🔜     |
| 16     | Contingencia avanzada y recepción por lotes   | 🔜     |
| 17     | Diagnóstico de errores Hacienda               | 🔜     |
| 18     | Legal + consentimiento                        | 🔜     |
| 19     | Billing self-service (Stripe / MercadoPago)   | 🔜     |
| 20     | Hardening pre-producción                      | 🔜     |
| 21     | UI/UX AppShell + design system                | 🔜     |
| 22     | NeoProfit básico                              | 🔜     |
| 23     | NeoScanAI integrado                           | 🔜     |
| 24     | NeoConnect API comercial                      | 🔜     |
| 25     | NeoPOS básico                                 | 🔜     |
| 26     | NeoPortal Clientes                            | 🔜     |
| 27–28  | NeoSTP Mobile API + MVP                       | 🔜     |
| 29     | SuperAdmin operativo avanzado                 | 🔜     |
| 30     | Preparación comercial y documentación         | 🔜     |

## Pruebas

```powershell
dotnet test NeoSTP.slnx                          # corre los 155 tests unit + integration
dotnet test tests/NeoSTP.Tests.Unit              # solo unit (rápido, ~10s)
```

Cobertura por área:

| Área                              | Tests | Ubicación                                                              |
| --------------------------------- | ----- | ---------------------------------------------------------------------- |
| Auth (BCrypt, login)              | 11    | `tests/NeoSTP.Tests.Unit/Auth/`                                        |
| Empresas (límites)                | 5     | `tests/NeoSTP.Tests.Unit/Empresas/`                                    |
| Clientes (validadores)            | 21    | `tests/NeoSTP.Tests.Unit/Clientes/`                                    |
| DTE — DataProtection              | 4     | `tests/NeoSTP.Tests.Unit/Dte/`                                         |
| DTE — Cálculo totales             | 8     | `tests/NeoSTP.Tests.Unit/Dte/DteCalculatorTests.cs`                    |
| DTE — Generación JSON             | 5     | `tests/NeoSTP.Tests.Unit/Dte/DteGeneratorTests.cs`                     |
| DTE — Firma JWS                   | 6     | `tests/NeoSTP.Tests.Unit/Dte/DteSignerTests.cs`                        |
| DTE — Recepción MH                | 5     | `tests/NeoSTP.Tests.Unit/Dte/MockHaciendaReceptionTests.cs`            |
| DTE — PDF                         | 3     | `tests/NeoSTP.Tests.Unit/Dte/DtePdfServiceTests.cs`                    |
| DTE — Correo (Mock)               | 3     | `tests/NeoSTP.Tests.Unit/Dte/MockEmailSenderTests.cs`                  |
| Dashboard (EF InMemory)           | 12    | `tests/NeoSTP.Tests.Unit/Dashboard/DashboardServiceTests.cs`           |
| Worker — Retransmisión (EF + NSub)| 8     | `tests/NeoSTP.Tests.Unit/Workers/DteRetransmisionServiceTests.cs`      |
| Worker — Limpieza tokens (EF)     | 6     | `tests/NeoSTP.Tests.Unit/Workers/LimpiezaTokensServiceTests.cs`        |
| Provisioning empresa prueba (EF)  | 4     | `tests/NeoSTP.Tests.Unit/Provisioning/EmpresaPruebaSeederTests.cs`     |
| Catálogos — esquema/CRUD/Import   | 31    | `tests/NeoSTP.Tests.Unit/Catalogos/*Tests.cs` (Sprint 13)              |
| Certificación DTE — schema/servicio | 18  | `tests/NeoSTP.Tests.Unit/Dte/Certificacion/*Tests.cs` (Sprint 14)      |
