# NeoSTP Cloud Web

Plataforma SaaS multiempresa para emisión de Documentos Tributarios Electrónicos (DTE) en El Salvador y suite de módulos de negocio asociados.

> Versión actual: **Sprint 0 — Setup técnico**. Aún no hay funcionalidad de negocio; solo cimientos.

## Stack

- **.NET 10** (LTS, soporte hasta nov-2028)
- **ASP.NET Core MVC + Razor** (Web)
- **ASP.NET Core Web API** + **OpenAPI nativo** (Api)
- **SQL Server 2022** + **Entity Framework Core 10**
- **Serilog** (logs estructurados a consola y archivo)
- **.NET Worker Service** (procesos en segundo plano)
- **xUnit** (pruebas)
- Autenticación prevista: **JWT** (Api) + **Cookies** (Web)

## Arquitectura

Modular monolith con separación por capas:

```
NeoSTP.slnx
├── src/
│   ├── NeoSTP.Web              # MVC/Razor (UI)
│   ├── NeoSTP.Api              # Web API (REST + OpenAPI)
│   ├── NeoSTP.Application      # Casos de uso, servicios, DTOs
│   ├── NeoSTP.Domain           # Entidades, reglas, enums
│   ├── NeoSTP.Infrastructure   # EF Core, SQL Server, integraciones
│   ├── NeoSTP.Worker           # Background jobs
│   └── NeoSTP.Shared           # Utilidades, ApiResponse, constantes
└── tests/
    ├── NeoSTP.Tests.Unit
    └── NeoSTP.Tests.Integration
```

### Referencias

| Proyecto         | Referencia a                          |
| ---------------- | ------------------------------------- |
| Web              | Application, Shared                   |
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

La cadena de conexión está en `appsettings.json` de los proyectos `Api`, `Web` y `Worker`:

```json
"ConnectionStrings": {
  "NeoStpDb": "Server=.;Database=NeoSTP_Cloud;User Id=sa;Password=jda;TrustServerCertificate=True;MultipleActiveResultSets=True"
}
```

> Las credenciales en `appsettings.json` son solo para desarrollo local. En producción deben moverse a User Secrets, variables de entorno o Azure Key Vault.

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
```

## Base de datos

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

## Endpoints disponibles (Sprint 1)

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

Catálogo `DEPARTAMENTO_ES` con los 14 departamentos de El Salvador y sus
códigos MH se siembra en el Sprint 3 (consultar vía `GET /api/catalogos/DEPARTAMENTO_ES/items`).

Catálogo `MUNICIPIO_ES` con 42 municipios/zonas post-reforma territorial 2024
(Decreto 290), ej. `CHALATENANGO_NORTE`, `LA_LIBERTAD_COSTA`. Cada item lleva
metadata `{"departamento":"CODIGO","zona":"NORTE|SUR|ESTE|OESTE|CENTRO|COSTA"}`
para permitir cascada UI. El Web Cliente filtra el dropdown de municipio al
seleccionar departamento. La distribución base es ajustable contra el CAT-013
final de Hacienda cuando se publique.

### Diagnóstico

```
GET  /health
GET  /openapi/v1.json     # solo en Development
```

## SuperAdmin inicial

Al primer arranque de la Api, `DatabaseSeeder` aplica migraciones pendientes y, si no hay
ningún usuario, crea un SuperAdmin:

| Username     | Password         |
| ------------ | ---------------- |
| `superadmin` | `ChangeMe!2026`  |

**Cambia la contraseña en el primer login** vía `POST /api/auth/change-password`
o desde el menú de usuario en la Web.

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

Ver el backlog técnico completo en la conversación inicial. Sprints planificados:

- **Sprint 0** — Setup técnico ✅
- **Sprint 1** — Seguridad y Core (login, usuarios, roles, JWT) ✅
- **Sprint 2** — Empresa y licenciamiento ✅
- **Sprint 3** — Catálogos, clientes, productos ✅
- **Sprint 3** — Catálogos, clientes, productos
- **Sprint 4** — Configuración DTE
- **Sprint 5** — Generación DTE
- **Sprint 6** — Firma y transmisión Hacienda
- **Sprint 7** — PDF, correo y documentos
- **Sprint 8** — Dashboard y SuperAdmin
