# GL1D — Correcciones, auditoría SaaS, ciberseguridad y ramas

Fecha: 2026-09-04. Rama de trabajo: `codex/gl0a-auth-security` sobre `a8c5d16`, más cambios locales. **Revisión GL1D cerrada en su alcance de envío individual; NO-GO para producción.**

Este informe continúa la [revisión multiagente anterior](Revision-Multiagente-2026-09-04.md). Se mantiene el diagnóstico NO-GO hasta contar con correcciones y evidencia suficiente. Los resultados anteriores no se atribuyen automáticamente al código que se está modificando durante GL1D.

## Resultado ejecutivo

- Envío individual: corregidos y probados B1/B2/B5 en el alcance descrito; **1,349 pruebas ordinarias aprobadas** y **59 comprobaciones SQL aprobadas** con cadena completa de migraciones. Las 58 comprobaciones EnsureCreated son una repetición del alcance, no 58 garantías adicionales independientes.
- SaaS/ciberseguridad: **cuatro caracterizaciones aprobadas confirman defectos abiertos**. No se suman como aceptación. Bootstrap, permisos/licencia, checkout y otros hallazgos estáticos también requieren trabajo.
- Empresa propia: preflight real de solo lectura y desafío criptográfico RS512 local satisfactorios. **No se emitió un DTE real, autenticó ante MH ni transmitió a Hacienda.**
- Continúan abiertos lotes sin claim coordinado, conciliación remota, recuperación del receptor, guía/acciones Web y riesgos SaaS/seguridad. No se activó producción, no se migró la base real ni se limpió información del usuario.

## Alcance y responsabilidades

- Coordinación/código: correcciones del flujo fiscal B1, B2 y B5.
- Auditoría de ramas: referencias locales/remotas, cambios sin commit, migraciones e integración; solo lectura.
- Auditoría SaaS: aislamiento de empresas, suscripciones, planes, permisos y facturación comercial.
- Ciberseguridad: revisión independiente de superficies de seguridad y límites de las pruebas.
- Documentación: consolidación de evidencia, estado y pendientes; no reemplaza trabajo de producto ni pruebas.

Los roles se ejecutan por tandas y algunos agentes se reutilizan al terminar su tarea anterior. Se utilizan las guías `neostp-context` para preservar empresa, módulos, permisos y trazabilidad; la separación API/Web y los riesgos de migración al arrancar condicionan las pruebas. Manuel mantiene la APK. El incidente de DTE11 comunicado como resuelto por el usuario permanece cerrado.

## Estados de evidencia

| Estado | Significado |
| --- | --- |
| Pendiente | No hay corrección o verificación suficiente. |
| En implementación | Se está modificando código; todavía no se declara corregido. |
| Implementado, no probado | El cambio existe pero falta evidencia ejecutada sobre esa versión. |
| Probado en alcance acotado | Hay pruebas ejecutadas y límites explícitos; no implica certificación integral. |
| Confirmado estáticamente | Se identificó una ruta de código, sin reproducción de extremo a extremo. |

Una prueba `AuditKnownDefect` verde significa que reprodujo el defecto esperado. Para cerrar un hallazgo debe pasar a exigir comportamiento seguro, y ejecutarse sobre el cambio correspondiente. No se suman esas caracterizaciones como garantías de aceptación.

## Matriz GL1D

