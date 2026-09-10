# Verificación SQL de autenticación

Herramienta aislada: no carga configuración, secretos, Program, DatabaseSeeder ni conexiones del cliente.
Servidor fijo `(localdb)\NeoStpAuthAudit_20260904`, autenticación integrada. Cada ejecución genera una
base `NeoStpAuthAudit_<GUID>` y elimina únicamente esa base en `finally`. No admite otra conexión.

```powershell
SqlLocalDB create NeoStpAuthAudit_20260904 17.0 -s
dotnet restore tools/AuthSqlVerification --source https://api.nuget.org/v3/index.json
dotnet run --project tools/AuthSqlVerification -c Release --no-restore -- --migration-chain
SqlLocalDB stop NeoStpAuthAudit_20260904
```

Sin `--migration-chain` crea el modelo actual con EnsureCreated. Con esa opción aplica la cadena completa
de migraciones **solo en la base nueva generada**. No es un ensayo sobre un backup productivo.

Usa servicios reales, TOTP real, conexiones/DbContexts independientes y barreras antes del primer guardado
para provocar carreras. Hash de contraseña, emisor JWT, protector y auditoría son dobles sintéticos:
este ensayo verifica persistencia/transacciones/concurrencia, no la criptografía del proveedor OIDC.
Verifica MFA, recuperación única, refresh, logout, consumo de desafío y contador de bloqueo.
Probado en SQL Server LocalDB 2025 (17.0.4025.3), no en el SQL Server 2022 del despliegue.
