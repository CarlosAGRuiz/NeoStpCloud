# Informe maestro final — NeoSTP Cloud

> Reauditado el 2026-09-04: este informe conserva el corte histórico GL1G. Ver la [auditoría actualizada](Auditoria-Actualizacion-2026-09-04.md), el [plan de continuidad](Plan-Continuidad-Auditado-2026-09-04.md) y el [catálogo de subagentes](Subagentes-Auditoria-Continuidad.md).

Fecha: **2026-09-04**  
Rama: `codex/gl0a-auth-security`  
HEAD/base: `a8c5d163fb372359c4738f0c3814ef1ee14024eb` (`a8c5d16`)  
Dictamen: **trabajo local ampliamente validado; PRODUCCIÓN global NO-GO**.

Este documento consolida qué se hizo, qué se validó, qué falta, las reglas aplicadas y los subagentes usados.
“Implementado” significa presente en el worktree; “validado” significa probado en el alcance indicado. No
significa publicado, desplegado, migrado en la base activa ni aceptado por Hacienda/proveedores reales.

## 1. Estado de rama y commits

- `codex/gl0a-auth-security` no tenía upstream ni commits propios sobre `main`.
- `HEAD`, `main` y la referencia local `origin/main` estaban en `a8c5d16`; divergencia 0/0.
- GL0A–GL1G permanecen locales: 104 archivos versionados modificados, 3,251 inserciones, 770 eliminaciones,
  numerosos archivos no rastreados y staging vacío al último inventario.
- Un despliegue desde `origin/main` **no contiene** esos cierres.

Commits anteriores relevantes ya presentes en la base:

| Commit | Contenido |
| --- | --- |
| `a8c5d16` | Commit base del corte. |
| `172f13e` | Regeneración de JSON al reintentar envío. |
| `7c55d39` | Etiquetas territoriales del emisor. |
| `0d33b0f` | Saneamiento del emisor y corrección NIT del cliente. |
| `149b8e3` | Catálogo país ISO 3166-1 alfa-2. |
| `f8ca1d5` | Autoarranque local, UI y documentación. |
| `ce8d812` | Retorno limitado a tipos 01/11/14. |
| `188333e` | Validación territorial salvadoreña. |
| `4ba8f6d` | Filtros de tipo y monto DTE. |
| `263af9b` | Corrección del evento de retorno. |

## 2. Resumen de evidencia actual

| Evidencia | Resultado | Límite |
| --- | --- | --- |
| Build Release final | 0 errores / 0 advertencias | Local, no publicación/despliegue. |
| Unitarias | 1,616/1,616 | Fixtures/sustitutos según cada área. |
| Integración | 9/9 | EF InMemory; no SQL/hosts reales. |
| Total xUnit disjunto | 1,625 | Los filtros no se suman otra vez. |
| Billing | 93/93 | Subconjunto focal. |
| GL1G focal | 26/26 | Subconjunto focal. |
| SQL GL1G | 63/63 | LocalDB sintético; base temporal eliminada. |
| API/Web health | HTTP 200 | Binarios anteriores; no prueban GL0A–GL1G. |
| Base cliente | Sin cambios | No migración, limpieza, reseed ni emisión. |
| Externos | No ejecutados | Sin MH/pasarela/SMTP/OIDC real. |

## 3. Solicitudes cubiertas por la campaña

- Retorno y contingencias; datos fiscales; territorio El Salvador/extranjero.
- Autocompletado de cliente, stepper `PROCESADO`, filtros y búsqueda.
- Mensajes Hacienda claros y recuperación sin duplicar DTE.
- Web/API/Worker y autoarranque.
- Auditoría completa, planes de certificación/producción y documentación.
- Visor de contraseña.
- Tipos DTE por plan/empresa, pausa/reanudación y QR/links/pasarelas.
- Seguridad multiempresa, SaaS, ramas, diseño, Web y ciberseguridad.

## 4. Trabajo realizado por bloque

