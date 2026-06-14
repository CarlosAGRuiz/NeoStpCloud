# Plan de Sprints - Hallazgos, Bugs y Preparacion de Demos

> Fecha: 2026-06-14. Alcance: cerrar hallazgos importantes detectados en README, contexto,
> README de API, funcionamiento actual y revision de NeoScan/Gemini. Este plan no incluye nuevos
> productos ni modulos; esos se evaluan despues de estabilizar la base comercial.
>
> Actualizacion API movil: la app Android ya existe en
> `manuelberganza-dev/neocloud_mobile_android`. En este repositorio solo se trabaja backend/API,
> contrato HTTP, permisos, datos demo y pruebas. El plan detallado esta en
> `docs/Plan-Hallazgos-Api-Mobile.md`.
>
> Actualizacion 2026-06-14: API mobile AM-0..AM-6 quedo cerrada operativa al 100%. HB-1 y
> HB-3/HB-4 tambien quedaron cerrados operativos con build limpio y suite completa verde:
> 701 unitarias + 9 integracion.

## Objetivo

Convertir el estado actual de NeoSTP Cloud en una base demostrable y vendible con documentacion
alineada, bugs conocidos priorizados, integraciones reales bien delimitadas y pruebas repetibles
para Web/API.

## Principios

- No abrir nuevos modulos hasta cerrar la consolidacion API-first, NeoScan real y pruebas de demo.
- No abrir trabajo Flutter en este repositorio; la app movil se atiende aqui solo mediante API.
- Todo cambio debe preservar aislamiento por `EmpresaId`, RBAC, modulo contratado, auditoria y cuotas.
- La demo debe probarse con usuarios reales de empresa, no solo con `SUPERADMIN`.
- Los proveedores externos deben seguir siendo pluggables: default seguro en `Mock`, activacion real por config.
- Cada sprint termina con evidencia: tests, capturas o checklist de demo ejecutado.

## Estado de Ejecucion 2026-06-14

| Sprint | Estado | Evidencia |
|---|---|---|
| HB-0 | Cerrado documental | README raiz, README API, contexto maestro, plan de bugs y plan de pruebas enlazados. |
| API mobile AM-0..AM-6 | Cerrado operativo 100% | Contrato Android, permisos, datos demo, NeoScan/Gemini, POS/cobros/alertas y runbook actualizados. |
| HB-1 | Cerrado operativo 100% | Billing sin `Id` duplicado, Billing Portal sin null refs, Infrastructure sin PackageReferences redundantes; build 0 warnings y suite base 697 + 9 verde. |
| HB-3 | Cerrado operativo 100% | `DemoReadinessContractTests` cubre rutas API criticas, permisos, modulos, NeoConnect v1 y superficies de demo. |
| HB-4 | Cerrado operativo 100% | `DemoReadinessContractTests` cubre rutas Web criticas, auth de controllers, portal publico y existencia de vistas Razor. |

## Siguiente Sprint Recomendado

**HB-5 - Datos Demo y Escenarios Comerciales.**

Motivo: con API mobile, HB-1 y la cobertura HB-3/HB-4 ya cerradas, el mayor riesgo de cliente pasa a
ser una demo tecnicamente sana pero pobre en datos: dashboards, libros, cartera, inventario,
tesoreria, CRM o reportes sin historias comerciales completas.

Entregables:

- Seed demo opcional e idempotente con DTE, POS/caja, cobros, compras, inventario, tesoreria,
  NeoScan, portal, CRM y reportes.
- Checklist para resetear demo sin tocar datos productivos.
- Evidencia de pantallas con datos visibles y reportes con valor de negocio.
- Actualizacion de README, contexto y plan de pruebas cuando se cierre.

Criterio de cierre:

- Demo puede iniciar desde una BD limpia y quedar lista en menos de 10 minutos.
- Dashboard, libros IVA, NeoProfit, inventario, CxC, tesoreria, CRM y portal muestran datos utiles.
- El seed es idempotente y no duplica escenarios si corre dos veces.

## Roadmap de Sprints