| ID | Riesgo / objetivo | Estado del corte documental | Evidencia requerida para cerrar |
| --- | --- | --- | --- |
| B1 | Dos transmisiones concurrentes y degradación tardía de PROCESADO. | Corregido y probado en envío individual: tokens Estado/EnviadoAt/GeneradoAt, unitarias y SQL con dos contextos desde FIRMADO. No cubre claim de lotes. | Mantener regresiones y completar E2E HTTP del despliegue; coordinar lotes en B6. |
| B2 | Invalidación local de un envío de resultado incierto. | Corregido y probado en unitarias/SQL: bloqueo, estado/consulta conservados y ausencia de webhook falso. | Mantener regresiones y completar ruta HTTP con roles reales; conciliación remota sigue pendiente. |
| B5 | Reintentos HTTP automáticos del mismo POST fiscal. | Implementado para recepción, eventos, contingencia y lotes. 15 casos aprobados: tres de recepción y doce de los cuatro registros HTTP ante 503, red y timeout Polly. | Mantener esta cobertura y comprobar persistencia/recuperación del resultado incierto, sin inferir entrega real a MH. |
| B6 | Envío de lotes sin reclamar estados individuales ni coordinarse con envío directo. | Hallazgo estático abierto de revisión independiente; no corregido por B1. | Filtrar/reclamar documentos elegibles y probar colisión lote/lote e individual/lote en SQL. |
| B3 | Corrección del Cliente no actualiza explícitamente el receptor histórico del DTE rechazado. | Pendiente. | Operación autorizada y auditada sobre documentos recuperables; prohibición de mutar silenciosamente PROCESADO. |
| B4 | SuperAdmin sin empresa de soporte en diagnóstico API. | Pendiente. | Empresa explícita, autorización y pruebas negativas entre empresas. |
| D1/D2/D5 | Guía Web, acciones por permiso y etapas basadas en evidencia. | Pendientes de esta iteración. | Razor/navegador y HTTP real con roles; no atribuir resultados anteriores al nuevo código. |
| D3/D4 | Búsqueda de retorno limitada a 200 y asociación de etiquetas. | Pendientes. | Búsqueda servidor/paginación y foco/accesibilidad de controles. |
| SAAS-01 | Posible aprobación de transferencias entre empresas por ADMIN. | Confirmado estáticamente por auditor SaaS; no corregido. | Aislamiento HTTP/servicio con dos empresas, denegación sin cambio de pago/suscripción y rol central explícito cuando corresponda. |
| SAAS-02 | Cambio de plan durante trial puede eliminar caducidad. | Defecto reproducido mediante caracterización InMemory; no corregido. | Trial con fin → cambio de plan mantiene vigencia aplicable; no se activa indefinidamente por ausencia de proveedor externo. |
| SAAS-03–07 | Módulos tras downgrade, pagos externos, cancelación, permisos y GET con mutaciones. | Confirmados estáticamente; no corregidos en el bloque fiscal. | Pruebas de licencia efectiva, webhook autenticado, dos empresas/roles y operaciones sin efectos en GET. |
| SEC-01–04 | SSRF en webhooks, bootstrap inseguro, cookies SSO y contenido de logo no validado. | SSRF/SSO/logo reproducidos con caracterizaciones aisladas; bootstrap conserva evidencia estática. Ninguno corregido en GL1D fiscal. | Corregir y convertir caracterizaciones en regresiones seguras; separar pruebas sintéticas de una explotación real. |

No se declara cerrada ninguna fila por el solo hecho de proponer una solución. La conciliación remota autenticada de un envío incierto sigue siendo un trabajo distinto del bloqueo preventivo.

### Primer resultado ejecutado de GL1D

- Coordinación compiló `NeoSTP.slnx` en Release: código 0, 0 errores, una advertencia preexistente CS8604 en `LotesInventarioTests.cs:137`.
- `tmp/gl1d/gl1d-focused.trx`: **5 casos ejecutados, 5 aprobados, 0 fallidos/omitidos**, contadores leídos por documentación.
- Separación correcta: **4 regresiones de comportamiento seguro B1/B2/B5 aprobadas** y **1 caracterización SaaS que confirma un defecto aún abierto**. No presentar las cinco como aceptación del producto.
- En esta primera tanda, B1/B2 usaron el servicio real con InMemory y colaboradores sustituidos; la fixture base era ENVIADO sin fecha de envío, no el recorrido completo desde FIRMADO. La regresión concurrente verificó una llamada al transporte, conflicto para el perdedor y estado PROCESADO con sello/fecha conservados. La evidencia SQL posterior se describe en el cierre.
- En esta primera tanda, B5 empleó la factory/registro reales y transporte primario sustituido: un POST de recepción ante HTTP 503 y excepción de red. No abrió sockets ni envió a Hacienda. Los otros clientes se añadieron a la cobertura posterior.
- Las pruebas de seguridad llegaron después de esa compilación y no están incluidas en ese TRX.

### Ampliación de evidencia y ajuste del diseño