| Bloque | Trabajo realizado | Evidencia del corte | Estado operativo |
| --- | --- | --- | --- |
| Auditoría base | 725 acciones inventariadas; Auth, tenant, DTE, Billing, operación y respaldo revisados; defectos reproducidos. | 1,005 unitarias + 9 integración; 147 rutas 401; `.bak` pasó VERIFYONLY. | Auditoría terminada; remediación inició. |
| GL0A | Cerró escalada `SUPERADMIN`, preservó tenant en refresh, lockout temporal y concurrencia de token; MFA parcial. | 1,094 + 9. | Local; migración no aplicada. |
| Visor login | Mostrar/ocultar accesible, teclado/ARIA, ocultación automática, sin registrar password. | 13 checks navegador iniciales. | Local; no desplegado. |
| GL0B | Sesiones persistidas/revocables, JWT/cookie revalidados, logout, `SecurityStamp`, MFA restringido y rate limits. | 1,187 + 9; HTTP/Razor. | Local; no desplegado. |
| GL0C | SSO por issuer+subject, no por email; políticas Google/Entra; MFA concurrente; contrato Android. | 1,215 + 9; 11 SQL; 30 visuales; 16 Flutter. | Local; OIDC/APK reales pendientes. |
| GL1A | Ambiente fiscal inmutable, coherencia JSON/JWS/credenciales, correlativo transaccional y reportes solo PRODUCCIÓN. | 1,251 + 9; 13 SQL. | Local; sin MH real. |
| GL1B | Idempotencia durable de creación/emisión API/Connect/POS; replay usa la misma identidad. | 1,276 + 9; 28 SQL. | Local; consumidores/despliegue pendientes. |
| GL1C | Diagnóstico contextual `008`/`096`, códigos API estables, DTE existente en error y conservación de estados inciertos. | 1,319 + 9; 57 focales; 32 SQL. | Local; sin consulta MH real. |
| Revisión multiagente | Backend, pruebas, diseño, Web y dos revisores; identificó B1–B6 y D1–D5. | 1,328 ordinarias; cuatro defectos caracterizados. | Auditoría, no corrección. |
| GL1D | Cerró carrera/retry/invalidación B1/B2/B5 en envío individual; auditorías Git, SaaS y seguridad. | 1,340 + 9; 59 SQL. | Local; otros hallazgos siguieron. |
| GL1E | Coordinación de lotes, SSRF, bootstrap, cookies SSO y controles trial/transferencia SaaS. | 1,536 + 9; 221 focales; 83 SQL DTE; 16 SQL SaaS. | Local; externos pendientes. |
| GL1F | Conciliación DTE sin reenvío, licencia/cancelación local, Stripe fail-closed y branding defensivo. | 1,588 + 9; 44 focales; 75 Billing; 24 SQL. | Local; Billing distribuido pendiente. |
| GL1G | Intención Billing previa, lease, ACK durable, commit local atómico, cuarentena monotónica y lock de mutaciones. | 1,616 + 9; 93 Billing; 26 focales; 63 SQL. | **GO local GL1G; NO-GO global**. |

Migraciones preparadas y no aplicadas a base activa:

1. `20260904021242_GL0A_RefreshSessionContext`.
2. `20260904030641_GL0B_AuthSessionFoundation`.
3. `20260904125956_GL0C_SsoIdentityMfaConcurrency`.
4. `20260904140909_GL1B_DteIdempotency`.
5. `20260904154619_GL1D_DteFiscalConcurrency`.
6. `20260904173458_GL1E_LoteAttemptConcurrency`.
7. `20260905002545_GL1G_BillingProviderOperations`.

## 5. Estado de los reportes del cliente

### Datos fiscales y errores

El backend sanea/valida actividad y territorio del emisor y produce mensajes más claros. Atiende la raíz
de los errores `codActividad`, municipio y departamento, pero falta desplegar y probar con la configuración
vigente del cliente. El servicio activo anterior no demuestra que esta corrección esté operativa.

### País, departamento y municipio

- Regla: país → departamento → municipio.
- En fixtures Web funcionó la cascada por `ParentCodigo` y la limpieza/ocultación para extranjero.
- Falta comprobar catálogo MH real, tenant, endpoints y asociaciones `label/for` en el despliegue.
- Para país distinto de El Salvador no se muestran ni exigen códigos territoriales salvadoreños.

### Cliente, stepper, filtros y retorno

- Autocompletado revisado estáticamente; falta E2E de cambio/limpieza con datos anonimizados.
- `PROCESADO` mostró cinco puntos violetas en Razor; falta validar el binario desplegado.
- Hay cobertura backend de filtros; falta E2E Web/HTTP paginado y por permisos.
- Retorno estaba limitado a buscar en los primeros 200 DTE; falta búsqueda remota/paginada y elegibilidad.
- NEO tuvo históricamente 5 retornos procesados; no prueba el despliegue actual del cliente.

### Certificación del cliente

