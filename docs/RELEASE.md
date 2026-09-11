# Release gates de NeoSTP v1.0

Una Release Candidate puede etiquetarse únicamente cuando todos los controles aplicables estén aprobados con evidencia saneada y recuperable.

## Código y seguridad

- [ ] Build Release sin errores ni advertencias.
- [ ] Pruebas unitarias y de integración en verde.
- [ ] Pruebas sobre SQL Server 2022 real en verde.
- [ ] CI obligatorio en `main` en verde.
- [ ] Gitleaks sobre árbol e historial sin secretos activos.
- [ ] GitHub Secret Scanning sin alertas activas.
- [ ] ZAP sin hallazgos críticos o altos no resueltos/aceptados formalmente.
- [ ] Dependencias y vulnerabilidades revisadas.

## Git y artefactos

- [ ] `main` protegida; PR y checks obligatorios; force-push y borrado bloqueados.
- [ ] Commit de release identificado e inmutable.
- [ ] Artefactos API, Web y Worker producidos desde el mismo commit.
- [ ] Manifest de migrations y script SQL revisados.
- [ ] Tag SemVer de RC creado solo después de aprobar los gates.

## Ambientes y datos

- [ ] STAGING y PROD usan bases, secretos, DataProtection, storage, logs y backups independientes.
- [ ] Migrations aplicadas primero en STAGING, sin ejecución automática en PROD.
- [ ] Backup físico y copia off-site verificados.
- [ ] Restore completo realizado en un entorno aislado.
- [ ] RPO <= 1 hora y RTO <= 4 horas medidos.
- [ ] Cuentas demo sin acceso ni datos reales.

## Operación

- [ ] API, Web, Worker, SQL Server y cloudflared arrancan sin login interactivo.
- [ ] `/health/live` y `/health/ready` vigilados externamente.
- [ ] Alertas de caída, dependencias, worker, backups, disco y TLS probadas.
- [ ] Rollback reproducible y ensayado.
- [ ] ProductionGuards rechaza providers incompletos y acepta `Disabled` donde sea opcional.

## Producto y fiscal

- [ ] Smoke por roles SUPERADMIN, ADMIN, OPERADOR, CONTADOR y READONLY.
- [ ] ADMIN puede operar todos los módulos contratados y solo esos módulos.
- [ ] Smoke DTE completo en MH PRUEBAS sobre STAGING.
- [ ] Smoke comercial POS, inventario, compras, CxP/CxC, cotización, cobro y NeoScan.
- [x] Modelo de precios con/sin IVA consistente y probado.
- [ ] Notification Outbox durable e idempotente operativa.

## Mobile

- [ ] `flutter analyze` y `flutter test` en verde.
- [ ] LOCAL, STAGING y PRODUCTION usan endpoints separados.
- [ ] APK y AAB release firmados con keystore productivo respaldado fuera del repositorio.
- [ ] Instalación y smoke de release aprobados.

## Aprobación

- [ ] `v1.0.0-rc.1` desplegada y observada en STAGING.
- [ ] Riesgos aceptados documentados por responsable y fecha.
- [ ] Go/no-go aprobado.
- [ ] `v1.0.0` desplegada, verificada y monitoreada.
