# GL1E — Cierre de verificación de seguridad, SaaS y lotes

Fecha: 2026-09-04. Rama `codex/gl0a-auth-security`, HEAD `a8c5d16` más cambios locales preservados. **Verificación acotada GL1E cerrada: 1,545 pruebas de aceptación aprobadas; NO-GO para producción.** No implica que todos los cambios del worktree correspondan a GL1E, estén publicados o desplegados.

## Cambios y revisión independiente

| Área | Implementación GL1E | Evidencia al corte |
| --- | --- | --- |
| Lotes DTE | Reserva durable de documentos y lote antes del POST; competencia con envío individual/otros lotes; respuesta perdida no se retransmite; consultas aplican solo evidencia terminal explícita y control de concurrencia. | Ensayo SQL sintético: 83 comprobaciones aprobadas. |
| Identidad del intento | La consulta antigua no puede mutar un documento regenerado; UUID duplicados se rechazan en ambos órdenes; se guarda la causa de transporte aunque `Raw` sea nulo. | Cinco casos independientes aprobados; regeneración mediante servicio/generador reales y contextos nuevos, con EF InMemory y respuestas sintéticas. |
| SEC01 / webhooks | Solo HTTPS a IP públicas, bloqueo de destinos privados/reservados y respuestas DNS mixtas; conexión a la IP validada, sin proxy, redirecciones ni cookies. Revalidación al entregar y bloqueo de destinos antiguos inseguros. | Primer filtro: 80 casos de destino, cuatro de dispatcher y tres controles compartidos de seguridad/SSO aprobados. Sin conexión TLS/proveedor real. |
| SEC02 / bootstrap | Sin credenciales utilizables por defecto ni contraseña en logs; valida credenciales explícitas solo al crear el primer usuario. El rol debe ser central, de sistema y activo; usuario y asignación se guardan juntos. Usuarios existentes no se modifican. | 23 casos aprobados, incluida suplantación por rol tenant y un único SaveChanges. Verificación InMemory de la frontera de guardado, no un fallo transaccional SQL inducido. |
| SEC03 / cookies SSO | Política global `Unspecified` conserva nonce/correlación `None`; autenticación local `Lax`, antiforgery `Strict`, HTTPS y HttpOnly. | Tres casos dedicados y controles compartidos aprobados. Middleware y opciones reales, sin inicio de sesión con proveedores externos. |
| SaaS | Gestión restringida al ADMIN de la propia empresa o central; cobros globales/confirmación exigen SUPERADMIN central persistido. Trial único y finito al cambiar plan, sin activación offline no pagada. GET de transferencia deja de escribir; POST exige antiforgery. Pago pendiente bloquea cambios incompatibles; confirmación verifica importe, moneda, plan activo y unicidad. Transacción serializable y bloqueo por empresa sin reintentos automáticos. | 10 casos SaaS, nueve de autorización, 12 HTTP TestServer, seis de transferencia pendiente y seis existentes aprobados en el filtro de 221. SQL: 16/16 comprobaciones aprobadas. No equivale a pasarela de pagos conciliada. |

La migración `20260904173458_GL1E_LoteAttemptConcurrency` registra metadatos de concurrencia. `Up` y `Down` están vacíos; se comprobó dentro de la cadena completa en la base sintética, **no se aplicó a la base del cliente**.

## Verificación final

- Solución completa: **1,536 unitarias + nueve de integración = 1,545 aprobadas**, sin fallos ni omitidas, con `dotnet test NeoSTP.slnx -c Release --no-restore --filter "Category!=AuditKnownDefect"`. TRX leídos independientemente: `tmp/gl1e/gl1e-final-acceptance_net10.0_20260904114542.trx` y `tmp/gl1e/gl1e-final-acceptance_net10.0_20260904114524.trx`.
- Compilación Release de solución, incluidos API/Web/Worker: **cero errores y cero advertencias**, `tmp/gl1e/build-final.log`. Es compilación local, no despliegue.
- Caracterización separada: **1/1 confirma que el defecto de branding/PDF sigue presente**, `tmp/gl1e/gl1e-known-defects.trx`; no forma parte de las 1,545 pruebas de aceptación.

