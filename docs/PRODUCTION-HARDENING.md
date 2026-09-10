# NeoSTP v1.0 Production Hardening

Estado operativo del cierre v1.0. Este documento es la fuente pública de seguimiento; no debe contener datos, credenciales ni evidencia de instalaciones reales.

## Alcance congelado

Desde el 10 de septiembre de 2026 el objetivo es estabilizar y liberar la plataforma existente. No se agregan módulos generales ni se inicia NeoTaller hasta cerrar los gates P0, Notification Outbox, la semántica de precio/IVA y un STAGING operativo.

La base de trabajo es `main`. Las ramas privadas o de soporte a clientes no se fusionan en `main`; únicamente se trasladan cambios comunes, revisados y libres de datos particulares.

## Baseline confirmado

- Build Release: 0 errores y 0 advertencias.
- Pruebas locales: 2,401 unitarias y 9 de integración aprobadas.
- Higiene del árbol público: 1,450 archivos rastreados aprobados por `Check-PublicTree.ps1`.
- GitHub Secret Scanning: 0 alertas abiertas al inicio del cierre.
- CI y ZAP no llegan a ejecutar steps: GitHub reporta la cuenta bloqueada por un problema de facturación.
- `main` no tenía protección de rama al inicio del cierre.

## Orden de ejecución

| Fase | Entregable verificable | Estado |
|---|---|---|
| 0. Freeze | Alcance, baseline y gates versionados | En curso |
| 1. Seguridad/Git | Escaneo actual e histórico, rotaciones, CI/ZAP, `main` protegida | En curso |
| 2. Ambientes | Matriz STAGING/PROD, configuración, DNS, TLS, secretos, storage y DataProtection separados | Pendiente |
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
- [x] Ejecutar build y suites locales.
- [x] Ejecutar higiene del árbol público.
- [x] Consultar alertas abiertas de GitHub Secret Scanning.
- [x] Diagnosticar el fallo temprano de CI y ZAP.
- [ ] Resolver el bloqueo de facturación de GitHub Actions (acción del propietario de la cuenta).
- [ ] Ejecutar Gitleaks sobre todo el historial una vez desbloqueado Actions.
- [ ] Revisar ramas remotas y tags antes de declarar limpio el historial.
- [ ] Rotar e invalidar cualquier credencial histórica confirmada; no registrar valores aquí.
- [ ] Ejecutar CI, SQL Server integration y ZAP en verde.
- [ ] Activar protección de `main`: PR, review, checks, conversación resuelta, sin force-push ni borrado.

## Gates de salida

La lista normativa vive en [RELEASE.md](RELEASE.md). Ninguna casilla operativa se marca basándose solo en documentación o mocks: debe existir evidencia reproducible y saneada.