| Tipo | Documento | Captura | Pendiente histórico |
| --- | --- | ---: | ---: |
| 01 | Factura | 1/90 | 89 |
| 03 | CCF | 0/75 | 75 |
| 11 | Exportación | 0/90 | 90 |
| 14 | Sujeto Excluido | 0/25 | 25 |
| Total | Cuatro tipos | 1/280 | **279** |

El conteo debe actualizarse directamente en el portal. DTE11 como incidente está cerrado, pero los casos 11
de la matriz siguen siendo obligatorios. No se ha ejecutado CERT-0–CERT-4.

## 6. Producción de NEO

El PDF aportado **listaba** en PRODUCCIÓN los tipos `01, 03, 04, 05, 06, 07, 08, 09, 11, 14` y los eventos
retorno, contingencia e invalidación. No mostraba Donación 15 ni Operaciones Especiales. Ese PDF no basta para
habilitar capacidades: autorización y vigencia deben revalidarse antes del corte. NEO y cliente son procesos
independientes.

El preflight de NEO fue de solo lectura, en PRUEBAS, con desafío RS512 local. No acredita vigencia/revocación,
login MH, aceptación fiscal o producción.

No se realizó cambio a PRODUCCIÓN, primera emisión, limpieza de 796 DTE de prueba observados, reseed de IDs,
reset de correlativos, migración activa, restore funcional, instalación/reinicio de servicios ni proveedor real.

## 7. Lo que falta

### P1 de release/producción

1. Congelar/inventariar el árbol, revisar archivos, hacer commits coherentes y validar el mismo SHA a publicar.
2. Restore aislado y ensayo de toda la cadena de migraciones sobre SQL Server objetivo; aplicación autorizada.
3. **GL1H:** correlación durable de checkout/pago/webhook con monto/moneda server-side, firma, dedupe y commit.
4. Reconciliación Billing administrativa, auditable y monitoreada; después evaluar Worker.
5. Outbox/reentrega de notificaciones poscommit.
6. Consulta MH controlada en PRUEBAS, con evidencia sanitizada y sin retransmisión.
7. Web/API/Worker Release como Windows Services: cuenta mínima, inicio sin login, recovery, health y singleton.
8. DataProtection persistente, secretos por ambiente, proxy/TLS y startup fail-closed.
9. Backup fuera del host y restore funcional de SQL, archivos y key ring con RTO/RPO.
10. E2E autenticado con dos tenants/roles; módulos no aceptados bloqueados en backend.

### Fiscal/Web

- Tipos DTE por plan/empresa/autorización fiscal en una regla central de todos los canales.
- Retorno remoto/paginado, filtros completos y catálogo territorial real.
- Guía/acciones Web por permiso y stepper por evidencia.
- Corrección auditada de receptor histórico recuperable; nunca modificar silenciosamente `PROCESADO`.
- Diagnóstico de soporte con empresa explícita.
- Completar 01/03/11/14 del cliente en lotes pequeños, checkpoints y conciliación del portal.

### SaaS/producto

- Pausa/reanudación `PAUSED_BY_CUSTOMER`: tiempo restante, idempotencia, concurrencia, cuotas y recurrencia.
- Una pasarela comercial completa primero; QR es enlace al checkout, no prueba de pago.
- Separar Billing SaaS de cobros de ventas, beneficiarios y credenciales.
- Cerrar downgrade/addons, snapshot histórico de plan y módulos efectivos.
- Definir reglas de tiempo pagado, cuota, deuda y reactivación gratuita.

### Operación/externos

- Elegir host, continuidad de energía/red y estrategia de base.
- Actualizar portal/certificado/datos del cliente sin exponer secretos.
- Coordinar contrato Android con Manuel.
- Probar SMTP/OIDC/pago real solo en cuentas y ventanas autorizadas.
- Capacitar operador en rechazo, timeout, consulta, contingencia y recuperación.

## 8. Reglas obligatorias

### Git

- Revisar rama/commit/divergencia/árbol y preservar cambios ajenos.
- No `reset --hard`, checkout destructivo, limpieza masiva o importación ciega de otro worktree.
- Código, migraciones y evidencia viajan juntos; el SHA validado es el que se publica.
- Commit/push/PR no equivalen a despliegue o habilitación fiscal.

### Tenant, permisos y licencias

- Toda operación se aísla por `EmpresaId` y revalida empresa, membresía, rol, permiso, módulo y licencia.
- `SUPERADMIN` exige identidad global persistida; un nombre enviado por cliente no concede privilegios.
- Un administrador tenant no amplía plan/autorización fiscal; acciones sensibles se auditan sin secretos.

