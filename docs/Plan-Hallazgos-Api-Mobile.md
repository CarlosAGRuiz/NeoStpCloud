# Plan de Hallazgos API para NeoCloud Mobile

> Fecha: 2026-06-14. Alcance: backend/API de NeoSTP Cloud para la app Android existente
> `manuelberganza-dev/neocloud_mobile_android`. En este repositorio no se trabaja Flutter; se trabaja
> contrato HTTP, permisos, datos demo, observabilidad, documentacion y pruebas de API.

## Fuente revisada

Repositorio movil revisado:

- `https://github.com/manuelberganza-dev/neocloud_mobile_android`
- Rama revisada: `main`
- Archivos clave: `README.md`, `pubspec.yaml`, `lib/core/config/app_config.dart`,
  `lib/core/network/api_client.dart`, `lib/core/network/api_endpoints.dart` y repositorios/modelos
  de `auth`, `invoice`, `dte_query`, `dte_config`, `dte_delivery`, `clients`, `dashboard`,
  `collections`, `neoscan`, `notifications` y `pos`.

## Estado de Ejecucion 2026-06-14

| Sprint | Estado | Evidencia |
|---|---|---|
| AM-0 | Cerrado operativo 100% | Matriz de endpoints, permisos, DTOs y contratos inmutables en este documento. |
| AM-1 | Cerrado operativo 100% | Lista/detalle DTE usan `DTE.Consultar`; pruebas `DteControllerMobileContractTests`. |
| AM-2 | Cerrado operativo 100% | Suite contractual por reflexion + integracion `MobileApiContractOperationalTests` con fixture mobile demo, MAPI-01..MAPI-23 y shape camelCase. |
| AM-3 | Cerrado operativo 100% | Gemini usa `x-goog-api-key`; NeoScan valida MIME, timeout OCR, metadata persistida y reintento `POST /reprocesar`; migracion `Sprint_AM3_ScanOcrOperationalMetadata`. |
| AM-4 | Cerrado operativo 100% | `EmpresaPrueba:MobileDemo:Enabled` crea usuarios, modulos, DTE/CxC/POS/Scan y alertas pendiente+resuelta idempotentes. |
| AM-5 | Cerrado operativo 100% | Runbook de POS, Cobros y Alertas en `docs/Runbook-Api-Mobile-Demo.md`. |
| AM-6 | Cerrado operativo 100% | Runbook de URL, providers, versionado y evidencias en `docs/Runbook-Api-Mobile-Demo.md`. |

Estado de validacion acumulado 2026-06-14: `dotnet build NeoSTP.slnx` quedo en 0 warnings/0 errores y
`dotnet test NeoSTP.slnx` quedo verde con 701 unitarias + 9 integracion. HB-1 no cambio contratos HTTP
mobile; HB-3/HB-4 agregaron baseline automatizada de demo readiness sin romper mobile.

Pendiente deliberado: OCR asincrono/polling avanzado de NeoScan queda como mejora V3. El contrato actual
queda operativo para mobile con modo rapido: `Scan:OcrTimeoutSeconds` corta el intento OCR antes del timeout
de la app, Gemini degrada a captura manual ante error y el mismo documento puede reprocesarse sin duplicarse.

## Resumen Ejecutivo

La app movil ya consume una API amplia y estable de NeoSTP:

- Auth JWT con refresh token.
- Envelope `ApiResponse<T>` y paginacion `PagedResult<T>`.
- DTE: emision en un paso, listado, detalle, PDF, JSON y reenvio.
- Configuracion DTE: credenciales MH y certificado base64.
- Clientes/productos/lookups.
- Dashboard empresa.
- Cobros, pagos y QR.
- NeoScanAI con subida base64 y conversion a gasto, compra o DTE recibido.
- Alertas, preferencias y registro de dispositivo push.
- POS, caja, ticket PDF, envio y promocion a DTE.

El contrato general calza con la API actual y los riesgos iniciales del plan quedaron cerrados:

- Permisos de lectura DTE corregidos para consulta con `DTE.Consultar`.
- Suite automatizada contractual contra endpoints consumidos por la app.
- NeoScan/Gemini endurecido: header `x-goog-api-key`, whitelist MIME, timeout, metadata y reintento.
- Flujos largos protegidos por timeout OCR y degradacion a captura manual.
- URL demo, providers, usuarios y evidencias documentadas en el runbook.

## Principios

- No abrir trabajo Flutter desde este repositorio.
- Preservar `ApiResponse<T>` para JSON y bytes crudos para descargas (`pdf`, `json`, `ticket`,
  `archivo`) donde la app llama `getBytes`.
- Mantener tenant implicito por JWT para usuarios de empresa; la app no envia `empresaId` ni soporta
  `SUPERADMIN`.
- Toda prueba de API movil debe correr con usuarios reales de empresa, permisos reales y modulos
  contratados.
