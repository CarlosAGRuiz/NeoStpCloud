# Estado maestro de NeoSTP Cloud: trabajo, pendientes, reglas y subagentes

Fecha de consolidación: **2026-09-04**  
Repositorio: `NeoStpCloud`  
Rama: `codex/gl0a-auth-security`  
Base/HEAD confirmado: `a8c5d163fb372359c4738f0c3814ef1ee14024eb` (`a8c5d16`)  
Decisión: **avance local aprobado por bloques; salida global a PRODUCCIÓN en NO-GO**.

Este documento consolida los cierres GL0A–GL1G, la revisión multiagente, el plan de certificación
del cliente y el plan de producción de NEO. Cuando un informe histórico describe un estado anterior,
prevalece el cierre posterior.

> **Importante:** “implementado” significa que el cambio existe en el árbol local; “validado” significa
> que pasó la evidencia indicada dentro de su alcance. No significan desplegado, migrado en la base activa,
> aceptado por Hacienda o habilitado en producción.

## 1. Resumen ejecutivo

| Tema | Estado al corte |
| --- | --- |
| Git | El commit base sigue siendo `a8c5d16`. GL0A–GL1G están en un árbol grande sin commit/push de esta campaña. |
| Rama remota | `codex/gl0a-auth-security` no estaba publicada ni tenía upstream; `main` y `origin/main` coincidían en `a8c5d16`. |
| Build más reciente | Release: 0 errores y 0 advertencias. |
| Suite más reciente | 1,616 unitarias + 9 integración = **1,625 xUnit distintas aprobadas**. |
| GL1G | 93/93 Billing y 26/26 focales, subconjuntos del total; 63/63 checks SQL independientes. |
| Base del cliente | No fue modificada, limpiada, resecuenciada ni migrada. |
| Migraciones | Generadas y ensayadas offline/LocalDB sintético; no aplicadas a una base activa. |
| API/Web | Salud HTTP 200 en procesos anteriores; eran binarios previos y no acreditan el nuevo código. |
| Worker | No acreditado como servicio productivo; el Worker Billing está apagado por defecto. |
| Hacienda/pasarelas | Sin transmisión/consulta MH nueva ni cobro/cancelación real en estos cierres. |
| Android | Manuel mantiene la APK; este equipo trabaja la API y sus contratos. |
| DTE11 | Incidente anterior **cerrado por instrucción expresa del usuario**. |
| Producción | **NO-GO** hasta cerrar los P1 aplicables o bloquear efectivamente esas funciones. |

## 2. Solicitudes que originaron el trabajo

1. Terminar retorno y manejar contingencias sin reenvíos inseguros.
2. Corregir actividad y territorio del emisor y explicar claramente los errores de Hacienda.
3. Aplicar país → departamento → municipio para El Salvador; ocultar/limpiar territorio para extranjeros.
4. Autocompletar identificación, nombre, NRC y correo del cliente registrado.
5. Completar en violeta el stepper de un DTE `PROCESADO`.
6. Filtros por número DTE, código, monto, cliente, tipo, estado, fecha y ambiente.
7. Levantar API, Web y Worker de forma confiable al encender el equipo.
8. Auditar sistema, API, Web, diseño, ramas, SaaS y ciberseguridad.
9. Configurar tipos DTE disponibles por plan/empresa.
10. Diseñar pagos QR/links y pasarelas API-first para Web/App.
11. Diseñar pausa/reanudación voluntaria de la suscripción sin cargo de reactivación.
12. Agregar visor de contraseña accesible al login.
13. Preparar certificación del cliente y plan de producción independiente para NEO.

## 3. Trabajo realizado y validado por bloque

### 3.1 Auditoría base

- Inventario: 39 controladores/360 acciones API y 51 controladores/365 acciones Web.
- Revisión de arquitectura, autorización, contratos, configuración, datos de lectura, rutas y respaldo.
- Línea base: 1,005 unitarias + 9 integración; 147 rutas protegidas terminaron en 401 al respetar cuotas.
- Se reprodujeron escalada de privilegios, pérdida de empresa en refresh, lockout y MFA sin contador.
- El `.bak` pasó `RESTORE VERIFYONLY`; no se ejecutó una restauración funcional.
- Las nueve pruebas llamadas integración usan EF InMemory y no acreditan SQL/transacciones reales.

### 3.2 GL0A — privilegios, tenant, refresh y bloqueo

Estado: **implementado/validado local; no desplegado**.

