# Runbook — Disaster Recovery (Sprint 20)

Plan de recuperación ante desastres de **NeoSTP Cloud**. Cubre respaldo, restauración,
rotación de secretos y los objetivos de recuperación. Complementa el módulo de
**Hardening** (`/Hardening`, `Ops_BackupJobs`, cuotas e IP allowlist).

## Objetivos de recuperación

| Métrica | Objetivo | Notas |
|---|---|---|
| **RPO** (pérdida máxima de datos) | ≤ 1 h | Backups diferenciales/full programados + log shipping recomendado |
| **RTO** (tiempo máximo de recuperación) | ≤ 4 h | Restaurar BD + relanzar Api/Web/Worker |

## Componentes a respaldar

1. **Base de datos SQL Server** `NeoSTP_Cloud` — fuente de verdad (empresas, DTE, eventos, billing…).
2. **Llaves de DataProtection** — cifran secretos DTE (password MH, certificados, secretos MFA/TOTP).
   Ubicación: `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` (Windows) o `/var/aspnet/DataProtection-Keys` (Linux).
   > ⚠️ **Sin estas llaves, los secretos cifrados (incluido el secreto TOTP de MFA) son irrecuperables.**
3. **Certificados MH** por empresa — almacenados cifrados en `Dte_Configuracion`; se recuperan con la BD + llaves.
4. **`appsettings.Local.json`** de cada servicio (cadena de conexión, JWT key, toggles) — fuera del repo.

## Respaldo

### Lógico (incluido en la app — Sprint 20)
- Módulo `BackupService` + `BackupWorker` generan un **manifiesto** (snapshot de conteos/metadatos)
  con checksum SHA-256, registrado en `Ops_BackupJobs` y subido al storage (`Hardening:Backup:StorageProvider`
  = LOCAL | AZURE_BLOB | S3). Sirve como verificación de integridad y bitácora; **no sustituye** el backup físico.
- Bajo demanda: `POST /api/hardening/backups/ejecutar` o botón en `/Hardening`.
- Programado: `Hardening:Backup:WorkerEnabled=true` + `IntervaloHoras`.

### Físico SQL Server (operación)
```sql
-- FULL diario
BACKUP DATABASE [NeoSTP_Cloud]
  TO DISK = 'X:\backups\NeoSTP_Cloud_FULL.bak'
  WITH INIT, CHECKSUM, COMPRESSION;

-- DIFERENCIAL cada hora
BACKUP DATABASE [NeoSTP_Cloud]
  TO DISK = 'X:\backups\NeoSTP_Cloud_DIFF.bak'
  WITH DIFFERENTIAL, CHECKSUM, COMPRESSION;

-- LOG cada 15 min (modo recovery FULL) para RPO bajo
BACKUP LOG [NeoSTP_Cloud] TO DISK = 'X:\backups\NeoSTP_Cloud_LOG.trn' WITH CHECKSUM;
```
Copiar los `.bak/.trn` y la carpeta de llaves DataProtection a almacenamiento **off-site** (Azure Blob / S3).

## Restauración

1. Provisionar SQL Server e instancia de cómputo.
2. Restaurar BD:
   ```sql
   RESTORE DATABASE [NeoSTP_Cloud] FROM DISK='...FULL.bak' WITH NORECOVERY, REPLACE;
   RESTORE DATABASE [NeoSTP_Cloud] FROM DISK='...DIFF.bak' WITH NORECOVERY;
   RESTORE LOG     [NeoSTP_Cloud] FROM DISK='...LOG.trn'  WITH RECOVERY;
   ```
3. Restaurar la carpeta **DataProtection-Keys** (sin ella, los secretos cifrados no se descifran).
4. Restaurar `appsettings.Local.json` (cadena de conexión + JWT key).
5. Aplicar migraciones pendientes: `dotnet ef database update` (el `DatabaseSeeder` lo hace al arrancar la Api).
6. Levantar Api → Web → Worker. Validar `GET /health` y un login.
7. Validar emisión DTE de prueba (ambiente PRUEBAS) antes de reabrir producción.

## Rotación de secretos

- **JWT key** (`Jwt:Key`): rotar invalida los access tokens vigentes (los usuarios reinician sesión). No afecta datos.
- **Llaves DataProtection**: NO eliminar las antiguas al rotar; DataProtection conserva el anillo de llaves
  para descifrar lo viejo. Borrarlas obliga a **reingresar** todos los secretos (passwords MH, certificados,
  y a **re-enrolar MFA** de los usuarios).
- **MFA/TOTP**: si se pierde el secreto de un usuario, deshabilitar su MFA (`POST /api/auth/mfa/disable` con código
  de recuperación) o restablecerlo por soporte, y volver a enrolar.

## Verificación periódica (recomendado)

- Restaurar el último backup en un entorno aislado **mensualmente** y correr el smoke (`/health`, login, emisión PRUEBAS).
- Ejecutar `ops/k6/baseline.js` para confirmar latencia/errores dentro de umbrales.
- Correr el escaneo `OWASP ZAP Baseline` (workflow `zap-baseline.yml`).

## Endurecimiento operativo activable (Sprint 20)

- **Rate limiting** (`Core_ApiQuotas`): definir cuotas por empresa/plan/módulo desde `/Hardening`.
- **IP allowlist** (`Core_AdminIpAllowlist`): restringir el acceso SuperAdmin por IP/CIDR.
- **MFA SuperAdmin**: obligatorio (login marca `MfaEnrollmentRequired` hasta enrolar).