- Proveedores externos siguen pluggable: `Mock` por defecto, reales por configuracion.

## Contrato que Consume la App

| Area movil | Endpoints principales | Contrato esperado |
|---|---|---|
| Health | `GET /health` | JSON con `status` y `service` dentro de `data`. |
| Auth | `/api/auth/login`, `refresh`, `logout`, `me`, MFA | `accessToken`, `refreshToken`, expiraciones y `user` con `empresaId`, `roles`, `permisos`. |
| DTE config | `/api/dte/configuracion`, `/certificado`, `/probar-conexion` | JSON base64 para certificado; secretos nunca regresan. |
| DTE emision | `/api/dte/emitir`, `/emitir/factura`, `/emitir/credito-fiscal`, `/emitir/nota-*` | Request `CreateDteDocumentoRequest`; backend calcula totales y transmite. |
| DTE consulta | `/api/dte/documentos`, `/{id}`, `/pdf`, `/json`, `/reenviar` | Lista paginada, detalle JSON, descargas como bytes crudos. |
| Maestros | `/api/clientes`, `/api/productos`, `/api/lookups/*` | CRUD/paginacion y lookups livianos para autocompletes. |
| Dashboard | `/api/dashboard/empresa` | KPIs DTE y plan mensual. |
| Cobros | `/api/cobros/resumen`, `pendientes`, `dte/{id}/pagos`, `qr`, `cuentas` | CxC derivada, pagos, QR base64. |
| NeoScan | `/api/scanai/documentos/*` | Subida base64, campos extraidos, confirmaciones, archivo como bytes. |
| Alertas | `/api/alertas/*`, `/dispositivos`, `/preferencias` | Badges, centro de alertas, token FCM/ANDROID. |
| POS/caja | `/api/pos/ventas/*`, `/api/pos/caja/*` | Ventas, ticket bytes, caja nullable en estado actual. |

## Contratos Inmutables para Mobile

Estos contratos no deben cambiar sin versionar y coordinar con la app Android:

| Contrato | Regla |
|---|---|
| Envelope JSON | Todo JSON sale como `ApiResponse<T>` con `success`, `message`, `data`, `errors`, `traceId`. |
| Paginacion | Listados moviles usan `PagedResult<T>` con `items`, `total`, `page`, `pageSize`, `totalPages`. |
| Descargas | PDF DTE, JSON DTE, ticket POS y archivo NeoScan salen como bytes crudos, no `ApiResponse`. |
| Auth | Login/refresh devuelven `accessToken`, `refreshToken`, expiraciones y `user`. |
| Tenant | Mobile usa usuarios de empresa. No se envia `?empresaId`; `empresaId` viene en JWT. |
| SuperAdmin | La app lo bloquea localmente; la API debe seguir devolviendo `AUTH_NO_TENANT` si intenta operar sin empresa. |
| Errores | Todo error de negocio debe traer `traceId` y codigos estables en `errors`. |
| Timeouts | Endpoints de accion usados por mobile deben responder dentro de 30s o quedar como flujo asincrono/polling. |
| Providers | `Mock` sigue siendo default seguro; providers reales no cambian contrato HTTP. |

## Matriz Endpoint-Permiso-Modulo

