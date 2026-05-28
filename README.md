# NeoSTP Cloud Web

Plataforma SaaS multiempresa para emisión de Documentos Tributarios Electrónicos (DTE) en El Salvador y suite de módulos de negocio asociados.

> **Versión actual: Sprint 8 — Dashboard operativo y panel SuperAdmin** ✅
> El ciclo completo de emisión está implementado de punta a punta. El dashboard muestra KPIs en tiempo real: DTE emitidos, facturación del mes, tendencia diaria y distribución por estado (Chart.js). El panel SuperAdmin incluye métricas globales, top 10 empresas y alertas de planes próximos a vencer.

## Stack

- **.NET 10** (LTS, soporte hasta nov-2028)
- **ASP.NET Core MVC + Razor** (Web)
- **ASP.NET Core Web API** + **OpenAPI nativo** (Api)
- **SQL Server 2022** + **Entity Framework Core 10**
- **Serilog** (logs estructurados a consola y archivo)
- **.NET Worker Service** (procesos en segundo plano)
- **xUnit + FluentAssertions** (83 pruebas, todas pasando)
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
| `Hacienda:Client`| `Mock`  | `HttpHaciendaAuthClient` + `HttpHaciendaReceptionClient` (POST `/seguridad/auth` y `/fesv/recepciondte`) |
| `Dte:Signer`     | `Mock`  | `Pkcs12DteSignerService` (RS256 con PFX en `DteConfiguracion`)                     |
| `Email:Provider` | `Mock`  | `SmtpEmailSender` (MailKit, STARTTLS/SSL)                                          |

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

# Correr pruebas (83 unit tests)
dotnet test NeoSTP.slnx
```

También hay una skill local `.claude/skills/neostp/` con todos los comandos cotidianos.
Invócala como `/neostp <subcomando>` dentro de Claude Code.

## Quickstart — flujo E2E con mocks

Con la configuración por defecto (todos los toggles en `Mock`) puedes ejercitar
el ciclo completo de emisión DTE sin certificado, sin credenciales MH y sin
servidor SMTP. En PowerShell, desde la raíz del repo:

```powershell
# 1. Crear la BD y aplicar las 6 migraciones
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

### Catálogos (cualquier autenticado)

```
GET  /api/catalogos
GET  /api/catalogos/{codigo}/items     # ej: MONEDA, TIPO_FACTURA, ESTADO_USUARIO
```

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
| 9      | Worker jobs y resiliencia              | ⏳     |

## Pruebas

```powershell
dotnet test NeoSTP.slnx                          # corre los 83 tests unit + integration
dotnet test tests/NeoSTP.Tests.Unit              # solo unit (rápido, ~10s)
```

Cobertura por área:

| Área                      | Tests | Ubicación                                              |
| ------------------------- | ----- | ------------------------------------------------------ |
| Auth (BCrypt, login)      | 11    | `tests/NeoSTP.Tests.Unit/Auth/`                        |
| Empresas (límites)        | 5     | `tests/NeoSTP.Tests.Unit/Empresas/`                    |
| Clientes (validadores)    | 21    | `tests/NeoSTP.Tests.Unit/Clientes/`                    |
| DTE — DataProtection      | 4     | `tests/NeoSTP.Tests.Unit/Dte/`                         |
| DTE — Cálculo totales     | 8     | `tests/NeoSTP.Tests.Unit/Dte/DteCalculatorTests.cs`    |
| DTE — Generación JSON     | 5     | `tests/NeoSTP.Tests.Unit/Dte/DteGeneratorTests.cs`     |
| DTE — Firma JWS           | 6     | `tests/NeoSTP.Tests.Unit/Dte/DteSignerTests.cs`        |
| DTE — Recepción MH        | 5     | `tests/NeoSTP.Tests.Unit/Dte/MockHaciendaReceptionTests.cs` |
| DTE — PDF                 | 3     | `tests/NeoSTP.Tests.Unit/Dte/DtePdfServiceTests.cs`    |
| DTE — Correo (Mock)       | 3     | `tests/NeoSTP.Tests.Unit/Dte/MockEmailSenderTests.cs`  |
| Dashboard (EF InMemory)   | 12    | `tests/NeoSTP.Tests.Unit/Dashboard/DashboardServiceTests.cs` |