- Cerrada en código la escalada basada solo en el nombre `SUPERADMIN`.
- Roles, usuarios, membresías y permisos validan actor, ámbito y tenant confiables.
- Refresh conserva la empresa seleccionada y revalida empresa, rol, membresía y estado.
- Refresh histórico sin contexto exige login nuevo; no infiere otra empresa.
- Solo bloqueo temporal vencido se recupera; bloqueo administrativo no.
- Rotación de refresh protegida por concurrencia; MFA comenzó a contar fallos.
- Migración: `20260904021242_GL0A_RefreshSessionContext`.
- Evidencia: 1,094 unitarias + 9 integración.

### 3.3 Visor de contraseña

Estado: **implementado/validado en Razor y navegador aislado; no desplegado**.

- Mostrar/ocultar por clic o teclado, SVG local, `aria-label`/`aria-pressed`.
- Se oculta al enviar, cambiar pestaña o salir; no copia ni registra la contraseña.
- Corregido el orden de jQuery y validadores; 13 comprobaciones iniciales escritorio/móvil.

### 3.4 GL0B — sesiones, revocación, MFA restringido y límites HTTP

Estado: **integrado/validado local; no desplegado**.

- Sesiones persistidas para login, SSO y cambio de empresa; JWT/cookie se revalidan por petición.
- Logout revoca la sesión padre y credenciales históricas/revocadas fallan cerrado.
- `SecurityStamp` rota con cambios sensibles.
- Propósitos `FULL`, `MFA_ENROLL`, `MFA_VERIFY`; desafíos sin permisos operativos.
- Web MFA con antiforgery/no-store; rate limits separados login/MFA/refresh y `Retry-After`.
- Migración: `20260904030641_GL0B_AuthSessionFoundation`.
- Evidencia: 1,187 unitarias + 9 integración, HTTP y pruebas visuales.

### 3.5 GL0C — SSO, concurrencia MFA y consumidor Android

Estado backend: **validado local; no desplegado**. APK: **no publicada; Manuel**.

- SSO vincula proveedor + issuer + subject, no correo automáticamente.
- Políticas Entra/Google, enlace protegido, expiración y revalidación de identidad.
- `MfaVersion`/CAS protegen enrolamiento, recuperación y fallos concurrentes.
- Contrato Android estabilizado para sesión, refresh y desafíos MFA.
- Migración: `20260904125956_GL0C_SsoIdentityMfaConcurrency`.
- Evidencia: 1,215 unitarias + 9 integración, 11 SQL, 30 visuales y 16 Flutter.
- Pendiente: OIDC real, APK/dispositivo y corte coordinado.

### 3.6 GL1A — integridad fiscal

Estado: **validado local; no desplegado**.

- Ambiente fiscal explícito e inmutable en crear/generar/validar/firmar/enviar.
- Coherencia documento–JSON–JWS–credenciales–establecimiento–punto de venta.
- Retorno, invalidación, contingencia y lotes validan empresa/ambiente/origen.
- Estados enviados/procesados/invalidados no se reabren por revalidación.
- Caché MH ligada a empresa/ambiente/usuario/credenciales cifradas.
- Reserva correlativa transaccional sin reset; reportes/NeoProfit filtran PRODUCCIÓN.
- Evidencia: 1,251 unitarias + 9 integración y 13 SQL.

### 3.7 GL1B — idempotencia DTE/NeoConnect/POS

Estado: **validado local; no desplegado**.

- Clave empresa + ámbito + hash persistida atómicamente con el DTE.
- Replay devuelve la misma identidad y no consume correlativo ni retransmite.
- Misma clave con body distinto: `409 / IDEMPOTENCY_CONFLICT`.
- POS usa ámbito interno y vínculo venta–DTE transaccional.
- Contrato para Manuel/integradores: persistir key antes del primer POST y reutilizarla tras timeout.
- Migración: `20260904140909_GL1B_DteIdempotency`.
- Evidencia: 1,276 unitarias + 9 integración y 28 SQL.

### 3.8 GL1C — diagnóstico y recuperación del mismo DTE

Estado: **validado local; no desplegado**.

- Diagnóstico contextual por código, mensaje y ruta; cubre `008/codActividad` y `096/territorio`.
- `ApiResponse<T>` incluye `code`; un error puede devolver el DTE existente en `data`.
- Códigos: `HACIENDA_DATOS_INVALIDOS`, `HACIENDA_RECHAZO`, `DTE_RESULTADO_INCIERTO`,
  `HACIENDA_AUTH_FAILED`.