| Pantalla/flujo movil | Endpoint | Permiso API | Modulo | Estado del contrato |
|---|---|---|---|---|
| Splash/conectividad | `GET /health` | anon | n/a | OK. |
| Login | `POST /api/auth/login` | anon | n/a | OK. |
| Sesion | `GET /api/auth/me`, `POST /api/auth/refresh`, `POST /api/auth/logout` | usuario autenticado | n/a | OK; suite contractual mobile. |
| Dashboard | `GET /api/dashboard/empresa` | usuario autenticado | n/a | OK; datos demo mobile-first. |
| Config DTE | `GET/PUT /api/dte/configuracion` | `DTE.Configurar` | NEODTE/licencia de empresa | OK. |
| Certificado DTE | `POST/DELETE /api/dte/configuracion/certificado` | `DTE.Configurar` | NEODTE/licencia de empresa | OK; errores de archivo documentados para mobile. |
| Prueba MH | `POST /api/dte/configuracion/probar-conexion` | `DTE.Configurar` | NEODTE/licencia de empresa | OK; provider puede ser Mock/Http. |
| Emitir factura | `POST /api/dte/emitir/factura` | `DTE.Emitir` | NEODTE/licencia de empresa | OK; medir duracion. |
| Emitir generico | `POST /api/dte/emitir` | `DTE.Emitir` | NEODTE/licencia de empresa | OK; preservar para tipos futuros. |
| Consultar DTE | `GET /api/dte/documentos`, `GET /api/dte/documentos/{id}` | `DTE.Consultar` | NEODTE/licencia de empresa | Cerrado AM-1. |
| Descargar DTE | `GET /api/dte/documentos/{id}/pdf`, `/json` | `DTE.Consultar` | NEODTE/licencia de empresa | OK; bytes crudos. |
| Reenviar DTE | `POST /api/dte/documentos/{id}/reenviar` | `DTE.Reenviar` | NEODTE/licencia de empresa | OK; validar error email. |
| Clientes | `GET/POST/PUT /api/clientes`, `PATCH /inactivar`, `PATCH /etiqueta` | `Clientes.Ver/Crear/Editar` | CORE | OK. |
| Productos | `GET/POST/PUT /api/productos`, `PATCH /inactivar` | `Productos.Ver/Crear/Editar` | CORE | OK. |
| Lookups | `/api/lookups/clientes`, `/productos`, `/sucursales`, territoriales, `verificar-nit` | usuario autenticado | CORE | OK. |
| Cobros lectura | `/api/cobros/resumen`, `pendientes`, `clientes/{id}`, `dte/{id}/pagos`, `cuentas`, `qr` | `Cobros.Ver` | CORE/NEODTE | OK; `qr` usa permiso de lectura. |
| Cobros gestion | pagos, confirmar/anular, cuentas CRUD | `Cobros.Gestionar` | CORE/NEODTE | OK. |
| NeoScan bandeja | `GET/POST /api/scanai/documentos`, `GET /{id}`, `GET /{id}/archivo`, `PUT /campos`, `POST /reprocesar`, `POST /resultado` | `ScanAI.Ver` | `NEOSCANAI` | OK; hardening AM-3 operativo. |
| NeoScan confirmar | `registrar-gasto`, `registrar-compra`, `registrar-dte-recibido`, `rechazar` | `ScanAI.Confirmar` | `NEOSCANAI` | OK; probar estados invalidos. |
| Alertas | `/api/alertas/*`, `/dispositivos`, `/preferencias` | usuario autenticado | CORE | OK; FCM real opcional. |
| POS ventas | `/api/pos/ventas`, detalle, anular, ticket, enviar, resumen | `Pos.Ver`, `Pos.Vender`, `Pos.Anular` | `NEOPOS` | OK; ticket bytes crudos. |
| POS promover | `POST /api/pos/ventas/{id}/promover` | `DTE.Emitir` | `NEOPOS` + NEODTE | OK; validar error fiscal controlado. |
| POS caja | `/api/pos/caja/*` | `Pos.Ver`, `Pos.Vender` | `NEOPOS` | OK; `estado` puede traer `data: null`. |

## Perfiles de Prueba API Mobile

| Perfil | Uso | Permisos minimos |
|---|---|---|
| `MOBILE_ADMIN` | Smoke completo de empresa | `DTE.Emitir`, `DTE.Consultar`, `DTE.Reenviar`, `DTE.Configurar`, `Clientes.*`, `Productos.*`, `Cobros.*`, `ScanAI.*`, `Pos.*`. |
| `MOBILE_DTE_CONSULTA` | Validar bug AM-001 y lectura sin emitir | `DTE.Consultar`, opcional `DTE.Reenviar`. Sin `DTE.Emitir`. |
| `MOBILE_OPERADOR_POS` | POS/caja/ticket/promocion | `Pos.Ver`, `Pos.Vender`, `DTE.Emitir`, `Clientes.Ver`, `Productos.Ver`. |
| `MOBILE_COBROS` | CxC, pagos y QR | `Cobros.Ver`, `Cobros.Gestionar`, `DTE.Consultar`, `Clientes.Ver`. |
| `MOBILE_SCAN` | NeoScan captura y confirmacion | `ScanAI.Ver`, `ScanAI.Confirmar`, `Profit.Gestionar` si se confirma gasto/compra. |
| `MOBILE_LIMITADO` | Casos negativos de 403/402 | Usuario sin permisos o empresa sin modulo `NEOSCANAI`/`NEOPOS`. |

## Hallazgos Priorizados

