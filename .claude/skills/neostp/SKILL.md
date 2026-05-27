---
name: neostp
description: Atajos de consola para el proyecto NeoSTP Cloud. Use this skill when the user wants to build, run, test, migrate, or otherwise drive the NeoSTP Cloud .NET 10 solution from PowerShell. Triggers include phrases like "compila el proyecto", "levanta la web", "levanta la api", "corre el worker", "nueva migración", "actualiza la base", "corre los tests", "formatea", "limpia", or "/neostp <subcomando>".
---

# NeoSTP — atajos de consola

Esta skill encapsula los comandos más comunes para operar la solución desde la raíz del repo (`C:\Neo\NeoSTPBusinnesSuite\NeoStpCloud`). Todos los comandos usan PowerShell.

## Cómo decidir qué comando ejecutar

1. Lee el argumento que el usuario pasó tras `/neostp` (si lo hay) y compáralo contra la tabla "Subcomandos".
2. Si no hay argumento, mapea por intención:
   - "compila", "build" → `build`
   - "levanta web", "corre web" → `run-web`
   - "levanta api", "corre api" → `run-api`
   - "levanta worker", "corre worker" → `run-worker`
   - "corre tests", "pruebas" → `test`
   - "nueva migración X" → `migrate-add X`
   - "aplica migración", "actualiza base" → `migrate-update`
   - "deshacer migración" → `migrate-remove`
   - "lista migraciones" → `migrate-list`
   - "borra build", "clean" → `clean`
   - "restaura paquetes" → `restore`
   - "formato" → `format`
3. Si la intención del usuario no coincide con ningún subcomando, explica las opciones y pídele que elija.

## Subcomandos

### `build`
Compila la solución completa. Usar al inicio de la sesión o tras cambios grandes.

```powershell
dotnet build NeoSTP.slnx
```

### `restore`
Restaura paquetes NuGet sin compilar. Útil si NuGet quedó inconsistente.

```powershell
dotnet restore NeoSTP.slnx
```

### `clean`
Limpia binarios y `obj/`. Usar antes de un build limpio cuando haya artefactos viejos.

```powershell
dotnet clean NeoSTP.slnx
Get-ChildItem -Recurse -Directory -Include bin,obj | Remove-Item -Recurse -Force
```

### `run-web`
Levanta el sitio MVC (`NeoSTP.Web`).

```powershell
dotnet run --project src/NeoSTP.Web
```

### `run-api`
Levanta la Web API (`NeoSTP.Api`). En `Development` expone OpenAPI en `/openapi/v1.json` y un health check en `/health`.

```powershell
dotnet run --project src/NeoSTP.Api
```

### `run-worker`
Levanta el Worker Service.

```powershell
dotnet run --project src/NeoSTP.Worker
```

### `test`
Corre todas las pruebas (unit + integration).

```powershell
dotnet test NeoSTP.slnx
```

### `seed-reset`
Borra el usuario SuperAdmin inicial para forzar que `DatabaseSeeder` lo vuelva a crear en el próximo arranque (útil si olvidaste la contraseña). **Destructivo.**

```powershell
$conn = New-Object System.Data.SqlClient.SqlConnection "Server=.;Database=NeoSTP_Cloud;User Id=sa;Password=jda;TrustServerCertificate=True"
$conn.Open()
$c = $conn.CreateCommand()
$c.CommandText = "DELETE FROM Core_UsuarioRoles WHERE UsuarioId IN (SELECT Id FROM Core_Usuarios WHERE Username='superadmin'); DELETE FROM Core_RefreshTokens WHERE UsuarioId IN (SELECT Id FROM Core_Usuarios WHERE Username='superadmin'); DELETE FROM Core_Usuarios WHERE Username='superadmin'"
$c.ExecuteNonQuery()
$conn.Close()
```

