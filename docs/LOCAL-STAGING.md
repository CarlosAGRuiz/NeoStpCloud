# STAGING local en Windows

Este perfil levanta NeoSTP STAGING en la misma máquina de desarrollo sin compartir base,
secretos, DataProtection, logs, tareas ni puertos con `NeoSTP_Cloud` o con una futura
instalación de producción.

## Topología inicial

| Recurso | Valor |
|---|---|
| Base | `NeoSTP_Staging` en SQL Server 2022 Express local |
| Data root | `%LOCALAPPDATA%\NeoSTP\STAGING` |
| API interna | `http://127.0.0.1:5158` |
| Web interna | `http://127.0.0.1:5131` |
| API pública esperada | `https://staging-api.neostp.com` |
| Web pública esperada | `https://staging.neostp.com` |
| Persistencia de procesos | tareas `NeoSTP STAGING API/Web/Worker` al iniciar sesión |
| Worker | desactivado hasta aprobar preflight y providers |

Los puertos internos permanecen en loopback. Cloudflare Tunnel debe terminar TLS y enrutar
los dos hostnames hacia esos puertos; no se deben abrir 5131/5158 en el firewall.

## Instalación o actualización

Desde una rama o tag revisado y con el árbol de trabajo limpio:

```powershell
./tools/Deployment/Install-LocalStaging.ps1
```

El instalador es idempotente: genera el SQL de release, valida que cubra todas las migrations,
lo aplica a `NeoSTP_Staging`, publica API/Web/Worker desde el mismo commit y ejecuta health y
login. Lee la conexión administrativa desde el `appsettings.Local.json` ignorado por Git,
pero crea para runtime el login dedicado `neostp_staging_app`; no reutiliza la cuenta
administrativa ni la persiste en `deployment.json`.

La credencial del administrador STAGING y las claves runtime se generan aleatoriamente y se
guardan cifradas con DPAPI para el usuario Windows actual. Para obtener un objeto
`PSCredential` sin imprimir la contraseña:

```powershell
$stagingCredential = ./tools/Deployment/Get-LocalStagingCredential.ps1
```

## Cloudflare Tunnel pendiente

En el túnel ya instalado en esta máquina, crear exactamente estas rutas públicas:

```text
staging.neostp.com      -> http://127.0.0.1:5131
staging-api.neostp.com  -> http://127.0.0.1:5158
```

Después se deben verificar DNS, TLS, `/health/live`, `/health/ready`, login y selección de
empresa desde otra red. No modificar las rutas existentes de `app.neostp.com` ni
`api.neostp.com`.

## Activación del Worker

Sólo después de validar API/Web, providers y outbox:

```powershell
./tools/Deployment/Install-LocalStaging.ps1 -SkipPublish -SkipDatabase -EnableWorker
```

Confirmar que la tarea `NeoSTP STAGING Worker` queda en ejecución y observar reintentos,
latencia y estados `DEAD` antes de declarar operativo el gate de Notification Outbox.

## Limitación temporal

Las tareas usan la cuenta interactiva actual porque la sesión no tiene privilegios para
crear servicios Windows. El ambiente arranca al iniciar sesión, pero no sobrevive un cierre
de sesión. Antes de una Release Candidate debe migrarse API/Web/Worker a identidades de
servicio y concederles ACL y permisos SQL mínimos equivalentes.