| ID | Severidad | Hallazgo | Impacto | Sprint |
|---|---:|---|---|---|
| AM-001 | Alta | `GET /api/dte/documentos` y `GET /api/dte/documentos/{id}` requerian `DTE.Emitir`; la app y el contrato de lectura esperan `DTE.Consultar`. | Cerrado: usuarios de solo consulta pueden listar/detallar. | AM-1 |
| AM-002 | Alta | No habia suite automatizada especifica que cubriera endpoints exactos de `api_endpoints.dart`. | Cerrado operativo 100%: cobertura contractual por controllers/rutas/permisos/modulos e integracion MAPI-01..MAPI-23 con datos demo. | AM-2 |
| AM-003 | Alta | NeoScan con Gemini necesitaba hardening: API key por query, umbral de confianza permisivo y MIME whitelist pendiente. | Cerrado operativo 100%: header seguro, MIME whitelist, umbral, metadata OCR, timeout y reintento seguro. | AM-3 |
| AM-004 | Alta | Flujos largos pueden superar el timeout movil de 30s (`receiveTimeout`). | Cerrado operativo 100%: `Scan:OcrTimeoutSeconds` limita OCR y degrada a `REQUIERE_REVISION` con `OCR_TIMEOUT`. | AM-1/AM-3 |
| AM-005 | Media-alta | URL demo movil depende de tunnel temporal si no se inyecta `API_BASE_URL`. | Cerrado: runbook define URL/tunnel y override `API_BASE_URL`. | AM-6 |
| AM-006 | Media-alta | Descargas binarias deben permanecer sin envelope. | Cerrado: contrato documentado y cubierto por suite contractual. | AM-2 |
| AM-007 | Media-alta | Auth refresh, bloqueo SuperAdmin y permisos efectivos requieren pruebas contractuales. | Cerrado: auth/session/permisos cubiertos por AM-1/AM-2. | AM-1/AM-2 |
| AM-008 | Media-alta | Datos demo deben cubrir DTE, CxC, QR, NeoScan, POS/caja y alertas; si no, pantallas moviles quedan vacias. | Cerrado operativo 100%: seeder mobile opt-in e idempotente, con alerta pendiente y resuelta. | AM-4 |
| AM-009 | Media | `POST /api/dte/emitir/factura` es el flujo movil real actual; la app muestra mas tipos, pero su request de factura hardcodea `01`. | Cerrado: preservar ruta generica y atajos por tipo queda como contrato. | AM-5 |
| AM-010 | Media | POS/caja depende de modulo `NEOPOS`, permisos y caja activa; estado de caja puede ser `data: null`. | Cerrado: seed/permisos/runbook documentan positivo y null controlado. | AM-5 |
| AM-011 | Media | Push real FCM es pluggable; polling de alertas funciona sin FCM. | Cerrado: runbook declara Mock/FCM y no promete push real sin credenciales. | AM-5 |
| AM-012 | Media | Certificado DTE y Scan usan base64 en JSON; faltan pruebas de tamano, MIME y errores legibles para movil. | Cerrado: NeoScan valida MIME y certificado/archivo queda en checklist de pruebas. | AM-1/AM-3 |
| AM-013 | Media | Versionado interno `/api/*` no esta formalizado; NeoConnect si usa `/api/v1`. | Cerrado: politica de compatibilidad mobile documentada en AM-6. | AM-6 |
| AM-014 | Media | Faltaba runbook de demo API movil: URL, usuario, permisos, proveedor mock/real, health y evidencias. | Cerrado: `docs/Runbook-Api-Mobile-Demo.md`. | AM-6 |
| AM-015 | Media-alta | Faltaba matriz de usuarios/roles demo para mobile con permisos minimos y negativos. | Cerrado operativo 100%: usuarios `mobile.*` sembrados por `MobileDemo` y modulos CORE/NEODTE/NEOPOS/NEOSCANAI/NEOPROFIT/NEOPORTAL activos. | AM-4 |
| AM-016 | Media | Falta registrar evidencia por endpoint: status, traceId, usuario, empresa, duracion y payload resumido. | Cerrado operativo 100%: runbook + manifiesto MAPI-01..MAPI-23 en prueba de integracion. | AM-2/AM-6 |
| AM-017 | Media-alta | Falta validar modulos no contratados (`NEOPOS`, `NEOSCANAI`) con respuesta legible para app. | Cerrado operativo: negativos de modulo quedan en suite/runbook. | AM-2/AM-5 |

## Siguiente Sprint Recomendado

**Ninguno dentro del plan API mobile: AM-0..AM-6 esta cerrado operativo al 100%.**

Siguiente trabajo recomendado en este repositorio: ejecutar HB-5 del plan de hallazgos general
para producir datos demo comerciales completos. Mobile queda en modo mantenimiento de contrato: cualquier cambio
futuro debe conservar `ApiResponse<T>`, `PagedResult<T>`, bytes crudos, tenant por JWT y providers
pluggables.

Entregables:

- Mantener el runbook de demo API mobile actualizado por ambiente.
- Ejecutar la suite mobile contractual cuando cambien controllers, auth, permisos, DTOs o descargas.
- Registrar cualquier nuevo hallazgo mobile en este documento antes de tocar Flutter.

Criterio de cierre:

- `dotnet test NeoSTP.slnx` verde.
- README raiz, README API, contexto y runbook mobile actualizados en el mismo commit de cierre.

## Roadmap de Sprints

| Sprint | Prioridad | Tema | Resultado esperado |
|---|---:|---|---|
| AM-0 | Alta | Baseline contrato movil | Matriz de endpoints, permisos, DTOs y datos requeridos contra repo Android. |
| AM-1 | Alta | Compatibilidad critica API movil | Bugs de permisos, auth, bytes y timeouts cerrados. |
| AM-2 | Alta | Suite contractual mobile API | Tests de integracion que ejecutan endpoints consumidos por la app. |
| AM-3 | Alta | NeoScan/Gemini productivo para movil | OCR seguro, medible, preferiblemente asincrono y compatible con timeout movil. |
| AM-4 | Media-alta | Datos demo mobile-first | Empresa demo con datos visibles en dashboard, DTE, cobros, POS, NeoScan y alertas. |
| AM-5 | Media-alta | POS, Cobros y Alertas para demo | Flujos moviles de venta, caja, QR y centro de alertas repetibles. |
| AM-6 | Media | Runbook y versionado API movil | URL demo estable, politica de cambios y checklist antes de demo/release. |

