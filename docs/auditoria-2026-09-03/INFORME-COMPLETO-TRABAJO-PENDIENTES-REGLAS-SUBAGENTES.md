# Informe completo: trabajo, pendientes, reglas y subagentes

> Reauditado el 2026-09-04: el cuerpo conserva el corte histórico GL1G. Consultar la [auditoría actualizada](Auditoria-Actualizacion-2026-09-04.md), el [plan de continuidad](Plan-Continuidad-Auditado-2026-09-04.md) y el [catálogo de subagentes](Subagentes-Auditoria-Continuidad.md).

Fecha: **2026-09-04**  
Repositorio: `NeoStpCloud`  
Rama: `codex/gl0a-auth-security`  
Base/HEAD confirmado: `a8c5d163fb372359c4738f0c3814ef1ee14024eb` (`a8c5d16`)  
Dictamen: **bloques locales validados; PRODUCCIÓN global en NO-GO**.

Este documento es la fuente consolidada de la campaña. Cuando un informe histórico describe un estado
anterior, prevalece el cierre posterior. “Implementado” significa presente en el árbol local; “validado”
significa probado dentro del alcance indicado. No significa desplegado, migrado en la base activa,
aceptado por Hacienda ni habilitado en producción.

## 1. Estado ejecutivo

| Tema | Estado real |
| --- | --- |
| Git | GL0A–GL1G están en un árbol grande sin commit/push de esta campaña; el commit base sigue siendo `a8c5d16`. |
| Rama remota | La rama no estaba publicada ni tenía upstream; `main`/`origin/main` coincidían en `a8c5d16`. |
| Build final | Release: 0 errores, 0 advertencias. |
| Pruebas finales | 1,616 unitarias + 9 integración = **1,625 xUnit distintas**. |
| GL1G | 93 Billing y 26 focales son subconjuntos; 63/63 checks SQL independientes. |
| Base activa | Sin limpieza, reseed, migración ni cambios a datos del cliente. |
| Servicios | API/Web anteriores dan health 200, pero son binarios previos; no prueban este código. Worker no acreditado. |
| Externos | Sin nueva transmisión/consulta real MH, cobro, cancelación, SMTP u OIDC real. |
| Android | Manuel mantiene la APK; el alcance actual es API/contratos. |
| DTE11 | Incidente anterior cerrado por instrucción expresa del usuario. |
| Producción | **NO-GO** hasta cerrar P1 aplicables o bloquear efectivamente esas funciones. |

## 2. Objetivos solicitados

- Terminar retorno y contingencias.
- Corregir actividad/territorio del emisor y mensajes de Hacienda.
- País → departamento → municipio para El Salvador; ocultar/limpiar para extranjeros.
- Autocompletar identificación, NRC, nombre y correo del cliente.
- Stepper `PROCESADO` morado y completo.
- Filtros por DTE, código, monto, cliente, tipo, estado, fecha y ambiente.
- Web/API/Worker confiables y autoarranque al encender la PC.
- Auditoría completa de sistema, API, Web, diseño, ramas, SaaS y ciberseguridad.
- Tipos DTE configurables por plan/empresa.
- QR/links/pasarelas de pago API-first.
- Pausa/reanudación de suscripción sin cargo de reactivación.
- Visor de contraseña en login.
- Plan independiente de certificación del cliente y producción de NEO.

## 3. Trabajo ejecutado

### Auditoría base

- Inventario de 39 controladores/360 acciones API y 51 controladores/365 acciones Web.
- Revisión de arquitectura, tenant, autorización, configuración, contratos, datos, rutas y respaldo.
- Línea base: 1,005 unitarias + 9 integración; 147 rutas protegidas respondieron 401 respetando cuotas.
- Reproducción de escalada, pérdida de empresa en refresh, lockout y MFA sin contador.
- `.bak` pasó `RESTORE VERIFYONLY`; no hubo restauración funcional.
- Las nueve pruebas llamadas integración usan EF InMemory y no acreditan SQL/transacciones reales.

### GL0A — seguridad tenant y refresh

- Escalada por nombre `SUPERADMIN` cerrada en código.
- Roles/usuarios/membresías/permisos validan actor, ámbito y empresa.
- Refresh conserva empresa y revalida membresía/rol/estado; refresh viejo exige login.
- Bloqueo temporal vencido se recupera; suspensión administrativa no.
- Rotación concurrente protegida y MFA comienza a contar fallos.
- Migración `20260904021242_GL0A_RefreshSessionContext`.
- Evidencia: 1,094 unitarias + 9 integración. Local, no desplegado.