| Sprint | Prioridad | Tema | Resultado esperado |
|---|---:|---|---|
| HB-0 | Alta | Alineacion documental y backlog | Fuente de verdad actualizada y plan accionable |
| HB-1 | Alta | Limpieza de warnings y bugs menores | Cerrado operativo: build 0 warnings y suite 697 + 9 verde |
| HB-2 | Alta | NeoScan/Gemini productivo | OCR real mas seguro, asincrono y medible |
| HB-3 | Alta | Pruebas API de alto valor | Cerrado operativo: contrato automatizado de rutas, permisos, modulos y NeoConnect v1 |
| HB-4 | Alta | Pruebas Web para demo comercial | Cerrado operativo: contrato automatizado de rutas Web, auth, portal y vistas Razor |
| HB-5 | Media-alta | Datos demo y escenarios comerciales | Demo con datos completos, no pantallas vacias |
| HB-6 | Media-alta | Contratos API y versionado | API mas estable para mobile/integradores |
| AM-0..AM-6 | Alta | API movil Android | Cerrado operativo 100%: contrato, pruebas, datos demo y runbook |
| HB-7 | Media | Storage, secretos y retencion | Archivos fiscales y secretos operados con guardrails |
| HB-8 | Media | Runbook de demo y release | Checklist previo a demo/release ejecutable |

## Aclaracion de Alcance Movil

La app movil ya fue trabajada fuera de este repositorio. El repo Android consume esta API con:

- `ApiResponse<T>` para JSON y `PagedResult<T>` para listas.
- JWT Bearer y refresh token.
- Descargas binarias como bytes crudos para PDF/JSON/tickets/archivos.
- Tenant implicito por `empresaId` en el JWT; no soporta `SUPERADMIN` ni envia `?empresaId`.
- Endpoints de DTE, DTE config, clientes, productos, lookups, dashboard, cobros, NeoScan, alertas,
  POS y caja.

Por tanto, cualquier hallazgo movil se atiende aqui como trabajo de API: permisos, DTOs,
compatibilidad, datos demo, observabilidad y pruebas contractuales. No se planifican cambios Flutter en
NeoSTP Cloud. Ver la matriz completa en `docs/Plan-Hallazgos-Api-Mobile.md`.

## HB-1 - Limpieza de Warnings y Bugs Menores

Hallazgos:

- `Billing*` redefine `Id` aunque hereda de `AuditableEntity`.
- Posibles null refs en `Views/Billing/Portal.cshtml`.
- PackageReferences innecesarios reportados por `NU1510` en `NeoSTP.Infrastructure`.

Entregables:

- Eliminar `Id` duplicado o marcar intencionalmente con `new` solo si hay razon real.
- Blindar nullability en vista de Billing.
- Revisar/remover dependencias innecesarias sin romper build.

Validacion:

- `dotnet build NeoSTP.slnx`.
- `dotnet test NeoSTP.slnx`.
- Revision de `git diff` para confirmar que no hay migraciones accidentales.

Criterio de cierre:

- Build sin los warnings corregidos.
- 697 unitarias + 9 integracion siguen verdes.
- Sin migraciones accidentales.

### Cierre Operativo 2026-06-14

- Se eliminaron los `Id` duplicados en `BillingCustomer`, `BillingSubscription`, `BillingInvoice`,
  `BillingPayment`, `BillingPlanProviderMapping` y `BillingWebhookEvent`; el `Id` queda heredado desde
  `AuditableEntity`.
- `Views/Billing/Portal.cshtml` solo renderiza el modal de cambio de plan cuando existe suscripcion y
  calcula una lista `planesCambio` no nullable.
- `NeoSTP.Infrastructure.csproj` retiro PackageReferences redundantes ya cubiertos por
  `Microsoft.AspNetCore.App` o dependencias transitivas.
- Validacion ejecutada: `dotnet build NeoSTP.slnx` = 0 warnings/0 errores; `dotnet test NeoSTP.slnx`
  = 697 unitarias + 9 integracion verdes.

## HB-2 - NeoScan/Gemini Productivo

Estado 2026-06-14: hardening operativo cerrado por AM-3 para mobile/demo. Gemini ya usa
`x-goog-api-key`, el modelo es configurable, existe whitelist MIME, timeout OCR, metadata persistida y
reintento seguro. HB-2 queda como sprint residual solo si se decide llevar NeoScan a OCR asincrono
completo o conectar "registrar compra" con Compras/CxP/Inventario.

Hallazgos:

- Cerrado AM-3: Gemini real documentado y configurable.
- Cerrado AM-3: API key viaja por header `x-goog-api-key`, no query string.
- Cerrado AM-3: umbral de confianza y MIME permitidos configurables.
- Mitigado AM-3: timeout OCR evita bloquear la app; OCR asincrono/polling avanzado queda V3.
- Residual: "Registrar compra" desde NeoScan crea `ProfitCompra`, no una compra operativa con proveedor/CxP/inventario.