## Definition of Done del Plan API Mobile

Plan cerrado operativo 100% el 2026-06-14:

- AM-1 dejo bugfixes y pruebas verdes.
- AM-2 dejo una suite contractual repetible con rutas/permisos/modulos y fixture mobile demo.
- AM-3 dejo NeoScan seguro para demo con provider `Mock` y Gemini real configurable.
- AM-4 dejo empresa demo con datos moviles visibles e idempotentes.
- AM-5 dejo POS/cobros/alertas con flujos positivos y negativos documentados.
- AM-6 dejo runbook de demo API mobile con URL, usuarios, variables, providers y evidencias.
- README raiz, README API, `NeoCloud-Mobile-API.md` y OpenAPI/Scalar quedan alineados.
- No hay trabajo Flutter pendiente dentro de NeoSTP Cloud.

## Plan de Ejecucion por Orden

| Orden | Sprint | Por que va ahi | Salida concreta |
|---:|---|---|---|
| 1 | AM-1 | Cerrado. | Contrato base compatible y probado. |
| 2 | AM-2 | Cerrado. | Tests contractuales y reporte de cobertura. |
| 3 | AM-4 | Cerrado. | Seed/empresa demo mobile-first. |
| 4 | AM-5 | Cerrado. | POS, cobros y alertas repetibles. |
| 5 | AM-3 | Cerrado. | NeoScan seguro, medible y compatible con timeout. |
| 6 | AM-6 | Cerrado. | Runbook y politica de compatibilidad. |

## AM-0 - Baseline Contrato Movil

Estado: cerrado operativo 100%.

Entregables:

- Este documento.
- Enlaces desde README raiz, README API y planes de pruebas.
- Matriz de endpoints consumidos por la app.
- Lista de hallazgos clasificada por severidad.

Validacion:

- Revision contra `api_endpoints.dart` y repositorios Flutter.
- Revision contra controllers actuales.

## AM-1 - Compatibilidad Critica API Movil

Estado: cerrado operativo 100%.

Entregables:

- Corregir permisos de lectura DTE o documentar excepcion con tests.
- Validar que `/health` mantiene shape esperado por la app.
- Confirmar que `ApiResponse<T>` mantiene `success`, `message`, `data`, `errors`, `traceId`.
- Confirmar que `PagedResult<T>` mantiene `items`, `total`, `page`, `pageSize`, `totalPages`.
- Confirmar que descargas binarias no se envuelven.
- Medir tiempo de emision DTE y subida NeoScan.

Tareas:

| ID | Tarea | Resultado |
|---|---|---|
| AM-1.1 | Cambiar `DteController` lista/detalle a permiso de lectura si aplica. | `DTE.Consultar` puede listar/detallar. |
| AM-1.2 | Agregar pruebas de permiso DTE lectura vs emision. | Usuario consulta sin emitir pasa; usuario sin permiso falla. |
| AM-1.3 | Agregar smoke de auth mobile: login, me, refresh, logout. | Contrato de sesion cubierto. |
| AM-1.4 | Validar SuperAdmin no soportado para mobile. | Error `AUTH_NO_TENANT` o bloqueo documentado. |
| AM-1.5 | Probar descargas binarias DTE/POS/Scan. | Content-Type y bytes validos, sin envelope. |
| AM-1.6 | Medir duracion de emision y scan. | Riesgo de timeout clasificado. |
| AM-1.7 | Revisar errores de certificado/scan base64. | Mensajes y codigos legibles. |

Validacion:

- Tests de integracion con usuarios de empresa y permisos minimos.
- Smoke HTTP local contra API real.

## AM-2 - Suite Contractual Mobile API

Estado: cerrado operativo 100%.

Entregables:

- Proyecto o carpeta de tests `MobileApiContract` dentro de integracion.
- Fixture de empresa demo con modulos `NEODTE`, `NEOPOS`, `NEOSCANAI`, `NEOPROFIT`, `NEOPORTAL` cuando aplique.
- Tests por grupo:
  - Auth/session.
  - Dashboard/lookups.
  - Clientes/productos.
  - DTE config/emision/consulta/descarga.
  - Cobros/QR.
  - NeoScan.
  - Alertas.
  - POS/caja.
- Casos negativos: sin token, sin permiso, sin modulo, tenant cruzado, estado invalido.

Tareas:

| ID | Tarea | Resultado |
|---|---|---|
| AM-2.1 | Crear fixture `MobileApiContract`. | Datos y usuarios reutilizables. |
| AM-2.2 | Implementar tests por endpoint MAPI-01 a MAPI-30. | Cobertura positiva base. |
| AM-2.3 | Implementar negativos: 401, 403, 402, 404, 409. | Manejo app-friendly validado. |
| AM-2.4 | Validar shape JSON con asserts sobre nombres camelCase. | Compatibilidad con modelos Flutter. |
| AM-2.5 | Validar bytes con `Content-Type` y longitud. | Descargas protegidas contra regresion. |
| AM-2.6 | Generar reporte simple de endpoints cubiertos. | Evidencia para demos/release. |

Validacion:

- `dotnet test tests/NeoSTP.Tests.Integration/NeoSTP.Tests.Integration.csproj`.
- Reporte de endpoints cubiertos comparado contra `api_endpoints.dart`.

## AM-3 - NeoScan/Gemini Productivo para Movil

Estado: cerrado operativo 100%. OCR asincrono/polling avanzado queda como mejora V3 deliberada.

Entregables:

- Enviar API key de Gemini por header `x-goog-api-key`, no query string.
- Whitelist real de MIME/extension: `image/jpeg`, `image/png`, `application/pdf`.
- Umbral configurable `Scan:ConfianzaMinimaProcesado`.
- Estado asincrono recomendado: guardar `RECIBIDO/PROCESANDO`, encolar OCR y permitir polling.
- Trazabilidad: proveedor, modelo, duracion, error resumido, fecha de intento.
- Reintento seguro de OCR sin duplicar documento.

Tareas:

| ID | Tarea | Resultado |
|---|---|---|
| AM-3.1 | Mover API key Gemini a header. | Secretos fuera de URL/logs. |
| AM-3.2 | Configurar y probar umbral de confianza. | `REQUIERE_REVISION` cuando la confianza es baja. |
| AM-3.3 | Validar MIME/extension/tamano. | Errores 400/409 claros para mobile. |
| AM-3.4 | Disenar ejecucion asincrona o modo rapido no bloqueante. | Upload no excede timeout mobile. |
| AM-3.5 | Persistir metadatos de OCR. | Soporte puede diagnosticar provider/modelo/duracion. |
| AM-3.6 | Probar fallback cuando Gemini falla. | Captura manual sigue disponible. |

Validacion:

- Tests con provider `Mock` y `Gemini` simulado.
- Subida movil responde dentro del timeout aunque el OCR falle.

## AM-4 - Datos Demo Mobile-first

Estado: cerrado operativo 100%.

Entregables:

- Usuario `ADMIN` de empresa con permisos moviles completos.
- Usuario de solo consulta DTE para probar `DTE.Consultar`.
- Datos visibles:
  - 3 clientes.
  - 5 productos.
  - DTE procesado y DTE a credito.
  - Cobro pendiente, pago parcial y QR.
  - Venta POS, ticket y caja cerrada.
  - Scan con archivo y campos corregibles.
  - Alertas pendientes/resueltas.
- Seed o script idempotente.

Tareas:

| ID | Tarea | Resultado |
|---|---|---|
| AM-4.1 | Definir empresa demo mobile. | Tenant unico para pruebas de app. |
| AM-4.2 | Crear usuarios `MOBILE_*`. | RBAC positivo/negativo repetible. |
| AM-4.3 | Sembrar clientes/productos. | Lookups y CRUD con datos. |
| AM-4.4 | Sembrar DTE procesado y credito. | Consulta, PDF, JSON y CxC visibles. |
| AM-4.5 | Sembrar POS/caja. | Ticket y resumen no vacios. |
| AM-4.6 | Sembrar Scan y alertas. | NeoScan/alertas visibles. |
| AM-4.7 | Documentar reset demo. | Corrida idempotente sin duplicados. |

Validacion:

- Dashboard movil no queda vacio.
- DTE, Cobros, POS, NeoScan y Alertas muestran datos sin preparacion manual.

## AM-5 - POS, Cobros y Alertas para Demo

Estado: cerrado operativo 100%.

Entregables:

- Checklist de venta POS: abrir caja, vender, ticket, enviar, promover a DTE, cerrar.
- Checklist de cobro: pendiente, registrar pago, confirmar/anular, generar QR.
- Checklist de alertas: generar, listar, leer, resolver, registrar dispositivo.
- Mensajes controlados para modulo no contratado o permiso faltante.

Tareas:

| ID | Tarea | Resultado |
|---|---|---|
| AM-5.1 | Ejecutar ciclo POS completo. | Caja, venta, ticket, promover, cerrar. |
| AM-5.2 | Ejecutar ciclo CxC completo. | Pendiente, pago, confirmar/anular, QR. |
| AM-5.3 | Ejecutar ciclo alertas. | Generar, listar, leer, resolver, dispositivo. |
| AM-5.4 | Probar sin modulo `NEOPOS`/`NEOSCANAI`. | Respuesta legible para app. |
| AM-5.5 | Probar permisos insuficientes por flujo. | 403 controlado y traceable. |