### `login-superadmin`
Hace login del SuperAdmin contra la Api levantada (https://localhost:7043) y exporta el JWT en `$env:NEOSTP_TOKEN` para usarlo en pruebas manuales.

```powershell
Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllSkill : ICertificatePolicy { public bool CheckValidationResult(ServicePoint a, X509Certificate b, WebRequest c, int d){return true;} }
"@ -ErrorAction SilentlyContinue
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllSkill
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$r = Invoke-RestMethod -Uri https://localhost:7043/api/auth/login -Method Post -ContentType 'application/json' `
  -Body '{"usernameOrEmail":"superadmin","password":"ChangeMe!2026"}'
$env:NEOSTP_TOKEN = $r.data.accessToken
"Token guardado en `$env:NEOSTP_TOKEN ($($r.data.user.permisos.Count) permisos)"
```

### `test-unit`
Solo pruebas unitarias.

```powershell
dotnet test tests/NeoSTP.Tests.Unit
```

### `test-integration`
Solo pruebas de integración (requieren SQL Server local).

```powershell
dotnet test tests/NeoSTP.Tests.Integration
```

### `migrate-add <Nombre>`
Crea una nueva migración EF Core en `NeoSTP.Infrastructure/Persistence/Migrations`, usando `NeoSTP.Api` como startup project. Reemplaza `<Nombre>` por el nombre PascalCase de la migración (p. ej. `AddUsuarios`). Si el usuario no provee nombre, pídeselo.

```powershell
dotnet ef migrations add <Nombre> `
  --project src/NeoSTP.Infrastructure/NeoSTP.Infrastructure.csproj `
  --startup-project src/NeoSTP.Api/NeoSTP.Api.csproj `
  --output-dir Persistence/Migrations `
  --context NeoStpDbContext
```

### `migrate-update`
Aplica todas las migraciones pendientes a la base `NeoSTP_Cloud`.

```powershell
dotnet ef database update `
  --project src/NeoSTP.Infrastructure/NeoSTP.Infrastructure.csproj `
  --startup-project src/NeoSTP.Api/NeoSTP.Api.csproj `
  --context NeoStpDbContext
```

### `migrate-remove`
Quita la última migración (solo si **no** ha sido aplicada a la base).

```powershell
dotnet ef migrations remove `
  --project src/NeoSTP.Infrastructure/NeoSTP.Infrastructure.csproj `
  --startup-project src/NeoSTP.Api/NeoSTP.Api.csproj `
  --context NeoStpDbContext
```

### `migrate-list`
Lista todas las migraciones.

```powershell
dotnet ef migrations list `
  --project src/NeoSTP.Infrastructure/NeoSTP.Infrastructure.csproj `
  --startup-project src/NeoSTP.Api/NeoSTP.Api.csproj `
  --context NeoStpDbContext
```

### `db-drop`
**Destructivo.** Borra la base `NeoSTP_Cloud`. Confirma con el usuario antes de ejecutar.

```powershell
dotnet ef database drop `
  --project src/NeoSTP.Infrastructure/NeoSTP.Infrastructure.csproj `
  --startup-project src/NeoSTP.Api/NeoSTP.Api.csproj `
  --context NeoStpDbContext --force
```

### `db-ping`
Verifica conexión a SQL Server local con las credenciales del proyecto.

```powershell
$conn = New-Object System.Data.SqlClient.SqlConnection "Server=.;User Id=sa;Password=jda;TrustServerCertificate=True;Connection Timeout=5"
try { $conn.Open(); "OK"; $conn.Close() } catch { "ERROR: $($_.Exception.Message)" }
```

### `format`
Aplica `dotnet format` (style + analyzers) sobre toda la solución.

```powershell
dotnet format NeoSTP.slnx
```

### `outdated`
Lista paquetes NuGet con versiones nuevas disponibles.

```powershell
dotnet list NeoSTP.slnx package --outdated
```

## Recordatorios

- **Lanzar app web o api**: corre en foreground; ofrece `run_in_background: true` solo si el usuario va a hacer otra cosa mientras.
- **Migraciones**: nunca aplicar `migrate-update` en producción sin revisar el SQL generado. Para revisar usar `dotnet ef migrations script`.
- **Conexión SQL**: si `db-ping` falla, verificar que SQL Server esté arriba (`Get-Service MSSQLSERVER`) y que las credenciales `sa` / `jda` sigan vigentes.
- **Errores de build tras pull**: prueba en orden: `restore` → `clean` → `build`.