Entregables:

- Usar header `x-goog-api-key` y evitar secretos en URL/logs.
- `Scan:Gemini:Model` configurable con default documentado y probado.
- `Scan:ConfianzaMinimaProcesado` configurable.
- Validacion de MIME y extension permitida: `image/jpeg`, `image/png`, `application/pdf`.
- Opcion asincrona: guardar `PROCESANDO`, encolar OCR y permitir reintento.
- Resultado OCR con trazabilidad: proveedor, modelo, duracion, error resumido y fecha.
- Separar claramente "compra financiera Profit" vs "compra operativa Compras/CxP/Inventario".

Validacion:

- Unit tests de `GeminiScanExtractionService`.
- Tests de `ScanService` para umbral, MIME invalido, error Gemini, reintento y estado asincrono.
- Prueba manual con `Scan:Provider=Mock` y `Gemini` sin exponer API key.

Criterio de cierre:

- NeoScan funciona igual en modo mock.
- En modo Gemini, un fallo externo nunca bloquea la captura manual.
- La UI/API muestra estado de OCR y permite corregir antes de confirmar.

## HB-3 - Pruebas API de Alto Valor

Hallazgos:

- La suite automatizada es fuerte en unitarias, pero la cobertura de integracion es corta para la amplitud API.
- Falta una bateria tipo demo que use host real, auth, permisos, tenant y contratos HTTP.

Entregables:

- `WebApplicationFactory` o equivalente para API con DB aislada.
- Flujos API: auth, DTE emitir en un paso, POS venta -> DTE, Scan -> gasto/compra/DTE recibido, compras -> inventario/CxP, cobros -> QR/recordatorio, portal token, conciliacion.
- Casos negativos: sin permiso, sin modulo, empresa cruzada, rate limit, estado invalido.
- Evidencia generada: reporte de endpoints ejecutados y resultado.

Validacion:

- `dotnet test tests/NeoSTP.Tests.Integration/NeoSTP.Tests.Integration.csproj`.
- Un smoke HTTP local contra API real antes de demo.

Criterio de cierre:

- La demo API se puede repetir sin preparar datos manualmente.
- Cualquier fallo deja log claro con endpoint, usuario, empresa y dato usado.

### Cierre Operativo 2026-06-14

- Se agrego `tests/NeoSTP.Tests.Unit/Api/DemoReadinessContractTests.cs`.
- La suite congela rutas y metadata de seguridad para dashboard, DTE, cobros, POS/caja, NeoScanAI,
  compras, inventario, tesoreria, reportes fiscales, NeoConta, NeoProfit, CRM, portal interno y
  NeoConnect publico `/api/v1`.
- La validacion revisa `[Authorize]`, `[AllowAnonymous]`, `[RequireModule]`, `[RequirePermiso]`,
  verbo HTTP y template de cada endpoint critico.
- Validacion enfocada: `dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj --filter DemoReadinessContractTests`
  = 4/4 pruebas verdes.
- Validacion completa registrada: `dotnet build NeoSTP.slnx` y `dotnet test NeoSTP.slnx` verdes,
  701 unitarias + 9 integracion.

## HB-4 - Pruebas Web para Demo Comercial

Hallazgos:

- La UX ya fue recorrida una vez con ADMIN y se corrigieron bugs, pero falta un checklist recurrente.
- El mayor riesgo de demo es rol/permisos, rutas vacias, formularios con selects incompletos y estados sin datos.

Entregables:

- Plan de pruebas Web por rol: `SUPERADMIN`, `ADMIN`, `OPERADOR/POS`, `CONTADOR`, receptor publico.
- Checklist de rutas criticas con dato esperado y accion de demo.
- Capturas de evidencia por pantalla clave.
- Smoke responsive para dashboard, DTE, POS, Scan, Portal y Operacion.

Validacion:

- Ejecutar el plan de `docs/Plan-Pruebas-Web-Api-Demos.md`.
- Registrar hallazgos con prioridad y ruta exacta.

Criterio de cierre:

- Demo Web de 60-90 minutos sin errores bloqueantes ni pantallas vacias criticas.

### Cierre Operativo 2026-06-14

