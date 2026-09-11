# Disaster Recovery de NeoSTP Cloud

Esta es la fuente pública y operativa para respaldar y recuperar NeoSTP Cloud. No contiene credenciales, rutas privadas ni evidencia de clientes. El runbook histórico de Sprint 20 describe el diseño original; este documento define el procedimiento vigente y sus gates.

## Objetivos

| Métrica | Objetivo | Evidencia mínima |
|---|---:|---|
| RPO | ≤ 1 hora | FULL diario, DIFFERENTIAL cada hora y LOG cada 15 minutos |
| RTO | ≤ 4 horas | Restore aislado cronometrado, DBCC CHECKDB y smoke operativo |

Un manifiesto lógico de `BackupService` es útil para auditoría, pero no reemplaza un backup físico de SQL Server.

## Alcance recuperable

Cada ambiente conserva por separado:

- base SQL Server;
- key ring de DataProtection;
- certificado RSA con llave privada exportado mediante un procedimiento seguro;
- configuración operativa y secretos desde el secret store;
- artefactos API, Web y Worker del mismo tag;
- scripts SQL y manifest de migrations de la release.

Los secretos y certificados no se copian al repositorio ni se incluyen en evidencia pública. El key ring, PFX y configuración de recuperación deben residir en un volumen cifrado y con ACL mínima.

## Prerrequisitos

1. SQL Server 2022 accesible con una identidad operativa autorizada para `BACKUP DATABASE`, `RESTORE DATABASE`, `DBCC CHECKDB`, crear y eliminar bases de drill.
2. PowerShell 5.1 o posterior.
3. Variable de proceso `NEOSTP_SQLSERVER_ADMIN_CONNECTION`; nunca pasar la cadena en argumentos ni guardarla en scripts.
4. Una ruta local que el servicio de SQL Server pueda escribir.
5. Una segunda ruta física, disco externo o NAS para `-OffsiteDirectory`. Puede ser infraestructura propia sin servicio pagado.

Ejemplo de sesión, sin mostrar el secreto:

```powershell
$env:NEOSTP_SQLSERVER_ADMIN_CONNECTION = '<inyectada-desde-secret-store>'
```

## Backup físico

FULL diario:

```powershell
./tools/DisasterRecovery/New-PhysicalBackup.ps1 `
  -Database NeoSTP_Production `
  -OutputDirectory 'D:\NeoSTP\Backups\Production' `
  -OffsiteDirectory '\\nas\neostp-dr\Production' `
  -BackupType FULL
```

DIFFERENTIAL cada hora:

```powershell
./tools/DisasterRecovery/New-PhysicalBackup.ps1 `
  -Database NeoSTP_Production `
  -OutputDirectory 'D:\NeoSTP\Backups\Production' `
  -OffsiteDirectory '\\nas\neostp-dr\Production' `
  -BackupType DIFFERENTIAL
```

LOG cada 15 minutos, con la base en recovery model `FULL`:

```powershell
./tools/DisasterRecovery/New-PhysicalBackup.ps1 `
  -Database NeoSTP_Production `
  -OutputDirectory 'D:\NeoSTP\Backups\Production' `
  -OffsiteDirectory '\\nas\neostp-dr\Production' `
  -BackupType LOG
```

La herramienta:

- crea un archivo nuevo y nunca sobrescribe un backup anterior;
- usa `CHECKSUM` y `COMPRESSION` cuando la edición de SQL Server lo admite; Express conserva `CHECKSUM` sin compresión;
- ejecuta `RESTORE VERIFYONLY WITH CHECKSUM`;
- calcula SHA-256;
- copia el `.bak` y su manifest off-site primero como `.partial`, compara los hashes y luego publica los archivos;
- produce un manifest JSON sin servidor ni credenciales.

El scheduler de Windows debe ejecutar FULL, DIFFERENTIAL y LOG con una identidad dedicada. Una alerta es obligatoria si el último backup válido supera el RPO.

## Restore drill aislado

Ejecutar mensualmente y antes de cada release candidate:

```powershell
./tools/DisasterRecovery/Invoke-RestoreDrill.ps1 `
  -BackupPath 'D:\NeoSTP\Backups\Production\NeoSTP_Production_FULL_20260910T180000Z.bak' `
  -EvidencePath 'artifacts/dr/restore-drill.json'
```

La herramienta crea exclusivamente una base con prefijo `NeoSTP_Drill_`, restaura los archivos en las rutas por defecto de la instancia, ejecuta `DBCC CHECKDB`, registra el número de tablas de usuario y elimina la base sintética. Nunca reemplaza una base existente. `-KeepRestoredDatabase` se usa solo cuando el operador necesita continuar el smoke manual y asume su limpieza posterior.

Después del restore SQL, en un host aislado:

1. Restaurar el key ring en el `DataProtection:KeyRingPath` del ambiente de drill.
2. Instalar el certificado RSA correspondiente con su llave privada y ACL para la identidad del servicio.
3. Inyectar configuración y secretos desde el secret store.
4. Instalar API, Web y Worker del mismo tag que el manifest de migrations.
5. Iniciar API y Web; mantener Worker detenido hasta validar schema y dependencias.
6. Validar `/health/live`, `/health/ready`, login, empresa correcta, aislamiento de tenant, inventario y PDF.
7. Habilitar Worker y comprobar que no duplica procesamiento.
8. Ejecutar DTE únicamente contra MH PRUEBAS.
9. Registrar tiempos, resultado, responsable y hash de la evidencia en el sistema privado de operación.

## Criterios de aprobación

- `.bak` verificado por SQL Server y SHA-256.
- copia en una segunda ubicación con hash idéntico.
- base aislada restaurada y `DBCC CHECKDB` aprobado.
- base de drill eliminada o excepción operativa documentada.
- DataProtection recupera un secreto sintético.
- API, Web y Worker arrancan desde el mismo artefacto.
- health, login, tenant, inventario, PDF y DTE PRUEBAS aprobados.
- RPO observado ≤ 1 hora y RTO medido ≤ 4 horas.

La documentación y una prueba sintética local no cierran por sí solas el gate productivo: se necesita evidencia de una restauración de la infraestructura seleccionada para STAGING/PRODUCTION.

## Fallos y rollback

- No abrir tráfico si `RESTORE VERIFYONLY`, `DBCC CHECKDB`, DataProtection o health fallan.
- No borrar backups antiguos hasta completar y verificar la nueva cadena.
- Si se pierde el key ring o su certificado, no generar llaves nuevas como supuesto arreglo: los secretos históricos seguirán cifrados e irrecuperables.
- Si el restore drill deja una base por un fallo de limpieza, validar que su nombre empiece con `NeoSTP_Drill_` antes de eliminarla manualmente.
- Rotar inmediatamente cualquier credencial que aparezca en logs, evidencia o comandos compartidos.