### Visor de contraseña

- Mostrar/ocultar por clic/teclado, SVG local, ARIA y ocultación automática.
- No copia ni registra contraseñas; orden de jQuery/validadores corregido.
- Trece comprobaciones iniciales escritorio/móvil. Local, no desplegado.

### GL0B — sesiones, revocación, MFA y rate limits

- Sesiones persistidas; JWT/cookie revalidados por petición; logout revoca sesión padre.
- `SecurityStamp` en cambios sensibles.
- Propósitos `FULL`, `MFA_ENROLL`, `MFA_VERIFY` sin privilegios en desafíos.
- Web MFA con antiforgery/no-store; límites separados login/MFA/refresh y `Retry-After`.
- Migración `20260904030641_GL0B_AuthSessionFoundation`.
- Evidencia: 1,187 unitarias + 9 integración y pruebas HTTP/visuales. Local, no desplegado.

### GL0C — SSO, MFA concurrente y contrato Android

- SSO por proveedor + issuer + subject, no por correo automático.
- Validación Entra/Google y enlace protegido; `MfaVersion`/CAS para carreras.
- Contrato Android de sesión/refresh/MFA endurecido.
- Migración `20260904125956_GL0C_SsoIdentityMfaConcurrency`.
- Evidencia: 1,215 unitarias + 9 integración, 11 SQL, 30 visuales y 16 Flutter.
- Pendiente OIDC real, APK/dispositivo y despliegue coordinado.

### GL1A — integridad fiscal

- Ambiente fiscal explícito/inmutable y coherencia documento–JSON–JWS–credenciales–establecimiento.
- Retorno, invalidación, contingencia y lotes validan empresa/ambiente/origen.
- Estados enviados/procesados/invalidados no se reabren por revalidación.
- Caché MH ligada a tenant/ambiente/credenciales; correlativo transaccional sin reset.
- Reportes y NeoProfit filtran PRODUCCIÓN.
- Evidencia: 1,251 unitarias + 9 integración y 13 SQL. Local, no desplegado.

### GL1B — idempotencia DTE/NeoConnect/POS

- Key por empresa + ámbito + hash persistida atómicamente con el DTE.
- Replay devuelve la misma identidad, no consume correlativo ni retransmite.
- Body distinto con misma key: `409 / IDEMPOTENCY_CONFLICT`.
- POS con ámbito interno y vínculo venta–DTE transaccional.
- Migración `20260904140909_GL1B_DteIdempotency`.
- Evidencia: 1,276 unitarias + 9 integración y 28 SQL. Local, no desplegado.

### GL1C — diagnóstico Hacienda y recuperación

- Diagnóstico contextual que cubre `008/codActividad` y `096/territorio`.
- `ApiResponse<T>.code`; el error conserva el DTE existente en `data`.
- Códigos claros: datos inválidos, rechazo, resultado incierto y autenticación MH.
- Guía con campo, sección, acción, traceId y necesidad de consulta.
- HTTP 200 sin `PROCESADO`/sello no es aceptación; timeout conserva identidad e historial.
- Evidencia: 1,319 unitarias + 9 integración, 57 focales y 32 SQL.

### Revisión multiagente

- Separó backend, pruebas, diseño, Web y dos revisiones independientes.
- Halló B1–B6 y D1–D5: carreras, invalidación incierta, retries, receptor histórico, soporte,
  lotes, guía/acciones Web, retorno, labels y stepper.
- Positivos de fixtures: stepper morado, visor accesible y cascada territorial/limpieza de extranjero.
- La fixture territorial no acredita el catálogo real del cliente.
- 1,328 pruebas ordinarias; cuatro `AuditKnownDefect` confirmaban defectos, no aceptación.

### GL1D — concurrencia fiscal, ramas, SaaS y seguridad

- B1/B2/B5 cerrados local/SQL: una transmisión, estados terminales protegidos, invalidación segura.
- Eliminados retries automáticos POST en recepción, eventos, contingencia y lotes.
- Auditoría Git confirmó cambios locales no publicados y riesgo de migrar/sembrar al arrancar.
- Auditoría SaaS/seguridad identificó trial, downgrade, pagos, SSRF, bootstrap, SameSite y logo.
- Preflight NEO en PRUEBAS y desafío RS512 local, sin autenticación/transmisión MH.
- Evidencia: 1,340 unitarias + 9 integración y 59 SQL.