- Aceptación posterior, excluyendo `Category=AuditKnownDefect`: **1,339 unitarias + 9 de integración = 1,348 aprobadas**, 0 fallidas/omitidas. Contadores verificados en `tmp/gl1d/gl1d-acceptance_net10.0_20260904095420.trx` (unitarias) y `gl1d-acceptance_net10.0_20260904095401.trx` (integración).
- Esta ejecución **precede la última guarda sobre Validar/FIRMADO** y no se usó como aceptación final de ese cambio: fue reemplazada por la ejecución de 1,349 pruebas detallada abajo. Las nueve integraciones habituales siguen sin ser una ejecución de hosts/SQL reales.
- `HaciendaResilienceAuditTests` aporta 15 casos aprobados: recepción real con tres tipos de fallo y cuatro clientes nombrados por tres fallos. Los tipos son HTTP 503, excepción de red y `TimeoutRejectedException` de Polly. Factory/políticas reales con transporte primario interceptado; sin sockets ni llamadas a Hacienda.
- Los tokens definitivos son **Estado, EnviadoAt y GeneradoAt**. Se abandonó UpdatedAt para que guardar una nota interna durante el HTTP no impida conservar el acuse PROCESADO. La revisión añadió la prueba de nota concurrente.
- `Firmar` sobre FIRMADO conserva el JWS de forma idempotente, sin refirmar. La última guarda impide que `Validar` rebaje FIRMADO y abra un ciclo de regreso al mismo estado (ABA) que eluda el control de la generación firmada. La aceptación final incluye esta guarda.
- La revisión independiente mantuvo **B6 abierto**: `ContingenciaLoteService.cs:210` carga documentos sin filtrar su estado; `:282` guarda el lote y `:286` transmite sin reclamar los estados DTE. No se comprobó por transmisión real. El claim individual no demuestra seguridad integral del pipeline fiscal ni exclusión individual/lote.
- Los primeros intentos SQL se detuvieron en una fixture GL1C incompatible antes de los nuevos casos GL1D. Se corrigió únicamente la fixture sintética de establecimiento, de tipo 01 con control M001 a tipo 02/casa matriz. Los logs de esos intentos se conservaron; no se presentaron como aceptación. Las repeticiones finales se describen a continuación y no afectaron una base del cliente.

### Cierre ejecutado sobre el código final

**Aceptación ordinaria:** `dotnet test NeoSTP.slnx -c Release --no-restore`, con `Category!=AuditKnownDefect`, compiló y terminó con código 0: **1,340 unitarias + 9 de integración = 1,349 aprobadas**, 0 fallidas/omitidas. Incluye la guarda final anti-ABA Validar/FIRMADO. Documentación verificó contadores en:

- `tmp/gl1d/gl1d-final-acceptance_net10.0_20260904100147.trx` — unitarias.
- `tmp/gl1d/gl1d-final-acceptance_net10.0_20260904100118.trx` — integración.

**SQL real aislado:** el agente de pruebas finalizó `tools/DteSqlVerification` con **58/58 PASS en EnsureCreated** y **59/59 PASS con `--migration-chain`**, código 0. La segunda ejecución incluye **32 comprobaciones anteriores y 27 de GL1D**. Coordinación y documentación verificaron el log final, incluidas cero líneas `FAIL:`.

- Evidencia: `tmp/gl1d/sql/sql-gl1d-ensurecreated.log` y `tmp/gl1d/sql/sql-gl1d-migration-chain-final.log`.
- Seis fixtures sintéticas completan BORRADOR → GENERADO → VALIDADO → FIRMADO con servicio/generador reales, persistencia SQL y dispatcher real que encola webhooks. Firmador, autenticación y recepción están sustituidos; no se usan certificados reales, red, correos ni Hacienda.
- Las carreras cubren dos emisores sobre un FIRMADO, invalidación obsoleta después de PROCESADO, invalidación que gana antes del envío, invalidación durante HTTP más nota concurrente, rechazo tardío después de PROCESADO escrito por otro contexto y timeout seguido de invalidación rechazada.
- Se comprueban un único transporte en la carrera de envío, payload/UUID conservados, sello/fecha/estado terminal, un solo webhook persistido sin delivery y rollback de JSON/historia de error cuando un guardado obsoleto entra en conflicto.
- No son pruebas de aceptación por Hacienda ni pruebas HTTP de autenticación/roles del despliegue. Tampoco cubren el claim de lotes abierto en B6.

