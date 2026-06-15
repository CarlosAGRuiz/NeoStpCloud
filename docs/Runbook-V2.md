# Runbook operativo — NeoSTP Cloud V2

> Cierre Fase V2 (2026-06-10). Procedimientos mínimos para desplegar, respaldar,
> restaurar, rotar claves y operar soporte. Complementa `docs/Plan-Cierre-Fase-V2.md` (V2-E3/E4).

---

## 1. Topología y procesos

| Proceso | Proyecto | Rol |
|---|---|---|
| API | `src/NeoSTP.Api` | REST multi-tenant (JWT + API Keys NeoConnect), OpenAPI en `/openapi/v1.json`, Scalar en `/scalar/v1`. |
| Web | `src/NeoSTP.Web` | Backoffice MVC (cookies) + portal público del receptor (`/portal/{token}`). |
| Worker | `src/NeoSTP.Worker` | Jobs: recordatorios de cobro, generación de alertas, webhooks NeoConnect. |

Los tres procesos comparten la misma base SQL Server (`ConnectionStrings:NeoStpDb`) y el mismo
ensamblado de Infrastructure; **las migraciones las aplica un solo proceso** (ver §2.2).

## 2. Despliegue

### 2.1 Publicación

```powershell
dotnet publish src/NeoSTP.Api    -c Release -o out/api
dotnet publish src/NeoSTP.Web    -c Release -o out/web
dotnet publish src/NeoSTP.Worker -c Release -o out/worker
```

Cada salida se corre como servicio (IIS/Windows Service/systemd/contendor). Variables por entorno:
`ASPNETCORE_ENVIRONMENT` (`Staging`/`Production`) y la configuración sensible vía variables de
entorno o `appsettings.Production.json` **fuera del repo** (mismas claves que `appsettings.Local.json`).

### 2.2 Migraciones

Aplicar antes de levantar la versión nueva, desde la máquina de despliegue:

```powershell
dotnet ef database update --project src/NeoSTP.Infrastructure --startup-project src/NeoSTP.Api --context NeoStpDbContext
```

- Las migraciones son acumulativas e idempotentes (EF history table `__EFMigrationsHistory`).
- El seed (`SeedData`, EF `HasData`) corre dentro de las migraciones: módulos, permisos y planes
  nuevos llegan por migración, nunca a mano.
- Rollback: `dotnet ef database update <MigracionAnterior>` (verificar primero que la migración
  a revertir no haya destruido datos; si destruyó, restaurar backup — §3).

### 2.3 Smoke post-despliegue

1. `GET /health/live` y `GET /health/ready` en API y Web → 200 (ready valida BD, correo y storage).
2. Login web + `POST /api/auth/login` con un usuario de la empresa demo.
3. Emitir un DTE de prueba en ambiente 00 (`POST /api/dte/emitir`) si el entorno tiene certificado de pruebas.
4. Revisar `logs/neostp-*-.log` (Serilog, rotación diaria, 30 días retenidos) por errores de arranque.

## 3. Backup y restore

### 3.1 Backup

- **BD (todo el estado vive aquí)**: backup FULL diario + LOG cada 15–60 min según RPO.

```sql
BACKUP DATABASE [NeoSTP_Cloud] TO DISK = N'D:\Backups\NeoSTP_Cloud_FULL.bak'
  WITH COMPRESSION, CHECKSUM, INIT;
```

- Los blobs de NeoScan pueden vivir en **BD** (`Scan:Storage:Provider=Database`) o en
  **filesystem** (`Scan:Storage:Provider=FileSystem`). Si se usa filesystem, respaldar
  `Scan:Storage:Root` junto con la BD.
- Ver runbook HB-7 para storage, secretos y retencion:
  `docs/Runbook-Storage-Secretos-Retencion.md`.
- Copiar los `.bak` a almacenamiento fuera del servidor (regla 3-2-1).

### 3.2 Restore (probado como parte del release)

```sql
RESTORE DATABASE [NeoSTP_Cloud] FROM DISK = N'D:\Backups\NeoSTP_Cloud_FULL.bak'
  WITH REPLACE, RECOVERY;
```

1. Detener Api/Web/Worker.
2. Restaurar FULL (+ LOGs hasta el punto deseado con `NORECOVERY`/`STOPAT`).
3. Levantar API y correr el smoke de §2.3.
4. Validar con una empresa real: login, listado de DTE, descarga de un PDF.

## 4. Rotación de claves y certificados

### 4.1 Clave JWT (`Jwt:Key`)