### GL1E — lotes, SSRF, bootstrap, cookies y SaaS

- Reserva durable de lote/documentos antes del POST y competencia con otros envíos.
- Resultado perdido no retransmite; consulta tardía no pisa identidad regenerada.
- NeoConnect exige HTTPS/destino público y bloquea redes privadas, proxy, redirects y cookies.
- Bootstrap sin credencial conocida/log; cookies OIDC/local/antiforgery separadas correctamente.
- Trial finito, permisos tenant/central, GET sin mutación, transferencia POST y lock serializable.
- Migración `20260904173458_GL1E_LoteAttemptConcurrency` (`Up/Down` vacíos).
- Evidencia: 1,536 unitarias + 9 integración, 221 focales, 83 SQL DTE y 16 SQL SaaS.

### GL1F — conciliación, licencias y branding

- Consulta MH del intento incierto sin retransmisión, correlacionada con toda la identidad fiscal.
- Cancelación local inmediata revoca licencia; programada conserva período pagado finito.
- Stripe falla cerrado sin firma válida.
- Branding valida contenido/dimensiones/MIME; blob legado inválido no rompe DTE/PDF/correo.
- Regresión de tracking EF detectada y corregida.
- Evidencia: 1,588 unitarias + 9 integración, 44 focales, 75 Billing y 24 SQL Billing.

### GL1G — cancelación Billing durable

- Intención SQL antes del proveedor, snapshot completo e idempotencia.
- Lease único y ACK remoto durable antes del commit local serializable.
- Fallo/timeout sin ACK → `REQUIRES_RECONCILIATION`, sin retry externo ciego.
- Con ACK solo termina fase local; cuarentena no pisa `ACK`/`COMPLETED`.
- Modos opuestos bloqueados; checkout/portal comparten lock de preflight por empresa.
- Migración `20260905002545_GL1G_BillingProviderOperations`.
- Worker registrado y apagado por defecto.
- Evidencia final: build 0/0, 1,616 unitarias, 9 integración, 93 Billing, 26 focales y 63 SQL.
- Dictamen independiente: **GL1G local GO; producción global NO-GO**.

## 4. Estado exacto de los reportes del cliente

### Datos fiscales

El backend atiende la raíz de actividad/territorio y mejora el mensaje, pero falta desplegar esta release y
probar con la configuración vigente del cliente. No afirmar que ya factura mientras el servicio use binarios previos.

### Municipios y departamentos

- Regla correcta: país → departamento → municipio.
- La cascada `ParentCodigo` y la limpieza para extranjero pasaron en fixtures Web.
- Falta comprobar catálogo MH real, tenant, endpoint y asociaciones accesibles `label/for` en el despliegue.

### Cliente, stepper, filtros y retorno

- Autocompletado revisado estáticamente; falta E2E de cambio/limpieza con datos anonimizados.
- `PROCESADO` mostró cinco puntos violetas en Razor; falta comprobar binario desplegado.
- Hay pruebas backend de filtros; falta recorrido Web/HTTP paginado, permisos y combinaciones.
- Retorno tenía límite de primeros 200 DTE; falta búsqueda remota/paginada y elegibilidad real.
- NEO tuvo 5 retornos procesados históricamente; no acredita el flujo actual del cliente.

### Certificación del cliente

| Tipo | Documento | Captura | Pendiente histórico |
| --- | --- | ---: | ---: |
| 01 | Factura | 1/90 | 89 |
| 03 | CCF | 0/75 | 75 |
| 11 | Exportación | 0/90 | 90 |
| 14 | Sujeto Excluido | 0/25 | 25 |
| Total | Cuatro tipos | 1/280 | **279** |

Este conteo debe actualizarse en el portal. DTE11 como incidente está cerrado; sus escenarios de certificación
siguen siendo parte de la matriz obligatoria.

## 5. Producción de NEO

El PDF aportado acreditaba tipos `01, 03, 04, 05, 06, 07, 08, 09, 11, 14` y retorno, contingencia e
invalidación. No acreditaba Donación 15 ni Operaciones Especiales. NEO y cliente tienen cortes separados.

