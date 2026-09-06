# Actualización controlada del esquema activo 79 → 91

Herramienta **preparada, no ejecutada contra SQL**. No instala, detiene ni inicia hosts. No configura ambientes fiscales, correo, tipos autorizados ni campañas. No llama Hacienda. El uso activo necesita revisión operacional del agente principal y ventana sin escrituras.

Desde la raíz del repositorio:

```powershell
dotnet build tools/ClientCertificationSchemaUpgrade -c Release -p:DebugType=None -p:DebugSymbols=false
dotnet tools/ClientCertificationSchemaUpgrade/bin/Release/net10.0/ClientCertificationSchemaUpgrade.dll --self-test
dotnet tools/ClientCertificationSchemaUpgrade/bin/Release/net10.0/ClientCertificationSchemaUpgrade.dll --preview
```

Sin argumentos equivale a `--preview`. Comprueba evidencia local, consulta inventario Windows, valida SQL local y lee esquema/huellas; no crea backup ni aplica migraciones. `Passed=true` significa que el inventario terminó correctamente; `ReadyForApply` sólo será verdadero cuando ambas tareas estén Disabled, no haya procesos API/Web/Worker ni listeners 5031/5058. No inicia proveedores ni un host de aplicación.

El subproceso Windows PowerShell reconstruye su `PSModulePath` predeterminado para evitar heredar módulos incompatibles de PowerShell 7; este ajuste afecta sólo al entorno hijo. No cambia políticas de ejecución ni variables globales. Un fallo del guard conserva únicamente código de error y salida, nunca stderr íntegro, en `tmp/client-schema-upgrade/windows-guard-failures/`.

La acción modificadora usa únicamente `--apply-active-schema-79-to-91`. No admite servidores, bases, rutas ni objetivos de migración proporcionados en argumentos. Exige:

- Manifiesto fijo del candidato `20260905T222711Z-7b556dd2c9b74a8680c6dbeb4edc03d6`, SHA-256 fijado en código, tres hosts y verificación exacta de sus 647 archivos, sin extras. El ensamblado Infrastructure que ejecuta el migrador debe tener el mismo SHA-256 que el API del candidato; compilar con propiedades distintas o código modificado bloquea antes de abrir SQL.
- Los TRX concretos aprobados: 2,304 unitarias y 9 de integración, todas ejecutadas y aprobadas.
- Ensayo de clon 79 → 91 con 22 comprobaciones y ensayo piloto del cliente con cuatro esquemas aprobados, rollback comprobado y cero autenticaciones/recepciones.
- Tareas exactas `NeoSTP API` y `NeoSTP Web` Disabled; cero procesos de los tres hosts y cero listeners en sus puertos. Repite esta verificación justo antes de migrar.
- SQL Server 16, instancia predeterminada en este Windows, catálogo `NeoSTP_Cloud`, historial exacto de 79 como prefijo del ensamblado y exactamente las doce migraciones conocidas hasta `20260905221056_CERT2_TenantDteTypeAuthorization`.

Antes de aplicar crea un directorio nuevo `NeoClientUpgrade_<GUID>` bajo el directorio Backup del servicio SQL. Valida ausencia de reparse points y ACL heredada restringida a SYSTEM, administradores, usuario actual y servicio MSSQLSERVER. Se admite CREATOR OWNER únicamente como ACE InheritOnly, nunca como permiso efectivo a otro principal. El propietario efectivo también debe estar permitido. El backup usa COPY_ONLY/CHECKSUM y se comprueba con RESTORE VERIFYONLY/CHECKSUM; después verifica también los permisos, propietario y ausencia de reparse del propio archivo `.bak`. Se conserva y se registra su hash, sin copiarlo al repositorio.

Se calculan hashes en el servidor para todos los registros de **todas las tablas originales**, incluidos todos los tenants y datos globales. La lista de columnas se fija antes de migrar; excluye rowversion y la tabla de historial EF. Cada tabla debe tener PK estable. No se extraen filas ni datos secretos: sólo recuentos y digestos. Verifica estabilidad durante el backup y preservación después de las doce migraciones.

El ejecutor llama directamente a `IMigrator` con el objetivo 91 explícito, sin `AddInfrastructure`, arranque, bootstrap ni seed. Al terminar comprueba historial exacto, huellas originales, DBCC CHECKDB sin errores, campañas vacías y CSV de tipos aún sin provisionar. Mantiene los hosts detenidos para la revisión y el cambio de binarios/configuración posterior.

Ante un fallo conserva backup y evidencia y **no ejecuta Down, restaura ni reinicia automáticamente**. La serie de doce migraciones no se presenta como una única transacción atómica. Después de un fallo intermedio debe inspeccionarse el historial real y decidir recuperación antes de abrir el sistema. La evidencia se guarda en `tmp/client-schema-upgrade/<GUID>/results.json`.