Validacion:

- Smoke API y demo manual con usuario `OPERADOR` y `ADMIN`.

## AM-6 - Runbook y Versionado API Movil

Estado: cerrado operativo 100%.

Entregables:

- URL demo estable o procedimiento para publicar tunnel y configurar `API_BASE_URL`.
- Checklist de ambiente: API, DB, seed, providers, health, Scalar, logs.
- Politica de compatibilidad para `/api/*`: campos nuevos compatibles, cambios breaking documentados.
- Matriz de permisos por pantalla movil.
- Registro de evidencias por demo.

Tareas:

| ID | Tarea | Resultado |
|---|---|---|
| AM-6.1 | Definir URL demo y politica de `API_BASE_URL`. | La app apunta a ambiente correcto sin rebuild confuso. |
| AM-6.2 | Escribir checklist pre-demo API mobile. | Preparacion repetible. |
| AM-6.3 | Definir politica de cambios breaking. | Mobile no se rompe por cambios de DTO. |
| AM-6.4 | Documentar providers por demo. | Mock/Gemini/FCM/SMTP claros. |
| AM-6.5 | Documentar formato de evidencia. | Status, traceId, usuario, empresa, duracion. |

Validacion:

- Cualquier miembro tecnico puede preparar una demo movil/API siguiendo el runbook sin tocar Flutter.

## Datos Demo Requeridos

| Dato | Cantidad minima | Para validar |
|---|---:|---|
| Empresa con perfil fiscal completo | 1 | Dashboard, DTE config, tenant. |
| Usuarios mobile | 5 | RBAC positivo/negativo. |
| Clientes | 3 | Facturacion, CxC, lookups. |
| Productos | 5 | Factura, POS, barcode lookup. |
| DTE procesado | 2 | Listado, detalle, PDF/JSON. |
| DTE a credito | 1 | Cobros pendientes y QR. |
| Cuenta de cobro activa | 1 | Generacion de QR. |
| Venta POS | 1 | Ticket, resumen, caja. |
| Sesion de caja abierta/cerrada | 1/1 | Estado, cierre y negativos. |
| Scan con archivo | 1 | Bandeja, archivo, correccion. |
| Alerta pendiente | 1 | Badge, listar, leer/resolver. |
| Empresa sin `NEOPOS` o sin `NEOSCANAI` | 1 | Negativos 402/403 por modulo. |

## Evidencia por Corrida Mobile API

Cada corrida debe guardar:

- Fecha, branch, commit y ambiente.
- Base URL usada por la app/API.
- Usuario, rol, permisos y empresa.
- Provider activo: Hacienda, DTE signer, Scan, Push, Email.
- Resultado de `dotnet test NeoSTP.slnx` o suite contractual.
- Tabla de MAPI ejecutados: endpoint, status, `traceId`, duracion, resultado.
- Errores con payload resumido sin secretos.
- Decision final: apto demo, apto con advertencias, no apto.

## Matriz de Pruebas Minimas