No se ejecutó cambio a PRODUCCIÓN, primera emisión, limpieza de 796 DTE de prueba observados, reseed,
migración activa, instalación/reinicio de servicios, restore funcional ni proveedor externo real.

## 6. Pendientes

### P0/P1 de salida

1. Crear release reproducible: congelar/inventariar árbol, revisar archivos, commits coherentes, pruebas sobre
   el mismo SHA, rama/PR/tag.
2. Restaurar copia y ensayar cadena GL0A–GL1G sobre SQL Server objetivo; aplicar migraciones solo con autorización.
3. **GL1H:** checkout/pago/webhook durable con correlación, monto/moneda server-side, firma, dedupe y commit atómico.
4. Conciliación Billing administrativa, auditada y monitoreada; después considerar encender Worker.
5. Outbox/reentrega de notificaciones posteriores al commit.
6. Consulta MH controlada en PRUEBAS con evidencia sanitizada, sin reenvío.
7. Publicar Web/API/Worker Release como Windows Services: cuenta mínima, inicio sin login, recovery, health y singleton.
8. Key ring DataProtection persistente, secretos por ambiente, proxy/TLS y startup fail-closed.
9. Backup fresco fuera del host y restore aislado de SQL/archivos/key ring con RTO/RPO.
10. E2E autenticado con dos tenants/roles; módulo no aprobado bloqueado en backend.

### Producto fiscal/Web

1. Tipos DTE por plan/empresa/autorización, regla central para todos los canales.
2. Retorno remoto/paginado y filtros completos.
3. Catálogo territorial real y formulario cliente accesible.
4. Guía/acciones Web por permiso y stepper por evidencia.
5. Corrección autorizada/auditada del receptor histórico recuperable; nunca mutar un procesado.
6. Diagnóstico de soporte con empresa explícita.
7. Completar campaña 01/03/11/14 en lotes pequeños y conciliar portal.

### SaaS/producto

1. Pausa/reanudación `PAUSED_BY_CUSTOMER`, saldo de tiempo, idempotencia, concurrencia, cuotas y recurrencia.
2. Una pasarela comercial completa primero; QR es enlace al checkout, no prueba de pago.
3. Separar Billing SaaS de cobros de ventas/beneficiarios/credenciales.
4. Cerrar downgrade/addons, snapshot de plan y módulos efectivos con SQL concurrente.
5. Definir reglas de negocio de pausa, tiempo pagado, cuota, deuda y reactivación gratuita.

### Operación/externos

- Elegir host, continuidad de energía/red y estrategia de base.
- Actualizar portal/credenciales del cliente sin exponer secretos.
- Coordinar contrato Android con Manuel.
- Probar SMTP/OIDC/pagos externos en cuentas autorizadas.
- Capacitar operador en rechazo, timeout, consulta, contingencia y recuperación.

## 7. Reglas obligatorias

### Git

- Revisar rama/commit/divergencia/árbol antes de actuar y preservar cambios ajenos.
- No `reset --hard`, checkout destructivo, limpieza masiva ni importación ciega de otro worktree.
- Código, migraciones y evidencia deben viajar juntos; validar el mismo SHA que se publica.
- Commit/push/PR no significan despliegue ni habilitación fiscal.

### Tenant, permisos y licencia

- Toda operación se aísla por `EmpresaId` y revalida empresa, membresía, rol, permiso, módulo y licencia.
- `SUPERADMIN` exige identidad global persistida; un nombre enviado por cliente no concede privilegios.
- Tenant admin no amplía plan o autorización fiscal; acciones sensibles se auditan sin secretos.

### Hacienda/DTE

- Separar ambiente ASP.NET de ambiente fiscal.
- Preservar tenant, ambiente, UUID, número, JSON, JWS, sello e intentos.
- HTTP 200 no es `PROCESADO`; exigir estado/código/sello coherentes.
- Nunca retransmitir un POST incierto ni crear otra identidad por timeout: conciliar la misma.
- No regenerar/invalidar/degradar estados terminales ni inventar datos fiscales.
- DTE11 no se reabre sin una regresión nueva y autorización.

### Billing/pagos