- Guía con campo, sección, explicación, acción, `traceId` y necesidad de consulta.
- HTTP 200 sin `PROCESADO` y sello no equivale a aceptación.
- Timeout/red/5xx conserva `ENVIADO` e historial; no reenvía a ciegas.
- Evidencia: 1,319 unitarias + 9 integración, 57 focales y 32 SQL.

### 3.9 Revisión multiagente

Estado: **auditoría ejecutada; hallazgos, no correcciones**.

- Separó backend, pruebas, diseño, Web y dos revisiones independientes.
- Detectó B1–B6 y D1–D5: carreras, invalidación incierta, retries, receptor histórico,
  soporte, lotes, guía Web, permisos, búsqueda de retorno, labels y stepper.
- Positivos en fixtures: cinco puntos violetas en `PROCESADO`, visor accesible y cascada territorial
  con ocultación/limpieza para extranjero.
- La cascada sintética no prueba el catálogo real del cliente.
- 1,328 pruebas ordinarias; cuatro `AuditKnownDefect` confirmaban defectos y no eran aceptación.

### 3.10 GL1D — envío individual, SaaS, seguridad y ramas

Estado: **B1/B2/B5 cerrados local/SQL; resto continuó abierto**.

- Exclusión optimista de envío y protección de estados terminales ante respuestas tardías.
- Invalidación no pisa un envío incierto/aceptado concurrentemente.
- Eliminados retries automáticos POST en recepción, eventos, contingencia y lotes.
- Auditoría Git confirmó cambios locales no publicados y riesgo de migración/seed al arrancar.
- Auditorías SaaS/seguridad detectaron trial, downgrade, pagos, SSRF, bootstrap, SameSite y logo.
- Preflight NEO en PRUEBAS y desafío RS512 local; sin autenticación/transmisión MH.
- Evidencia: 1,340 unitarias + 9 integración y 59 SQL.

### 3.11 GL1E — lotes, SSRF, bootstrap, cookies y SaaS

Estado: **validado local; producción NO-GO**.

- Reserva durable de lotes/documentos antes del POST y competencia con otros envíos.
- Resultado perdido no retransmite; consulta tardía no pisa identidad regenerada.
- NeoConnect exige HTTPS/destino público y bloquea redes privadas, proxy, redirects y cookies.
- Bootstrap sin credencial conocida/log; rol central verificado.
- Cookies OIDC `None`, locales `Lax` y antiforgery `Strict`.
- SaaS restringido por tenant/rol; trial finito, GET sin mutación, transferencia POST/antiforgery,
  validación de importe/moneda/plan y bloqueo serializable.
- Migración: `20260904173458_GL1E_LoteAttemptConcurrency` (`Up/Down` vacíos).
- Evidencia: 1,536 unitarias + 9 integración, 221 focales, 83 SQL DTE y 16 SQL SaaS.

### 3.12 GL1F — conciliación, licencias y branding

Estado: **validado local; producción NO-GO**.

- Consulta MH solo para intento enviado/incierto; no retransmite.
- Correlación tenant/NIT/ambiente/tipo/UUID/número; confirma solo evidencia inequívoca.
- Cancelación local inmediata revoca licencia; programada conserva período pagado finito.
- Stripe falla cerrado sin firma válida.
- Branding valida contenido/dimensiones/MIME; blob legado inválido no rompe DTE/PDF/correo.
- Corregida regresión de tracking EF encontrada por suite completa.
- Evidencia: 1,588 unitarias + 9 integración, 44 focales, 75 Billing y 24 SQL Billing.

### 3.13 GL1G — cancelación Billing durable

Estado: **GO local del defecto específico; producción global NO-GO**.

- Intención `Billing_ProviderOperations` antes del proveedor, con snapshot completo e idempotencia.
- Lease único; ACK remoto durable antes del commit local serializable.
- Fallo/timeout/lease sin ACK → `REQUIRES_RECONCILIATION`, sin retry externo ciego.
- Con ACK durable solo completa la fase local; cuarentena no pisa `ACK`/`COMPLETED`.
- Modos opuestos bloqueados; checkout/portal comparten lock de preflight por empresa.
- Migración: `20260905002545_GL1G_BillingProviderOperations`.
- Worker registrado, apagado por defecto.
- Evidencia: build 0/0; 1,616 unitarias; 9 integración; 93 Billing; 26 focales; 63 SQL.
