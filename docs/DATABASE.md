# Base de datos y migrations

NeoSTP usa SQL Server 2022 y un único `NeoStpDbContext`. STAGING y PRODUCTION deben utilizar
bases, usuarios, backups y cadenas de conexión independientes. Ningún host de la aplicación aplica
migrations automáticamente en ambientes desplegados.

## Convenciones

- Toda entidad de tenant debe conservar `EmpresaId` y validarlo en servicios y consultas.
- Las restricciones únicas incluyen el scope de empresa cuando el identificador es tenant-specific.
- Las migrations son acumulativas y se revisan como código.
- No se usa `database update` directamente en PRODUCTION.
- Los datos demo, seeds y verificaciones sintéticas nunca se ejecutan contra una base comercial.

## Artefacto de migrations

Generar desde el commit exacto de la release:

    ./tools/Database/New-MigrationRelease.ps1 -ReleaseVersion v1.0.0-rc.1

El comando usa el host `SchemaDesign`, que rechaza conexiones, y produce un SQL idempotente más
un manifest con commit, migration final y hashes SHA-256. El archivo rastreado
`deploy/migrations/manifest.json` debe actualizarse cuando cambia la cadena de migrations.

Flujo obligatorio:

    review de migration
    → SQL idempotente
    → backup verificado
    → aplicación en STAGING
    → smoke y schema validation
    → aprobación
    → aplicación controlada en PRODUCTION

## Gates SQL Server reales

El job `SQL Server 2022 migrations` crea bases GUID desechables y ejecuta:

1. cadena completa de migrations sobre una base vacía;
2. reaplicación del script idempotente;
3. correlativos, idempotencia y concurrencia fiscal DTE aislados por empresa;
4. aplicación idempotente y rollback transaccional de pagos;
5. deduplicación, replay y conflictos del webhook Wompi.

Ejecución con una instancia efímera autorizada:

    $env:NEOSTP_SQLSERVER_TEST_CONNECTION = '<root connection with CREATE DATABASE>'
    ./tools/Database/Invoke-SqlInvariantGates.ps1

Para una LocalDB de desarrollo creada específicamente para estas verificaciones se requiere optar
explícitamente por `-AllowLocalDb`. Cada gate valida el nombre GUID y el servidor antes de eliminar
su propia base. Nunca apunta a `NeoSTP_Staging`, `NeoSTP_Production` ni a una base de cliente.

Los JSON de `artifacts/sql-invariants` son evidencia saneada: contienen nombres sintéticos, lista de
checks y migrations, pero no cadenas de conexión, secretos, payloads comerciales ni respuestas reales
de Hacienda o proveedores de pago.

## Rollback

Cada cambio de schema debe tener una estrategia revisada. Si un downgrade pierde datos o no es seguro,
el rollback consiste en retirar tráfico, reinstalar los artefactos anteriores y restaurar el backup
validado. La restauración física y la medición RPO/RTO pertenecen al sprint de Disaster Recovery.
