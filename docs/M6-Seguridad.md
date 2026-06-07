# M6 — Seguridad y cumplimiento

## M6.1 — Secretos fuera del repo (User Secrets en dev)

Los proyectos Web y Api tienen `UserSecretsId` (`neostp-web`, `neostp-api`). En desarrollo,
`WebApplication.CreateBuilder` carga automáticamente los User Secrets, que viven fuera del
repositorio (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`).

```bash
# Ejemplo: mover la API key de Gemini y el SMTP a user-secrets (no a appsettings.Local.json)
cd src/NeoSTP.Web
dotnet user-secrets set "Scan:Gemini:ApiKey" "<api-key>"
dotnet user-secrets set "Email:Smtp:Password" "<app-password>"
```

Orden de precedencia (mayor gana): variables de entorno > User Secrets > `appsettings.Local.json`
(gitignored) > `appsettings.json`. En producción usar variables de entorno / Key Vault.

## M6.2 — Política de contraseña + bloqueo de cuenta (configurables)

Sección `Security` en `appsettings.json` (valores por defecto seguros):

```jsonc
"Security": {
  "Password": {
    "MinLength": 8,
    "RequireUppercase": true,
    "RequireLowercase": true,
    "RequireDigit": true,
    "RequireNonAlphanumeric": false
  },
  "Lockout": {
    "MaxFailedAttempts": 5,   // 0 = sin bloqueo
    "LockoutMinutes": 15
  }
}
```

- **Complejidad:** `IPasswordPolicy` (impl `PasswordPolicy`) valida toda contraseña nueva.
  Aplicada en alta de usuario, cambio y reseteo de contraseña (`UsuariosService`). Devuelve
  código `PWD_WEAK` con la lista de requisitos faltantes.
- **Bloqueo:** `AuthService` bloquea la cuenta tras `MaxFailedAttempts` intentos fallidos
  consecutivos por `LockoutMinutes`. Configurable; `MaxFailedAttempts=0` lo desactiva.
- **MFA (TOTP):** disponible para cualquier usuario (`MfaService`); obligatorio para SuperAdmin.

## Pendiente (mayor alcance)

- **M6.3** Rotación de claves JWT/firma; retención/borrado GDPR-like; backups restaurables probados.
- **M6.4** RBAC más granular por módulo + revisión de auditoría de acciones críticas
  (la auditoría consultable ya existe — ver M3.4).