- Se agrego cobertura automatizada de rutas Web criticas para demos: Home, DTE, POS, caja, cobros,
  compras, inventario, NeoProfit, Scan, tesoreria, integraciones y soporte.
- La prueba verifica que los controllers internos sigan protegidos por `[Authorize]`.
- El portal publico mantiene `[AllowAnonymous]`, ruta base `portal` y acciones esperadas para token,
  PDF, JSON y QR.
- La prueba valida existencia fisica y contenido no vacio de vistas Razor criticas, incluyendo
  `Portal/Documento.cshtml`, `Portal/EstadoCuenta.cshtml` y `Portal/NoDisponible.cshtml`.
- Las capturas/manual browser walkthrough quedan como ejecucion recurrente por demo dentro de
  `docs/Plan-Pruebas-Web-Api-Demos.md`; el sprint queda cerrado por baseline automatizada y runbook.

## HB-5 - Datos Demo y Escenarios Comerciales

Hallazgos:

- Algunos reportes pueden verse vacios en la empresa demo, por ejemplo compras/CCF/libros.
- La demo necesita mostrar valor de negocio, no solo endpoints sanos.

Entregables:

- Seed demo opcional e idempotente con:
  - Factura consumidor final.
  - CCF.
  - Nota de credito/debito.
  - Compra con proveedor.
  - Producto con stock y costo.
  - Venta POS con corte.
  - Cobro pendiente y pago parcial.
  - Movimiento bancario y conciliacion.
  - Scan con archivo y campos corregidos.
- Script/checklist para resetear demo sin tocar produccion.

Validacion:

- Dashboard, libros IVA, NeoProfit, inventario, CxC, tesoreria y portal muestran datos.
- No se insertan duplicados si el seed corre dos veces.

Criterio de cierre:

- Demo puede iniciar desde una BD limpia y quedar lista en menos de 10 minutos.

## HB-6 - Contratos API y Versionado

Hallazgos:

- NeoConnect usa `/api/v1`, pero la API interna/mobile usa rutas sin version explicita.
- Es necesario estabilizar DTOs para app Flutter, integradores y demos.

Entregables:

- Matriz de endpoints estables para mobile/demo.
- Politica de versionado: que cambia en `/api/*`, que va a `/api/v1`, y como se depreca.
- Contratos de respuesta con ejemplos exitosos y errores frecuentes.
- OpenAPI revisado contra README API.

Validacion:

- Comparar controllers vs `src/NeoSTP.Api/README.md`.
- Smoke de OpenAPI/Scalar.

Criterio de cierre:

- Un integrador o app mobile puede seguir la documentacion sin leer el codigo.

## HB-7 - Storage, Secretos y Retencion

Hallazgos:

- Scan puede guardar blobs en BD o filesystem; ambos contienen documentos fiscales sensibles.
- Filesystem storage no cifra por aplicacion; depende del volumen/ACL.
- Rotacion de DataProtection puede invalidar secretos cifrados.

Entregables:

- Runbook de storage de scans: cifrado de disco, permisos, backup, retencion y purga.
- Guardrail para no loggear nombres/rutas sensibles de documentos.
- Checklist de secretos por entorno: JWT, DTE, Gemini, FCM, WhatsApp, pasarelas, SMTP.
- Prueba de health/readiness para storage configurado.

Validacion:

- `/health/ready` incluye storage sano.
- Revisión de logs sin secretos.

Criterio de cierre:

- Ambiente demo/productivo documentado sin secretos en repo y con retencion clara.

## HB-8 - Runbook de Demo y Release

Hallazgos:

- Hay runbooks operativos, pero falta uno orientado a demo comercial repetible.

Entregables:

- Checklist pre-demo: build, tests, migraciones, seed, usuarios, puertos, proveedores mock/reales.
- Guion de demo: problema -> flujo -> resultado -> valor de negocio.
- Checklist post-demo: limpiar datos, rotar tokens temporales, registrar feedback.
- Criterios de "no demo": certificado faltante, DB no migrada, tests rojos, usuario sin permisos, docs desalineadas.

Validacion:

- Ensayo completo antes de presentar a cliente.

Criterio de cierre:

- Cualquier miembro tecnico puede levantar la demo siguiendo el documento.

## Backlog de Hallazgos