1. Generar clave nueva (≥ 32 bytes aleatorios, base64).
2. Actualizar `Jwt:Key` en la config del entorno de API **y Web** (deben coincidir).
3. Reiniciar procesos. Efecto: los tokens emitidos con la clave anterior quedan inválidos —
   los usuarios re-loguean y la app móvil renueva sesión. Programar en ventana de bajo uso.
4. Las API Keys de NeoConnect **no** dependen del JWT (hash propio en BD); no se ven afectadas.

### 4.2 Certificado de firma DTE (por empresa)

1. La empresa carga el certificado nuevo en **Configuración DTE** (web) o vía API de configuración.
2. Probar primero en ambiente `00` (pruebas MH): emitir un DTE y verificar PROCESADO.
3. Cambiar a ambiente `01` (producción). El certificado anterior queda fuera de uso de inmediato.
4. Vigilar la alerta automática `CERT_POR_VENCER` (se genera cuando faltan ≤ 30 días).

### 4.3 Credenciales SMTP por empresa

Rotación desde la web (Configuración de correo) o `POST /api/correo` por empresa; el secreto se
guarda cifrado. Validar con el botón "Probar correo" del diagnóstico (`/Hardening`).

## 5. Retención y borrado de datos

- **Auditoría** (`Auditoria_Eventos`): retener mínimo 12 meses; usar `Worker:LimpiezaAuditoria`
  para purga programada (el servicio no baja de 30 dias).
- **Logs Serilog**: 30 días en disco (config `Serilog:WriteTo:File`); ampliar solo en staging.
- **DTE y libros fiscales**: NO se borran — obligación fiscal salvadoreña (≥ 10 años recomendado).
- **Baja de empresa (offboarding)**: inactivar empresa (corta acceso de inmediato), exportar sus
  DTE/JSON/PDF y libros CSV a petición, y borrar datos personales no fiscales tras el plazo
  contractual. Los enlaces de portal se revocan al inactivar (token valida empresa en cada acceso).
- **Tokens de portal**: expiran por defecto a 30 días; revocables desde "Portal del cliente".

## 6. Checklist de secretos

- [ ] Repo limpio: ningún `appsettings.Local.json`, `.pfx`, clave o password commiteado (`git log -p` spot-check).
- [ ] `Jwt:Key` ≠ valor placeholder en todos los entornos.
- [ ] SQL con login propio de la app (no `sa`), permisos mínimos (db_owner solo si aplica migraciones).
- [ ] SMTP global y por empresa con contraseñas de aplicación, no credenciales personales.
- [ ] Claves de pasarela (Wompi/PayPal/Stripe) solo en config del entorno; webhooks con secreto validado.
- [ ] Certificados DTE solo en BD (cifrados); nunca en disco ni en el repo.
- [ ] `Scan:Gemini:ApiKey`, FCM, WhatsApp Meta y pasarelas solo en config externa.
- [ ] DataProtection key ring persistente y respaldado; si se pierde, se deben reingresar secretos cifrados.
- [ ] Si `Scan:Storage:Provider=FileSystem`, `Scan:Storage:Root` tiene ACL minima, cifrado de disco,
  backup y readiness verde.
- [ ] `Cors:AllowedOrigins` explícito en producción (no `*`).

## 7. Checklist de release

- [ ] `dotnet build NeoSTP.slnx` y `dotnet test` (unit + integration) verdes.
- [ ] Migraciones nuevas revisadas (sin `DropTable`/`DropColumn` accidental) y aplicadas en staging.
- [ ] OpenAPI/`src/NeoSTP.Api/README.md` actualizados si cambió la superficie REST.
- [ ] Backup FULL inmediatamente antes de migrar producción.
- [ ] Smoke §2.3 en producción tras el despliegue.
- [ ] Etiquetar release en git (`git tag v2.x`) y anotar migraciones incluidas.

## 8. Soporte: incidentes comunes

| Síntoma | Primer paso |
|---|---|
| 401 masivo en API/app | ¿Se rotó `Jwt:Key` sin avisar? Ver §4.1. |
| DTE RECHAZADO | Revisar diagnóstico de errores Hacienda en el detalle del DTE; causas típicas: catálogo MH desactualizado, NIT/NRC receptor, `numDocumento` en retención (07). |
| Correo no sale desde API pero sí desde web | Falta bloque `Email` en config del entorno API o credenciales por empresa sin replicar. Probar `/Hardening`. |
| Worker no envía recordatorios | La empresa debe tener configuración **activa** en `/Cobros/Recordatorios` y el job habilitado (`Worker:RecordatoriosCobro`). |
| Lentitud general | Revisar `logs/`, espacio en disco de SQL, e índices tras crecimiento (`sys.dm_db_index_physical_stats`). |
