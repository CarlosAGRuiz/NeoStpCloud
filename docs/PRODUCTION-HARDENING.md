# NeoSTP v1.0 Production Hardening

Estado operativo del cierre v1.0. Este documento es la fuente pública de seguimiento; no debe contener datos, credenciales ni evidencia de instalaciones reales.

## Alcance congelado

Desde el 10 de septiembre de 2026 el objetivo es estabilizar y liberar la plataforma existente. No se agregan módulos generales ni se inicia NeoTaller hasta cerrar los gates P0, Notification Outbox, la semántica de precio/IVA y un STAGING operativo.

La base estable es `main`. Después de activar su protección, todo incremento parte de `main` y regresa mediante PR; las ramas privadas o de soporte a clientes nunca se fusionan completas.

## Baseline confirmado

- Build Release: 0 errores y 0 advertencias.
- Pruebas locales: 2,401 unitarias y 9 de integración aprobadas; el gate adicional de SQL Server real también fue aprobado.
- Higiene del árbol público: 1,455 archivos rastreados aprobados por `Check-PublicTree.ps1`.
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
| 3. Base de datos | SQL Server 2022 efímero, migrations, aislamiento, concurrencia e idempotencia | En curso |
| 4. DR | Backup físico y restauración aislada con RPO/RTO medidos | Pendiente |
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

## Gates de salida

La lista normativa vive en [RELEASE.md](RELEASE.md). Ninguna casilla operativa se marca basándose solo en documentación o mocks: debe existir evidencia reproducible y saneada.