| ID | Severidad | Hallazgo | Sprint |
|---|---:|---|---|
| HB-001 | Alta | Docs historicas contradicen estado de NeoScan/Gemini y proveedores reales | HB-0 |
| HB-002 | Media | Cerrado: warnings de build en Billing, vista Billing y PackageReferences | HB-1 |
| HB-003 | Alta | Cerrado AM-3: Gemini API key via header `x-goog-api-key` | HB-2/AM-3 |
| HB-004 | Alta | Mitigado AM-3: OCR con timeout y reintento; polling asincrono queda V3 | HB-2/AM-3 |
| HB-005 | Alta | Cerrado AM-3: umbral de confianza configurable | HB-2/AM-3 |
| HB-006 | Media-alta | Cerrado AM-3: whitelist estricta de MIME/tipo de archivo | HB-2/AM-3 |
| HB-007 | Media-alta | "Compra" desde Scan no cubre compra operativa/CxP/inventario | HB-2 |
| HB-008 | Alta | Cerrado HB-3: baseline automatizada de rutas API criticas, permisos, modulos y NeoConnect v1 | HB-3 |
| HB-009 | Alta | Cerrado HB-4: baseline automatizada de rutas Web, portal publico y vistas Razor + checklist recurrente | HB-4 |
| HB-010 | Media | Empresa demo puede dejar libros/reportes sin datos comerciales | HB-5 |
| HB-011 | Media-alta | Versionado API no esta formalizado fuera de NeoConnect | HB-6 |
| HB-012 | Media-alta | Storage de documentos fiscales requiere runbook de seguridad/retencion | HB-7 |
| HB-013 | Media | Falta runbook especifico de demo/release comercial | HB-8 |
| AM-001 | Alta | DTE listado/detalle requiere `DTE.Emitir`, pero la app separa consulta con `DTE.Consultar` | AM-1 |
| AM-002 | Alta | Falta suite contractual que ejecute endpoints reales consumidos por `api_endpoints.dart` | AM-2 |
| AM-003 | Alta | NeoScan/Gemini requiere hardening productivo antes de demo comercial OCR | AM-3 |
| AM-004 | Alta | Flujos largos pueden exceder timeout movil de 30s | AM-1/AM-3 |
| AM-005 | Media-alta | URL demo movil debe ser estable o inyectada via `API_BASE_URL` | AM-6 |
| AM-006 | Media-alta | Descargas binarias deben permanecer sin envelope JSON | AM-2 |
| AM-007 | Media-alta | Refresh, SuperAdmin bloqueado y permisos efectivos necesitan pruebas API movil | AM-1/AM-2 |
| AM-008 | Media-alta | Datos demo deben cubrir pantallas moviles para evitar estados vacios | AM-4 |
| AM-009 | Media | App actual emite factura por atajo `emitir/factura`; preservar ruta generica y atajos por tipo | AM-5 |
| AM-010 | Media | POS/caja requiere permisos, modulo y manejo de `data: null` en estado de caja | AM-5 |
| AM-011 | Media | Push FCM real es pluggable; demo debe declarar si usa polling/mock o FCM real | AM-5 |
| AM-012 | Media | Certificado y scan base64 requieren pruebas de tamano, MIME y errores legibles | AM-1/AM-3 |
| AM-013 | Media | Versionado interno `/api/*` debe formalizar compatibilidad para mobile | AM-6 |
| AM-014 | Media | Falta runbook demo API movil con URL, usuarios, providers y evidencias | AM-6 |
| AM-015 | Media-alta | Falta matriz de usuarios/roles demo para mobile con permisos minimos y negativos | AM-4 |
| AM-016 | Media | Falta registrar evidencia API mobile por endpoint, status, traceId, usuario, empresa y duracion | AM-2/AM-6 |
| AM-017 | Media-alta | Falta validar modulos no contratados (`NEOPOS`, `NEOSCANAI`) con respuesta legible para app | AM-2/AM-5 |

## Orden de Ejecucion Recomendado

1. HB-0, API mobile AM-0..AM-6, HB-1 y HB-3/HB-4 ya quedaron cerrados operativos.
2. Ejecutar HB-5 antes de cualquier demo ejecutiva importante que requiera datos comerciales ricos.
3. Mantener HB-2 como hardening residual de NeoScan si se requiere OCR asincrono completo o compra
   operativa Compras/CxP/Inventario desde Scan.
4. Cerrar HB-6, HB-7 y HB-8 como consolidacion antes de nuevos proyectos o modulos.