- Intención/correlación antes del efecto externo; idempotencia estable por tenant/operación.
- ACK antes del cambio local y sin reintento ciego ante ambigüedad.
- Validar proveedor, recurso, monto, moneda, comercio, plan, suscripción y licencia en servidor.
- Verificar/deduplicar webhook y aplicar transacción; redirect/QR/aprobación no equivale a pago capturado.
- Worker Billing sigue apagado hasta reconciliación, monitoreo y runbook.

### Base/migraciones

- Build antes de migrar; diseño offline y revisión de tenant, índices, FKs y snapshot.
- Web/API pueden ejecutar `MigrateAsync` al arrancar: no iniciar contra base activa para “probar”.
- Sin migración productiva sin backup/restore, SQL revisado, ventana, rollback y autorización.
- No `TRUNCATE`, delete general, reseed global o reset de correlativos. IDs internos no necesitan empezar en 1.
- Una identidad fiscal aceptada nunca se reutiliza.

### Secretos/material legal

- No versionar/imprimir certificados, private keys, contraseñas, JWT, API keys o material fiscal crudo.
- DataProtection/secret store y key ring persistente/protegido.
- Validar certificado sin exponer bytes/password; adjuntos legales no autorizan transmisión.

### Pruebas/evidencia

- Focales primero; suite/build amplios al tocar Auth, DTE, EF o código compartido.
- Distinguir InMemory, TestServer, LocalDB, navegador aislado y E2E externo.
- `AuditKnownDefect` verde confirma defecto; no suma aceptación. No duplicar subconjuntos.
- Conservar TRX/log/JSON, revisar contadores y documentar límites.
- Health 200 no demuestra binario nuevo, Worker, Hacienda o pago.

### Despliegue/datos

- Release versionado, no `dotnet run`/Debug; tres hosts coordinados con health y una sola instancia.
- No limpiar/resetear sin manifiesto exacto, dry-run, backup/restore, ventana y aprobación.
- Después de una aceptación fiscal, reparar hacia adelante; no restaurar para reutilizar números.
- Auditar/documentar no autoriza desplegar, migrar, borrar, emitir, cobrar o cambiar ambiente.

### UI/contratos

- UI data-driven; backend aplica la misma regla que Web/App.
- Errores: code, mensaje, campos, acción, retryable y traceId sin secretos.
- Estados/badges/stepper semánticos; municipio pertenece al departamento, no al revés.

## 8. Subagentes usados

Los informes históricos registran principalmente roles, no siempre alias personales; no se inventan nombres.
Todos compartieron el worktree, trabajaron por tandas y el coordinador integró los resultados.

| Rol | Trabajo | Resultado/regla |
| --- | --- | --- |
| Auditor backend | Transiciones DTE, tenant, permisos y transporte. | Produjo B1–B6; auditoría sin corregir. |
| Pruebas backend | Build, suites, filtros y caracterizaciones. | Datos sintéticos; aceptación separada de defectos conocidos. |
| Diseño | Razor/CSS/JS, responsive, stepper, login, formularios/PDF. | Captura/estática no equivale a E2E. |
| Pruebas Web | Razor real en navegador y dos resoluciones. | Sin hosts ni datos del cliente. |
| Revisor backend | Aserciones, fixtures, DI, transporte, TRX/SQL. | Exigió carreras y evidencia más realistas. |
| Revisor diseño/Web | Capturas, HTML, accesibilidad y trazabilidad. | Confirmó D1–D5 y positivos visuales. |
| Documentación | Consolidó hechos, conteos, límites y pendientes. | Redacción no cierra defectos. |
| Ramas | Refs, worktrees, migraciones y CI. | Solo lectura; sin reset/merge/commit/push. |
| SaaS | Planes, trial, permisos, downgrade, pagos/licencias. | Produjo SAAS-01–07 y pruebas requeridas. |
| Ciberseguridad | SSRF, bootstrap, cookies, branding/superficies. | Separó sintético de pentest real. |
| SQL/evidencia | LocalDB, carreras deterministas y limpieza acotada. | Nunca base del cliente; solo bases aleatorias propias. |
| **Noether (`gl1g_final_audit`)** | Auditoría final independiente de GL1G. | GL1G local GO, producción global NO-GO; sin P0/P1 nuevo en su perímetro. |
| **Beauvoir (`gl1g_evidence_audit`)** | Revisó TRX, arnés SQL, carreras y documentación. | Confirmó 1,625 xUnit, 63 SQL y coherencia del cierre. |

