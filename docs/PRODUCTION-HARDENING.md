# NeoSTP v1.0 Production Hardening

Estado operativo del cierre v1.0. Este documento es la fuente pública de seguimiento; no debe contener datos, credenciales ni evidencia de instalaciones reales.

## Alcance congelado

Desde el 10 de septiembre de 2026 el objetivo es estabilizar y liberar la plataforma existente. No se agregan módulos generales ni se inicia NeoTaller hasta cerrar los gates P0, Notification Outbox, la semántica de precio/IVA y un STAGING operativo.

La base estable es `main`. Después de activar su protección, todo incremento parte de `main` y regresa mediante PR; las ramas privadas o de soporte a clientes nunca se fusionan completas.

## Baseline confirmado

- Build Release: 0 errores y 0 advertencias.
- Pruebas locales: 2,431 unitarias y 9 de integración aprobadas; el gate adicional de SQL Server real también fue aprobado.
- Higiene del árbol público: 1,476 archivos rastreados aprobados por `Check-PublicTree.ps1`.
- Gitleaks sobre 205 commits de `main`: 0 hallazgos con excepciones exactas para tres fixtures publicados.
- Cuatro ramas remotas adicionales: 0 hallazgos.
- GitHub Secret Scanning: 0 alertas abiertas al inicio del cierre.
- CI y ZAP no llegan a ejecutar steps: GitHub reporta la cuenta bloqueada por un problema de facturación.
- `main` está protegida con PR, aprobación, checks, rama actualizada, conversación resuelta e historial lineal; force-push y borrado están bloqueados.

## Orden de ejecución

| Fase | Entregable verificable | Estado |
|---|---|---|
| 0. Freeze | Alcance, baseline y gates versionados | Completada |
| 1. Seguridad/Git | Escaneo actual e histórico, rotaciones, CI/ZAP, `main` protegida | En curso |
| 2. Ambientes | Matriz STAGING/PROD y separación en código completadas; DNS, TLS, secretos y recursos físicos pendientes | En curso (infraestructura) |
| 3. Base de datos | SQL Server 2022 efímero, migrations, aislamiento, concurrencia e idempotencia | Completada en código; CI remoto pendiente |
| 4. DR | Backup físico y restauración aislada con RPO/RTO medidos | En curso |
| 5. Plataforma | ProductionGuards completos, outbox, IVA, monitoreo y observabilidad | Pendiente |
| 6. Mobile | Ambientes, análisis/tests, firma y artefactos release | Pendiente (repositorio móvil) |
| 7. UX/Legal | Smoke por rol, DTE/comercial y limpieza legal | Pendiente |
| 8. RC | `v1.0.0-rc.1` y ejecución de todos los gates | Pendiente |
| 9. Producción | Despliegue, smoke, monitoreo y `v1.0.0` | Pendiente |

## Sprint actual: Freeze + Seguridad/Git

- [x] Confirmar `main` como base pública saneada.
- [x] Preservar por separado el trabajo local privado antes de cambiar de contexto.
- [x] Ejecutar build, suite común y gate de SQL Server real.
- [x] Eliminar de las pruebas públicas la dependencia de configuración privada.
- [x] Ejecutar higiene del árbol público.
- [x] Consultar alertas abiertas de GitHub Secret Scanning.
- [x] Escanear `main` y las ramas remotas con Gitleaks.
- [x] Clasificar fixtures sintéticos mediante excepciones de valor exacto.
- [x] Diagnosticar el fallo temprano de CI y ZAP.
- [x] Activar protección completa de `main`.
- [ ] Resolver el bloqueo de facturación de GitHub Actions (acción del propietario de la cuenta).
- [ ] Ejecutar CI, SQL Server integration, Gitleaks y ZAP en runners de GitHub.
- [ ] Revisar manualmente las referencias locales privadas antes de mover o publicar cualquier contenido.
- [ ] Rotar e invalidar cualquier credencial si la revisión manual confirma que estuvo activa; no registrar valores aquí.

## Sprint actual: Pipeline de migrations

- [x] Fijar `dotnet-ef` como herramienta local reproducible.
- [x] Versionar manifest de migrations y hash del model snapshot.
- [x] Generar SQL idempotente y manifest de artefacto sin cargar secretos ni abrir conexiones.
- [x] Rechazar cambios de modelo sin migration.
- [x] Agregar el gate que verifica en SQL Server real que el script crea una base vacía y puede reaplicarse sin cambios.
- [ ] Ejecutar el gate con una identidad efímera que pueda crear bases, localmente o en GitHub Actions cuando existan runners.
- [ ] Revisar el SQL generado para la Release Candidate y ensayarlo primero en STAGING.

## Sprint actual: Invariantes SQL de negocio

- [x] Hacer portables los verificadores SQL de DTE, aplicación de pagos y webhook Wompi.
- [x] Ejecutarlos sobre bases efímeras GUID y eliminar únicamente las bases creadas por cada gate.
- [x] Cubrir aislamiento de claves DTE por empresa, correlativos concurrentes y transición fiscal atómica.
- [x] Cubrir deduplicación de webhooks, aplicación idempotente de pagos y rollback transaccional.
- [x] Emitir evidencia JSON saneada por gate y conservarla como artefacto de CI.
- [x] Ejecutar localmente 3 gates y 140 checks sobre SQL Server real con datos sintéticos y limpieza verificada.
- [ ] Ejecutar el conjunto portable sobre SQL Server 2022 en GitHub cuando existan runners disponibles.

## Sprint actual: Disaster Recovery

- [x] Distinguir el manifiesto lógico del backup físico recuperable.
- [x] Automatizar FULL, DIFFERENTIAL y LOG con CHECKSUM, compresión compatible por edición y RESTORE VERIFYONLY.
- [x] Generar SHA-256 y manifest de evidencia saneada.
- [x] Copiar opcionalmente a segundo disco/NAS mediante publicación atómica y hash verificado.
- [x] Automatizar un restore aislado que rechaza sobrescrituras, ejecuta DBCC CHECKDB y limpia su base sintética.
- [x] Consolidar el procedimiento en [DISASTER-RECOVERY.md](DISASTER-RECOVERY.md).
- [x] Ejecutar un FULL y restore drill sintéticos sobre SQL Server real: 3,236,352 bytes, una tabla recuperada y limpieza verificada.
- [x] Recuperar un secreto sintético desde un key ring copiado y el mismo certificado: 28/28 pruebas DR/DataProtection aprobadas.
- [ ] Ejecutar smoke de API, Web, Worker, login, tenant, inventario, PDF y DTE en MH PRUEBAS.
- [ ] Configurar la programación y segunda ubicación definitivas; medir RPO/RTO productivos.

## Gates de salida

La lista normativa vive en [RELEASE.md](RELEASE.md). Ninguna casilla operativa se marca basándose solo en documentación o mocks: debe existir evidencia reproducible y saneada.