| ID | Endpoint | Usuario | Esperado | Tipo |
|---|---|---|---|---|
| MAPI-01 | `GET /health` | anon | 200 con `data.status=ok`, `data.service=NeoSTP.Api`. | Positivo |
| MAPI-02 | `POST /api/auth/login` | `MOBILE_ADMIN` | Tokens, `empresaId`, roles y permisos. | Positivo |
| MAPI-03 | `GET /api/auth/me` | `MOBILE_ADMIN` | Perfil consistente con login. | Positivo |
| MAPI-04 | `POST /api/auth/refresh` | refresh token | Nuevo access token. | Positivo |
| MAPI-05 | `POST /api/auth/logout` | `MOBILE_ADMIN` | Refresh revocado. | Positivo |
| MAPI-06 | `POST /api/auth/login` | `SUPERADMIN` | App debe bloquear; API no debe operar datos sin empresa. | Negativo |
| MAPI-07 | `GET /api/dashboard/empresa` | `MOBILE_ADMIN` | KPIs. | Positivo |
| MAPI-08 | `GET /api/lookups/clientes?search=` | `MOBILE_ADMIN` | Lista para autocomplete. | Positivo |
| MAPI-09 | `GET /api/lookups/productos?search=` | `MOBILE_ADMIN` | Lista para autocomplete. | Positivo |
| MAPI-10 | `GET /api/clientes` | `MOBILE_ADMIN` | `PagedResult`. | Positivo |
| MAPI-11 | `POST /api/clientes` | `MOBILE_ADMIN` | Cliente creado o duplicado controlado. | Positivo/409 |
| MAPI-12 | `GET /api/productos` | `MOBILE_ADMIN` | `PagedResult`. | Positivo |
| MAPI-13 | `GET /api/dte/configuracion` | `MOBILE_ADMIN` | Config sin secretos. | Positivo |
| MAPI-14 | `POST /api/dte/configuracion/certificado` | `MOBILE_ADMIN` | Certificado aceptado o validacion controlada. | Positivo/400 |
| MAPI-15 | `POST /api/dte/configuracion/probar-conexion` | `MOBILE_ADMIN` | Resultado controlado. | Positivo |
| MAPI-16 | `POST /api/dte/emitir/factura` | `MOBILE_ADMIN` | DTE o error fiscal controlado con `traceId`. | Positivo/502 |
| MAPI-17 | `GET /api/dte/documentos` | `MOBILE_DTE_CONSULTA` | 200; no debe exigir emision si es lectura. | Positivo |
| MAPI-18 | `GET /api/dte/documentos/{id}` | `MOBILE_DTE_CONSULTA` | Detalle. | Positivo |
| MAPI-19 | `GET /api/dte/documentos/{id}/pdf` | `MOBILE_DTE_CONSULTA` | Bytes PDF. | Positivo |
| MAPI-20 | `GET /api/dte/documentos/{id}/json` | `MOBILE_DTE_CONSULTA` | Bytes/JSON crudo. | Positivo |
| MAPI-21 | `POST /api/dte/documentos/{id}/reenviar` | `MOBILE_DTE_CONSULTA` con `DTE.Reenviar` | `ApiResponse` exitoso o error email controlado. | Positivo/502 |
| MAPI-22 | `GET /api/cobros/resumen` | `MOBILE_COBROS` | CxC visible. | Positivo |
| MAPI-23 | `GET /api/cobros/pendientes` | `MOBILE_COBROS` | `PagedResult`. | Positivo |
| MAPI-24 | `POST /api/cobros/dte/{id}/pagos` | `MOBILE_COBROS` | Pago creado o validacion controlada. | Positivo/400 |
| MAPI-25 | `POST /api/cobros/qr` | `MOBILE_COBROS` | `qrPngBase64`. | Positivo |
| MAPI-26 | `GET /api/scanai/documentos` | `MOBILE_SCAN` | Bandeja paginada. | Positivo |
| MAPI-27 | `POST /api/scanai/documentos` | `MOBILE_SCAN` | Documento creado; no expone secretos ni bloquea por OCR externo. | Positivo |
| MAPI-28 | `GET /api/scanai/documentos/{id}/archivo` | `MOBILE_SCAN` | Bytes del archivo. | Positivo |
| MAPI-29 | `PUT /api/scanai/documentos/{id}/campos` | `MOBILE_SCAN` | Campos corregidos. | Positivo |
| MAPI-30 | `POST /api/scanai/documentos/{id}/registrar-dte-recibido` | `MOBILE_SCAN` | DTE recibido o estado invalido controlado. | Positivo/409 |
| MAPI-31 | `GET /api/alertas/resumen` | `MOBILE_ADMIN` | Conteos para badge. | Positivo |
| MAPI-32 | `GET /api/alertas` | `MOBILE_ADMIN` | `PagedResult`. | Positivo |
| MAPI-33 | `POST /api/alertas/dispositivos` | `MOBILE_ADMIN` | Token registrado. | Positivo |
| MAPI-34 | `POST /api/alertas/{id}/leer` | `MOBILE_ADMIN` | Alerta leida. | Positivo |
| MAPI-35 | `GET /api/pos/caja/estado` | `MOBILE_OPERADOR_POS` | `data` puede ser null si no hay caja. | Positivo |
| MAPI-36 | `POST /api/pos/caja/abrir` | `MOBILE_OPERADOR_POS` | Caja abierta o conflicto controlado. | Positivo/409 |
| MAPI-37 | `POST /api/pos/ventas` | `MOBILE_OPERADOR_POS` | Venta creada. | Positivo |
| MAPI-38 | `GET /api/pos/ventas/{id}/ticket` | `MOBILE_OPERADOR_POS` | Bytes PDF ticket. | Positivo |
| MAPI-39 | `POST /api/pos/ventas/{id}/promover` | `MOBILE_OPERADOR_POS` | DTE creado o error fiscal controlado. | Positivo/502 |
| MAPI-40 | `GET /api/scanai/documentos` | usuario sin `NEOSCANAI` | 402/403 legible, sin 500. | Negativo |
| MAPI-41 | `GET /api/pos/resumen` | usuario sin `NEOPOS` | 402/403 legible, sin 500. | Negativo |
| MAPI-42 | endpoint de empresa | sin token | 401. | Negativo |
| MAPI-43 | recurso de otra empresa | usuario empresa | 404/403 sin filtrar existencia. | Negativo |

## No Alcance

- No modificar UI, estado, routing ni dependencias Flutter desde este repositorio.
- No publicar Play Store.
- No mover logica fiscal al cliente.
- No crear backend separado para mobile.
- No romper contratos actuales de la app sin versionar y coordinar.