Reglas de colaboración:

1. Nadie sobrescribe cambios de otro agente.
2. Auditor/revisor es read-only salvo encargo explícito.
3. Implementador no emite solo el dictamen final.
4. Revisor comprueba código y artifacts, no solo conteos narrados.
5. Fixtures sintéticas/anonimizadas; sin secretos o base cliente.
6. Ningún subagente autoriza despliegue, migración, limpieza, emisión o cobro real.
7. Coordinador reintegra y revalida la versión final.
8. Hallazgo no se cierra hasta convertirse en regresión segura.
9. Bases temporales se validan y eliminan solo por nombre exacto.
10. Manuel conserva APK; backend mantiene el contrato API.

## 9. Siguiente sprint recomendado: GL1H

**Checkout, pagos y webhooks durables** antes de activar Billing.

Entregables:

1. Agregado empresa–plan–checkout–pago–suscripción–licencia–proveedor–recurso–monto–moneda–beneficiario.
2. Persistencia previa a externos e idempotencia/recuperación ante timeout.
3. Monto/moneda server-side y mappings obligatorios, sin cero/placeholder/Mock.
4. Firma/timestamp, dedupe, consulta al proveedor y transición transaccional por webhook.
5. Política atómica de período/licencia para activar, renovar, cambiar y cancelar.
6. Reconciliador administrativo, auditoría y Worker controlado.
7. Sandbox completo de un proveedor: éxito, rechazo, expiración, repetición, desorden, fraude de monto,
   devolución y fallos antes/después de ACK.
8. SQL concurrente, TestServer, suite completa, auditoría independiente y docs API/Web.

Criterio: ninguna operación externa sin intención/correlación local; webhook falso/repetido/desordenado no
activa ni duplica; los ambiguos son resolubles sin POST ciego. Solo el proveedor que cierre todo el ciclo se habilita.

## 10. Ruta posterior hacia producción

1. Convertir árbol local en rama/PR reproducible.
2. Revisión independiente final del mismo commit.
3. Restore aislado y migraciones GL0A–GL1H.
4. Web/API/Worker Release como servicios en preproducción.
5. E2E Auth/tenant/DTE/retorno/contingencia/reportes/Billing.
6. Prueba MH controlada en PRUEBAS y conciliación.
7. Actualizar portal y completar certificación del cliente.
8. Checklist NEO, backup fresco y go/no-go firmado.
9. Habilitar solo tipos/módulos/proveedores acreditados.
10. Primera operación productiva legítima, supervisada y monitoreada.

## 11. Fuentes

- [Índice](README.md)
- [Auditoría integral](Auditoria-Sistema-API.md)
- [Plan cliente](Plan-Cliente-Certificacion.md)
- [Plan NEO](Plan-Produccion-NEO.md)
- [GL0A](Avance-GL0A.md), [GL0B](Avance-GL0B-Integrado.md), [GL0C](Avance-GL0C-SSO-SQL-Android.md)
- [GL1A](Avance-GL1A-Integridad-Fiscal.md), [GL1B](Avance-GL1B-Idempotencia.md),
  [GL1C](Avance-GL1C-Diagnostico-Recuperacion.md)
- [Revisión multiagente](Revision-Multiagente-2026-09-04.md)
- [GL1D](GL1D-Correcciones-Auditorias-2026-09-04.md),
  [GL1E](GL1E-Cierre-Seguridad-Lotes-2026-09-04.md),
  [GL1F](GL1F-Cierre-Conciliacion-Licencias-Branding-2026-09-04.md),
  [GL1G](GL1G-Cierre-Coordinacion-Billing-2026-09-04.md)
- [Visor de contraseña](Avance-Visor-Login.md)
- [Evidencia reproducible](evidencia/README.md)
- [README API](../../src/NeoSTP.Api/README.md) y [README Web](../../src/NeoSTP.Web/README.md)

## 12. Declaración final

El sistema avanzó de forma sustancial en identidad, tenant, integridad fiscal, idempotencia, diagnóstico,
concurrencia, lotes, SSRF, SaaS, conciliación, branding y cancelaciones durables. GL1G obtuvo revisión
independiente favorable dentro de su perímetro.

El estado correcto para comunicar es: **desarrollo y auditoría local avanzados; release, despliegue,
pruebas externas, certificación y producción todavía pendientes**.