### DTE/Hacienda

- Separar ambiente ASP.NET de ambiente fiscal.
- Preservar tenant, ambiente, UUID, número, JSON, JWS, sello e intentos.
- HTTP 200 no es `PROCESADO`; exigir estado/código/sello coherentes.
- Nunca retransmitir un POST incierto ni crear nueva identidad por timeout; conciliar la misma.
- No regenerar/invalidar/degradar terminales ni inventar datos fiscales.
- DTE11 no se reabre sin regresión nueva y autorización.

### Billing/pagos

- Intención/correlación antes del externo; idempotencia por tenant/operación.
- ACK antes del cambio local; no retry ciego ante ambigüedad.
- Validar proveedor, recurso, monto, moneda, comercio, plan, suscripción y licencia server-side.
- Firmar/deduplicar webhooks y aplicar transaccionalmente; redirect/QR/aprobación no prueba captura.
- Worker Billing apagado hasta reconciliación, monitoreo y runbook.

### Base/migraciones

- Build antes de migrar; diseño offline y revisión de tenant, índices, FKs y snapshot.
- Web/API pueden ejecutar `MigrateAsync` al arrancar: no iniciarlos contra base activa “para probar”.
- Sin migración productiva sin backup/restore, SQL revisado, ventana, rollback y autorización.
- No `TRUNCATE`, delete general, reseed global ni reset de correlativos. IDs internos no necesitan iniciar en 1.
- Una identidad fiscal aceptada nunca se reutiliza.

### Secretos/material legal

- No versionar/imprimir certificados, private keys, contraseñas, JWT, API keys o material fiscal crudo.
- Usar secret store/DataProtection y key ring persistente/protegido.
- Validar certificado sin exponer bytes/password; adjuntos legales no autorizan transmisión.

### Pruebas/evidencia

- Focales primero y suite/build amplios al tocar Auth, DTE, EF o código compartido.
- Distinguir InMemory, TestServer, LocalDB, navegador aislado y E2E externo.
- `AuditKnownDefect` verde confirma defecto; no es aceptación. No duplicar subconjuntos.
- Conservar TRX/log/JSON, revisar contadores y declarar límites.
- Health 200 no demuestra binario nuevo, Worker, Hacienda o pago.

### Despliegue/datos/UI

- Release versionado, no Debug/`dotnet run`; tres hosts coordinados y una sola instancia de Worker.
- No limpiar/resetear sin manifiesto exacto, dry-run, backup/restore, ventana y aprobación.
- Tras aceptación fiscal se repara hacia adelante; no restaurar para reutilizar números.
- UI data-driven y backend con la misma regla; errores con code, campos, acción, retryable y traceId.
- Auditar/documentar no autoriza desplegar, migrar, borrar, emitir, cobrar o cambiar ambiente.

## 9. Subagentes utilizados

Los informes históricos guardaron principalmente roles, no siempre alias personales. Todos compartieron el
worktree, trabajaron por tandas y el coordinador consolidó resultados.

| Rol/agente | Responsabilidad | Resultado/regla |
| --- | --- | --- |
| Auditor backend | DTE, tenant, permisos, recuperación/transporte. | Produjo B1–B6; auditoría sin corregir. |
| Pruebas backend | Build, suites, filtros y caracterizaciones. | Sintético; defectos separados de aceptación. |
| Diseño | Razor/CSS/JS, responsive, stepper, login, cliente y PDF. | Captura/estática no es E2E. |
| Pruebas Web | Razor real en navegador/resoluciones. | Sin hosts ni datos cliente. |
| Revisor backend | Aserciones, fixtures, DI, transporte, TRX/SQL. | Exigió carreras/evidencia realista. |
| Revisor diseño/Web | Capturas, HTML, accesibilidad/trazabilidad. | Confirmó D1–D5 y positivos visuales. |
| Documentación | Hechos, conteos, límites y pendientes. | Redacción no cierra defectos. |
| Auditor de ramas | Refs, worktrees, migraciones y CI. | Solo lectura; sin reset/merge/commit/push. |
| Auditor SaaS | Planes, trial, permisos, downgrade, pagos/licencias. | SAAS-01–07. |
| Ciberseguridad | SSRF, bootstrap, cookies y branding. | Separó sintético de pentest real. |
| SQL/evidencia | LocalDB, carreras y limpieza acotada. | Sin base cliente; solo bases propias exactas. |
| **Noether — `gl1g_final_audit`** | Revisión final independiente GL1G. | GL1G local GO; global NO-GO; sin P0/P1 nuevo en perímetro. |
| **Beauvoir — `gl1g_evidence_audit`** | TRX, arnés SQL, carreras y documentación. | Confirmó 1,625 xUnit, 63 SQL y coherencia. |