**Limpieza acotada:** se eliminaron las cuatro bases sintéticas creadas por los ensayos. `tmp/gl1d/sql/sql-cleanup-verification.log` verifica cero remanentes entre esos cuatro nombres exactos; no se inspeccionaron ni eliminaron otras bases. `localdb-final-status.log` confirma la instancia de pruebas `NeoStpAuthAudit_20260904` detenida. Los logs se conservaron; no se borró información del usuario.

**Identidad de código:** hash SHA-256 de `DteDocumentosService.cs`, coincidente entre aceptación, agente SQL, coordinación y lectura final de documentación: `4E06B7D86E5D22C43473BC7200CAE5AD7F68DEB78551FE80FA12B2B235E51CFC`. El agente SQL registró para `tools/DteSqlVerification/Program.cs`: `304E2DDCFCC2821A5B98DFEECFAF98A43AB7AB0850E2A67ADD79AE5DDCC9A900`.

El enfoque HTTP utiliza la posibilidad de desactivar retries de métodos no seguros que documenta Microsoft; el manejador estándar reintenta todos los métodos por defecto. [Resiliencia HTTP en .NET](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience).

La concurrencia optimista compara el valor original de los tokens al guardar y comunica el conflicto a la aplicación. No evita por sí sola rutas que no participan en el protocolo ni hace segura una retransmisión fiscal. [Conflictos de concurrencia en EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

## Auditoría de ramas e integración

### Corte verificado

Inventario: **2026-09-04 15:38 UTC**. Referencias remotas comprobadas con `git ls-remote --heads origin`, sin fetch ni modificación de Git. Los archivos que coordinación modifique después del corte no se incluyen en estos conteos.

| Referencia | Commit | Relación con la rama actual |
| --- | --- | --- |
| `codex/gl0a-auth-security` | `a8c5d16` | Actual, sin upstream y ausente en origin. |
| `main`, `origin/main`, main remoto consultado | `a8c5d16` | Coinciden, divergencia 0/0. |
| `claude/focused-wu-04fc99` | `7abb331` | Ancestro, 10 commits detrás; ocupado en otro worktree. |
| `feature/sprint8-dashboard` / origin | `69d2b26` | Ancestro, 189 commits detrás. |
| `origin/feature/sprint5-dte-documentos` | `5a5657a` | Divergencia 195/1; exclusivo remoto es un merge histórico. |
| `origin/feature/sprint6-firma-transmision` | `569357c` | Divergencia 194/1; exclusivo remoto es un merge histórico. |

Rama principal en el corte: **72 archivos versionados modificados, 106 archivos no versionados, 0 staged**. No existen commits GL0A–GL1C en esta rama: los avances permanecen en el árbol de trabajo. Un despliegue desde origin no los incluye. No había etiquetas locales de release; no se inspeccionaron protecciones de rama ni ejecuciones remotas de Actions.

### R1 — P1: preservar avances y preparar un release reproducible

No hacer checkout, reset, limpieza o integración masiva mientras continúan los escritores. El nombre de una rama no demuestra que el trabajo esté guardado o publicado. Preparar un inventario final tras detener temporalmente los cambios y commits coherentes con sus pruebas.

### R2 — P1: código y migraciones deben viajar juntos

En el corte estaban sin seguimiento cuatro migraciones y sus designers:

- `20260904021242_GL0A_RefreshSessionContext`
- `20260904030641_GL0B_AuthSessionFoundation`
- `20260904125956_GL0C_SsoIdentityMfaConcurrency`
- `20260904140909_GL1B_DteIdempotency`

La auditoría inicial de Git no determinaba si estaban aplicadas a una base concreta. Posteriormente, coordinación consultó la base real en modo solo lectura: la última migración registrada es CAT020 (`20260825233802`) y faltan las cuatro GL0/GL1 enumeradas. Web (`Program.cs:125`) y API (`Program.cs:103`) invocan el seeder; `DatabaseSeeder.cs:27` ejecuta `MigrateAsync`. Arrancar esos hosts puede cambiar el esquema destino. No se aplicaron migraciones en ese preflight.

Después del corte inicial se generó offline `20260904154619_GL1D_DteFiscalConcurrency`. Documentación comprobó que `Up` y `Down` están vacíos: registra metadatos de los tokens de concurrencia sin añadir columnas. Tampoco se aplicó a la base del usuario. Debe conservarse con su designer/snapshot para reproducir el modelo.

GL0C sustituye el índice único SSO por proveedor/emisor/sujeto. Su reversión recrea el índice anterior sin emisor y podría fallar si existen sujetos iguales con emisores distintos después de la migración. No asumir rollback automático seguro. Revisar respaldo/restauración, esquema y datos antes de aplicar cambios.

### R3 — P2: worktree antiguo con solapes

El worktree `claude/focused-wu-04fc99` contiene 11 modificaciones y dos migraciones no versionadas. Ocho de los 13 archivos son iguales al contenido confirmado en main, normalizando finales de línea; incluyen snapshot y ambos archivos CAT020. Cinco difieren:

- `ClienteValidator.cs`
- `ClientesService.cs`
- `DteDocumentosService.cs`
- `DteDocumentosService.Eventos.cs`
- `DteEventosController.cs`

No importar todo el worktree ni borrarlo automáticamente. Comparar esos cinco archivos y conservar solo cambios deliberados: una copia completa puede sobreescribir hotfixes y GL0/GL1. No se modificó ese worktree.

### R4 — P1 para release: CI no ejecuta la validación SQL/navegador

`.github/workflows/ci.yml:7–10` se activa con push a main o PR dirigido a main. Publicar esta rama sin PR no dispara ese workflow. El comando de `ci.yml:34` cubre solo `NeoSTP.slnx`; `AuthSqlVerification`, `DteSqlVerification` y `WebAuditPreview` no forman parte de la solución. Hace falta incorporar puertas específicas o evidencia reproducible exigida antes del release.

### R5 — P2: ZAP no demuestra una puerta de seguridad operativa

El workflow `.github/workflows/zap-baseline.yml` no aprovisiona SQL ni especifica una conexión efímera, aunque el comentario lo da por supuesto. El host migra al arrancar; el bucle de espera no falla explícitamente cuando `health` nunca responde (`:46–49`) y el escaneo declara `fail_action: false` (`:56`). Son observaciones estáticas: no se ejecutó ese workflow en esta auditoría.

### R6 — P2: revisar evidencia y secretos antes de publicar

Existe una captura del cliente ya versionada bajo `.codex-remote-attachments`. El inventario por nombres no mostró certificados, claves privadas ni `appsettings.Local.json` versionados. No equivale a escanear contenido o historial completo. Revisar los nuevos archivos de evidencia antes de incluirlos en commits; no publicar datos fiscales, credenciales, certificados privados ni tokens.

### Secuencia segura de integración

1. Terminar correcciones y congelar temporalmente escritores; registrar el diff final.
2. Resolver los cinco archivos divergentes del worktree antiguo, preservando su contenido.
3. Revisar nuevos archivos y preparar commits coherentes de seguridad, contexto fiscal/idempotencia, recuperación/concurrencia y superficies/pruebas; mantener cada conjunto compilable.
4. Incluir migraciones/designers/snapshot y verificar el esquema en una copia aislada.
5. Ejecutar aceptación, regresiones, SQL y navegador sobre el mismo commit.
6. Publicar rama y PR a main con las puertas correspondientes; no mezclar aprobación del código con activación fiscal.
7. Etiquetar el commit realmente validado y desplegar primero a pruebas con respaldo y recuperación comprobados.

Esta auditoría no ejecutó cambios Git, builds, migraciones ni eliminaciones. `git diff --check` no mostró errores de whitespace en el corte revisado.

## Auditoría SaaS — inspección de código completada

Los siguientes hallazgos proceden de inspección estática del auditor SaaS. No se ejecutaron pagos externos, cambios de suscripción sobre empresas reales ni pruebas HTTP de dos tenants durante esa inspección. Coordinación ejecutó posteriormente la caracterización trial en InMemory y reprodujo SAAS-02; los demás mantienen evidencia estática.

- **SAAS-01 / P1:** `BillingController.cs:134–148`, combinado con `EsAdmin` (`:234–235`), permite una ruta de listado/aprobación de transferencias para ADMIN. `BillingService.cs:275–292` resuelve el pago por ID sin EmpresaId. `DemoComercialSeeder.cs:244,249` usa ADMIN como rol de empresa, no exclusivamente operador central. Falta demostrar denegación entre empresas por HTTP y servicio.
- **SAAS-02 / P1:** `BillingService.cs:58,62` crea trial con vencimiento; cambiar plan puede omitir proveedor cuando no hay `ExternalSubscriptionId` (`:123`), activar con `CurrentPeriodEnd=null` (`:134`) y persistir `FechaFin=fin` (`:397`). `SaasAuditTests.KnownDefect_ChangingTrialPlanRemovesExpiryAndLeavesUnpaidLicenseValid` reprodujo en InMemory una licencia sin vencimiento y sin pagos: 1/1 en `tmp/gl1d/gl1d-focused.trx`. Verde confirma el defecto, no su corrección.
- **SAAS-03 / P1:** un downgrade no revoca módulos anteriores: `BillingService.cs:423` activa los del nuevo plan pero no desactiva los que sobran; `EmpresasService.cs:323–331` conserva `Activo=em.Activo` aun con `IncluidoEnPlan=false`. `RequireModuloAttribute.cs:45–46` y `ModuloRequirement.cs:38–42` comprueban activo, no inclusión en plan. Se requiere probar la licencia efectiva, no solo la presentación de planes.
- **SAAS-04 / P1:** checkout externo incompleto. `WompiBillingProvider.cs:58` y `PayPalBillingProvider.cs:62` construyen importes cero; `BillingService.cs:77–95` devuelve la sesión sin persistir la correlación `ExternalSubscriptionId`. En `src/NeoSTP.Infrastructure/Billing/BillingWebhookHandler.cs`, PayPal (`:95`) y Wompi (`:141`) buscan esa correlación y pueden retornar sin encontrarla; `HandleAsync` (`:57`) marca el evento procesado. El handler actualiza `Status`, pero no actualiza `EmpresaPlan`, `CurrentPeriodEnd` ni módulos. No se ha demostrado un pago real ni una licencia activada por esos flujos.
- **SAAS-05 / P2:** cancelar no corta la licencia efectiva: `BillingService.cs:161–164` cambia estado/fecha de cancelación, mientras `EmpresasService.cs:335–340` no consulta esa suscripción para la vigencia. Requiere fijar y probar la política de cancelación inmediata versus fin del período.
- **SAAS-06 / P2:** el controller Billing tiene `[Authorize]` general, pero trial/cambio/cancelación (`BillingController.cs:65,202,217`) no exigen un permiso específico o rol financiero. Un usuario autenticado de empresa puede modificar la suscripción sin separación de funciones.
- **SAAS-07 / P2:** GET `/billing/transferencia` (`BillingController.cs:105–111`) llama una operación que cambia `PlanId` y añade un pago (`BillingService.cs:230–258`). Cada visita puede producir efectos; no es una operación de lectura ni dispone de idempotencia/antiforgery para esa mutación.

### Mejoras solicitadas y alcance actualmente disponible

| Necesidad | Estado observado | Pendiente |
| --- | --- | --- |
| Tipos de DTE por plan/empresa | No implementado en la ruta inspeccionada: `Plan.cs:7–20` ofrece límites/módulos; `DteDocumentosService.cs:31,410` usa tipos globales y cuota de cantidad. | Configuración y validación compartida API/Web por tipo y empresa, con pruebas negativas. |
| Pausar/reanudar sin costo | Sin operaciones ni estado de pausa en `IBillingService.cs:8–24` y `SubscriptionStatus` (`:34–41`); no hay API de autoservicio Billing, solo webhook. | Definir la política y exponer contrato API autorizado para web y aplicación. |
| QR para cobro del negocio | `CobroQrService.cs:80–138` valida pertenencia, saldo y genera PNG/URL desde plantilla. | No crea por sí solo una transacción de pasarela ni concilia un pago autenticado. No anunciar cobro integrado completo. |
| Cuotas API bajo concurrencia | `ApiQuotaMiddleware.cs:61,89–96` comprueba conteo antes y registra después. | Reserva atómica/concurrencia para impedir excedentes. Esta observación no se extiende al límite mensual DTE, que tiene una ruta Serializable con sp_getapplock y requiere análisis propio. |

Las pruebas SaaS existentes cubren adaptadores HTTP simulados y transferencia secuencial. No acreditan autorización HTTP entre empresas, trial/downgrade, webhook más licencia ni concurrencia SQL.

## Ciberseguridad

Inspección estática completada por el agente asignado. `tests/NeoSTP.Tests.Unit/Auth/SecurityAuditTests.cs` aporta tres casos `AuditKnownDefect` y un control. El control de política SameSite Unspecified se incluyó en las 1,339 unitarias aprobadas.

Coordinación ejecutó después `Category=AuditKnownDefect`: **4/4 aprobadas** en `tmp/gl1d/gl1d-known-defects.trx`, contadores y nombres verificados por documentación. Son las tres caracterizaciones de seguridad y la del trial SaaS, **no cuatro correcciones ni cuatro nuevas garantías**. No se afirma cobertura integral OWASP, pentest, explotación real ni ausencia de vulnerabilidades.

- **SEC-01 / P1 — SSRF en webhooks:** `ConnectWebhookService.cs:52` valida esquema HTTP/S, pero los envíos (`:222,230`) no restringen IP destino/redirecciones. `ConnectWebhookDispatcher.cs:171,179` mantiene el mismo patrón. `ConnectController.cs:80,101` permite crear/probar webhooks desde la superficie autorizada de la empresa. `WebhookAcceptsLoopbackAndDispatchesToInterceptOnlyHandler` confirmó que se acepta una URL loopback y se entrega al handler interceptado. No abrió un socket ni exploró redes privadas; la reproducción prueba la falta de barrera en esa ruta, no una intrusión real.
- **SEC-02 / P1 — bootstrap y contraseña en logs:** `DatabaseSeeder.cs:47,68–69` usa la contraseña de opciones y la registra en claro; `SuperAdminOptions.cs` tiene un valor predeterminado conocido. `ProductionGuards.cs:14,30` controla mocks, no credenciales bootstrap. El auditor no leyó secretos ni logs reales. El hallazgo se sustenta en el código, no en una credencial de una instalación concreta.
- **SEC-03 / P2 — política SameSite incompatible con ciertos callbacks SSO:** `src/NeoSTP.Web/Program.cs:84,140` combina mínimo Lax con `UseCookiePolicy`. `GlobalLaxPolicyChangesOidcNonceAndCorrelationToLax` confirmó con configuración OIDC real y middleware aislado que las cookies solicitadas None quedan Lax para `form_post`. La [documentación oficial ASP.NET Core sobre SameSite](https://learn.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-10.0) explica las excepciones OIDC. No se probó un login ni un callback real del proveedor; el control Unspecified conserva None.
- **SEC-04 / P2 — logo no decodificado antes de persistir/usar:** `BrandingService.cs:85–96` valida tamaño y MIME declarado, pero no contenido de imagen; `DtePdfService.cs:37,86,110` entrega el blob a `.Image` sin fallback. `BrandingAcceptsNonImageBytesAndBreaksOtherwiseValidPdf` generó correctamente el PDF sintético sin logo y confirmó excepción después de aceptar texto declarado `image/png`. No demuestra ejecución de código ni afecta un documento real.

Estas pruebas de caracterización confirmaron condiciones inseguras; no constituyen correcciones. Las observaciones de ZAP/CI de la sección anterior son una revisión de integración separada.

## Pruebas más reales con la empresa del usuario

El usuario autorizó ampliar las pruebas con su propia empresa, que todavía no ha activado producción. Esta autorización no cambia el ambiente fiscal ni autoriza limpieza de datos, reinicio de IDs/correlativos o pruebas sobre otras empresas.

Condiciones antes de cualquier envío real a apitest:

1. Identificar de forma inequívoca la empresa propia y su configuración persistida. No inferir identidad solo por un nombre, una captura o un certificado adjunto.
2. Confirmar ambiente PRUEBAS en empresa, DTE, rutas HTTP, credenciales y firmador de cada host. Web firma con sus propios servicios; no delega el envío a la API.
3. Validar correspondencia y vigencia del certificado sin imprimir claves, contraseñas ni material privado; no copiar ese material al repositorio.
4. Revisar esquema y respaldo antes de arrancar un host que pueda migrar/sembrar. No modificar otras empresas por efectos del arranque.
5. Elegir un escenario controlado con identidad propia, receptor y contenido de prueba autorizados; conservar numeroControl/codigoGeneracion/respuesta/sello para trazabilidad.
6. Ante respuesta incierta, detener nuevos intentos fiscales del mismo documento hasta conciliación; nunca interpretar timeout como rechazo definitivo ni declarar PROCESADO sin evidencia.

### Preflight real confirmado, solo lectura

Coordinación identificó **EmpresaId 2, NEO SOFTWARE TECH PRO**, como empresa propia. Comprobó correspondencia del identificador con el certificado aportado, configuración DTE **PRUEBAS**, certificado presente y contraseña Hacienda cifrada presente. Consultó además el historial de migraciones real: faltan las cuatro GL0/GL1 descritas arriba. No se arrancó un host ni se modificaron registros durante estas comprobaciones.

La consulta inicial demostró presencia/configuración almacenada, no autenticación ni aceptación por Hacienda. No se incluyen NIT, bytes de certificado, contraseñas ni material privado en este informe.

Coordinación ejecutó después `tools/CompanyPreflight/Test-CompanyDteReadOnly.ps1`, con **seis comprobaciones booleanas verdaderas**: formato de actividad, departamento y municipio, presencia de correo y teléfono y correspondencia criptográfica del par real mediante un desafío aleatorio RS512 firmado/verificado **localmente**. Identidad y PRUEBAS son guardas previas adicionales.

El desafío no es un JSON DTE ni una firma de documento fiscal. **No hubo autenticación MH, transmisión, creación de DTE, escritura SQL ni exposición de secretos. Tampoco se validaron vigencia/revocación del certificado.** Esta evidencia mejora el preflight técnico, pero no equivale a una factura aceptada ni al cumplimiento tributario de todos los datos.

Los selectores locales revisados de API y Web son `Hacienda:Client=Http`, `Dte:Signer=HaciendaCert` y `Email:Provider=Smtp`. La ausencia de esos selectores en el archivo local de Worker no prueba que su configuración efectiva sea Mock: falta revisar la configuración combinada. Antes de un ensayo completo debe controlarse también el envío de correo, pues tener SMTP seleccionado no equivale a un destinatario de prueba seguro.

Hasta este corte documental **no hay resultado comunicado de una prueba real GL1D en apitest ni de una emisión con la empresa del usuario**. Los antecedentes de certificación de otras rondas/empresas no certifican esta configuración. No se activa producción y no se toca la APK.

## Estado final de la validación

| Evidencia | Estado |
| --- | --- |
| Build Release del cambio GL1D | Código 0; 0 errores, 1 advertencia preexistente CS8604. |
| Regresiones B1/B2/B5 convertidas a comportamiento seguro | Aprobadas en aceptación final; HTTP 15/15 y guarda Validar/FIRMADO incluidos. |
| Concurrencia SQL real | 58/58 EnsureCreated y 59/59 cadena de migraciones; 27 checks GL1D. Solo fixtures sintéticas. Cuatro bases de ensayo eliminadas e instancia detenida. |
| Suite ordinaria final | 1,340 unitarias + 9 integración = 1,349 aprobadas; 0 fallidas/omitidas. |
| Caracterizaciones SaaS | 1/1 confirma defecto trial sin vencimiento; repetido dentro de `gl1d-known-defects.trx`, separado de aceptación. |
| Revisión ciberseguridad | Tres defectos reproducidos y uno estático; control SameSite aprobado. Los cuatro casos conocidos SaaS/seguridad están verdes porque los defectos siguen presentes. |
| Preflight de la empresa real | EmpresaId 2, PRUEBAS; seis comprobaciones verdaderas y desafío RS512 local. Sin autenticación/envío MH, vigencia/revocación ni escrituras. Cuatro migraciones GL0/GL1 faltantes en la consulta; la quinta GL1D posterior tampoco se aplicó. |
| Envío real de la empresa a apitest | Autorizado en alcance de pruebas, no ejecutado/confirmado en este corte. |
| Commit, push, despliegue y producción | No realizados por el agente de documentación ni de ramas. |

La documentación registra lo observado y distingue alcance y pendientes. No sustituye una corrección, una prueba de extremo a extremo ni una aceptación de producción.

**Dictamen final:** GL1D de envío individual queda probado en los alcances unitario/SQL descritos. **El sistema no queda habilitado para producción**: persisten SaaS/seguridad, lotes sin claim coordinado, conciliación remota y pendientes de Web/recuperación. El siguiente pase debe priorizar esos bloqueos y el ensayo fiscal controlado en PRUEBAS, sin confundir preflight criptográfico con recepción de un DTE por Hacienda.