- Primer filtro conjunto: **175/175 aprobadas**, `tmp/gl1e/gl1e-targeted.trx`. Es una ejecución anterior a las últimas ampliaciones de pruebas; no reemplaza la suite final.
- Filtro actualizado: **221/221 aprobadas**, sin fallos ni omitidas, `tmp/gl1e/gl1e-targeted-final.trx`, leído independientemente. Incluye los cinco casos de intento, 23 de bootstrap, tres de política de cookies, 80 de destinos webhook y 30 de transporte de lotes. Repite parte del primer filtro; **no se suman 175 y 221 como casos distintos**.
- SQL DTE: **83/83 comprobaciones aprobadas**, salida 0, `tmp/gl1e/sql-gl1e-migration-chain.log`: 59 comprobaciones heredadas y 24 adicionales del ensayo de lotes. Incluye cadena completa de migraciones, preservación de JWS/UUID, lote contra lote e individual en ambos sentidos, rollback de documentos acompañantes, timeout durable y consulta tardía que no pisa un acuse confirmado. No se suman como pruebas xUnit.
- El ensayo usa SQL real, servicios/generador reales y empresa sintética; autenticación, firma y respuestas de Hacienda son sustitutos. El dispatcher encola evidencia sin entregar HTTP. El log confirma la eliminación de su única base aleatoria `NeoStpDteAudit_feabf7e0de9e486698ae348069d87c43`; no se limpió información del usuario.
- SQL SaaS: **16/16 comprobaciones aprobadas**, `tmp/gl1e/billing-sql.log`, leído independientemente. Dos contextos concurrentes con barreras, sin esperas temporizadas: un único trial/pago pendiente, período finito, conservación de vencimiento al cambiar plan, trial cancelado no reiniciable y confirmación central única. Proveedores/correo/identidades sintéticos; base `BillingAudit_6bc4fc9e9acd4189af453a7647b77fa3` eliminada según el log. Estas comprobaciones tampoco se suman como casos xUnit.
- Limpieza corroborada: cero bases de estos dos ensayos restantes en `tmp/gl1e/sql-cleanup-verification.log`; instancia de ensayo `NeoStpAuthAudit_20260904` detenida en `tmp/gl1e/localdb-final-status.log`. No se eliminaron bases ni datos del usuario.
- Las **1,349 pruebas** y el preflight de la empresa real pertenecen a [GL1D](GL1D-Correcciones-Auditorias-2026-09-04.md), no son nuevas ejecuciones GL1E. No se repitió ese preflight ni se transmitió un documento de la empresa en esta ronda.

## Límites y siguiente paso

Continúa **NO-GO** hasta cerrar los bloqueos aplicables y completar la evidencia de operación real autorizada:

1. Un lote incierto sin código de recepción conserva la reserva y requiere conciliación manual; no hay reenvío ciego ni garantía de recuperación automática del resultado externo.
2. Las notificaciones de webhook posteriores al commit son best-effort; aún no equivalen a un outbox transaccional completo. El endurecimiento SSRF no establece una allowlist de socios y no sustituye controles de salida de red.
3. Siguen abiertos pasarelas/correlación y sincronización de licencia por webhook, revocación efectiva al cancelar, downgrade/addons sin origen, snapshot histórico del plan pagado y coordinación ante fallo externo/commit. Los requisitos de tipos DTE por plan y pausa/reanudación no se dan por implementados mediante esta ronda.
4. No se ejecutaron Hacienda, pasarelas, proveedores SSO ni pruebas end-to-end con TLS externo real. El defecto de branding/PDF y los demás pendientes no corregidos siguen registrados; una prueba de caracterización verde no significa corrección.
5. Sin producción, reinicio de servicios del cliente, limpieza de su base, resecuenciación, commit o push. La APK sigue a cargo de Manuel y DTE11 permanece cerrado por indicación del usuario.

Siguiente paso: priorizar conciliación fiscal y los bloqueos de SaaS/branding pendientes, y después completar pruebas externas autorizadas y su evidencia de recuperación. Este cierre local no autoriza activar producción por el mero resultado favorable de pruebas sintéticas.

Referencias técnicas usadas para el enfoque: [concurrencia de EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [resiliencia HTTP y reintentos](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience) y [SameSite en ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-10.0).