Reglas de subagentes:

1. No sobrescribir cambios de otros.
2. Auditor/revisor en solo lectura salvo encargo explícito.
3. Implementador no dicta solo la aceptación final.
4. Revisor comprueba código/artifacts, no solo narración.
5. Fixtures sintéticas/anonimizadas; sin secretos/base cliente.
6. Subagente no autoriza despliegue, migración, limpieza, emisión o cobro.
7. Coordinador integra y revalida la versión final.
8. Hallazgo se cierra solo con regresión segura.
9. Base temporal se elimina por nombre exacto.
10. Manuel conserva APK; backend conserva contrato API.

## 10. Siguiente sprint recomendado: GL1H

**Checkout, pagos y webhooks durables.**

Entregables:

1. Agregado empresa–plan–checkout–pago–suscripción–licencia–proveedor–monto–moneda–beneficiario.
2. Persistencia previa e idempotencia/recuperación ante timeout.
3. Montos/mappings server-side, sin cero/placeholder/Mock.
4. Firma/timestamp, dedupe, consulta al proveedor y transición transaccional.
5. Política atómica de período/licencia.
6. Reconciliador administrativo y Worker controlado.
7. Sandbox de un proveedor: éxito, rechazo, expiración, repetición, desorden, monto falso, devolución y fallos.
8. SQL concurrente, TestServer, suite completa, auditoría independiente y README API/Web.

Criterio: ninguna operación externa sin intención/correlación local; webhook falso/repetido/desordenado no
activa ni duplica; estado ambiguo se resuelve sin POST ciego. Solo se habilita el proveedor que cierre el ciclo.

## 11. Ruta posterior

1. Convertir el árbol en rama/PR reproducible.
2. Auditoría final sobre el mismo commit.
3. Restore aislado y migraciones GL0A–GL1H.
4. Web/API/Worker como servicios en preproducción.
5. E2E Auth/tenant/DTE/retorno/contingencia/reportes/Billing.
6. Prueba MH controlada en PRUEBAS.
7. Actualizar portal y completar certificación del cliente.
8. Checklist NEO, backup fresco y go/no-go firmado.
9. Habilitar solo tipos/módulos/proveedores revalidados.
10. Primera operación productiva legítima, supervisada y monitoreada.

## 12. Fuentes

- [Índice](README.md), [auditoría](Auditoria-Sistema-API.md), [plan cliente](Plan-Cliente-Certificacion.md),
  [plan NEO](Plan-Produccion-NEO.md).
- [GL0A](Avance-GL0A.md), [GL0B](Avance-GL0B-Integrado.md), [GL0C](Avance-GL0C-SSO-SQL-Android.md).
- [GL1A](Avance-GL1A-Integridad-Fiscal.md), [GL1B](Avance-GL1B-Idempotencia.md),
  [GL1C](Avance-GL1C-Diagnostico-Recuperacion.md).
- [Revisión multiagente](Revision-Multiagente-2026-09-04.md),
  [GL1D](GL1D-Correcciones-Auditorias-2026-09-04.md),
  [GL1E](GL1E-Cierre-Seguridad-Lotes-2026-09-04.md),
  [GL1F](GL1F-Cierre-Conciliacion-Licencias-Branding-2026-09-04.md),
  [GL1G](GL1G-Cierre-Coordinacion-Billing-2026-09-04.md).
- [Visor login](Avance-Visor-Login.md), [evidencia](evidencia/README.md),
  [README API](../../src/NeoSTP.Api/README.md), [README Web](../../src/NeoSTP.Web/README.md).

## 13. Cierre

NeoSTP avanzó de forma sustancial en identidad, tenant, integridad fiscal, idempotencia, diagnóstico,
concurrencia, lotes, SSRF, SaaS, conciliación, branding y cancelaciones durables. GL1G obtuvo revisión
independiente favorable dentro de su perímetro.

Estado correcto para comunicar: **desarrollo y auditoría local avanzados; commit/release, despliegue,
migraciones activas, pruebas externas, certificación y producción todavía pendientes**.
