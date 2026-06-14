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

El contrato general calza bien con la API actual. Los riesgos principales no estan en crear nuevos
endpoints, sino en endurecer compatibilidad y demo:

- Permisos de lectura DTE: lista/detalle usan `DTE.Emitir`, mientras la app separa consulta con
  `DTE.Consultar`.
- Falta una suite automatizada que use exactamente los endpoints consumidos por la app.
- NeoScan/Gemini requiere hardening productivo antes de venderlo como OCR real.
- La app tiene timeout de respuesta de 30s; flujos largos deben terminar rapido o pasar a estado
  asincrono/reintento.
- La URL demo debe ser estable y operable; la app permite override con `API_BASE_URL`, pero hoy trae una
  URL Cloudflare temporal como default.

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

## Hallazgos Priorizados

| ID | Severidad | Hallazgo | Impacto | Sprint |
|---|---:|---|---|---|
| AM-001 | Alta | `GET /api/dte/documentos` y `GET /api/dte/documentos/{id}` requieren `DTE.Emitir`; la app y el contrato de lectura esperan `DTE.Consultar`. | Usuarios de solo consulta podrian ver 403 en listado/detalle aunque si puedan descargar PDF/JSON. | AM-1 |
| AM-002 | Alta | No hay suite automatizada especifica que ejecute los endpoints exactos de `api_endpoints.dart`. | Un cambio de controller/DTO puede romper la app sin que CI lo detecte. | AM-2 |
| AM-003 | Alta | NeoScan con Gemini aun necesita hardening: API key por query, OCR sincronico, umbral de confianza permisivo, MIME whitelist pendiente. | Riesgo de seguridad, UX lenta y documentos marcados como procesados con baja calidad. | AM-3 |
| AM-004 | Alta | Flujos largos pueden superar el timeout movil de 30s (`receiveTimeout`). | La app puede mostrar error aunque el backend termine despues. | AM-1/AM-3 |
| AM-005 | Media-alta | URL demo movil depende de tunnel temporal si no se inyecta `API_BASE_URL`. | Demo falla por endpoint expirado o no reproducible. | AM-6 |
| AM-006 | Media-alta | Descargas binarias deben permanecer sin envelope. | Si se envuelven por error, compartir PDF/JSON/ticket/archivo falla. | AM-2 |
| AM-007 | Media-alta | Auth refresh, bloqueo SuperAdmin y permisos efectivos requieren pruebas contractuales. | Sesiones moviles pueden quedar en logout inesperado o acceso incorrecto. | AM-1/AM-2 |
| AM-008 | Media-alta | Datos demo deben cubrir DTE, CxC, QR, NeoScan, POS/caja y alertas; si no, pantallas moviles quedan vacias. | Demo comercial pierde valor aunque la API este sana. | AM-4 |
| AM-009 | Media | `POST /api/dte/emitir/factura` es el flujo movil real actual; la app muestra mas tipos, pero su request de factura hardcodea `01`. | No es bug backend, pero debe preservarse ruta generica y documentar atajos por tipo. | AM-5 |
| AM-010 | Media | POS/caja depende de modulo `NEOPOS`, permisos y caja activa; estado de caja puede ser `data: null`. | La app lo tolera, pero requiere seed/permisos claros. | AM-5 |
| AM-011 | Media | Push real FCM es pluggable; polling de alertas funciona sin FCM. | Demo no debe prometer notificacion push real sin credenciales. | AM-5 |
| AM-012 | Media | Certificado DTE y Scan usan base64 en JSON; faltan pruebas de tamano, MIME y errores legibles para movil. | Errores de archivo pueden verse genericos en la app. | AM-1/AM-3 |
| AM-013 | Media | Versionado interno `/api/*` no esta formalizado; NeoConnect si usa `/api/v1`. | Riesgo de cambios incompatibles para mobile. | AM-6 |
| AM-014 | Media | Falta runbook de demo API movil: URL, usuario, permisos, proveedor mock/real, health y evidencias. | La demo depende de memoria operativa. | AM-6 |

## Siguiente Sprint Recomendado

**AM-1 - Compatibilidad critica API movil.**

Motivo: antes de ampliar pruebas o preparar demo, hay que cerrar los bugs que pueden bloquear una app ya
construida: permisos DTE de lectura, refresh/tenant, bytes crudos y timeouts de flujos largos.

Entregables:

- Cambiar o justificar permiso de lista/detalle DTE para lectura movil (`DTE.Consultar`).
- Tests de autorizacion para DTE: usuario con `DTE.Consultar` puede listar/detallar/descargar; usuario sin
  permiso recibe 403.
- Smoke de auth movil: login, `/me`, refresh, logout y rechazo de usuario `SUPERADMIN`.
- Tests de descargas binarias: PDF/JSON DTE, ticket POS y archivo NeoScan no devuelven envelope JSON.
- Medicion basica de duracion para `POST /api/dte/emitir/*` y `POST /api/scanai/documentos`.

Criterio de cierre:

- `dotnet test NeoSTP.slnx` verde.
- Matriz AM-001, AM-006 y AM-007 cubierta en tests o checklist ejecutado.
- Documento de pruebas actualizado con evidencia de endpoints.

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

## AM-0 - Baseline Contrato Movil

Entregables:

- Este documento.
- Enlaces desde README raiz, README API y planes de pruebas.
- Matriz de endpoints consumidos por la app.
- Lista de hallazgos clasificada por severidad.

Validacion:

- Revision contra `api_endpoints.dart` y repositorios Flutter.
- Revision contra controllers actuales.

## AM-1 - Compatibilidad Critica API Movil

Entregables:

- Corregir permisos de lectura DTE o documentar excepcion con tests.
- Validar que `/health` mantiene shape esperado por la app.
- Confirmar que `ApiResponse<T>` mantiene `success`, `message`, `data`, `errors`, `traceId`.
- Confirmar que `PagedResult<T>` mantiene `items`, `total`, `page`, `pageSize`, `totalPages`.
- Confirmar que descargas binarias no se envuelven.
- Medir tiempo de emision DTE y subida NeoScan.

Validacion:

- Tests de integracion con usuarios de empresa y permisos minimos.
- Smoke HTTP local contra API real.

## AM-2 - Suite Contractual Mobile API

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

Validacion:

- `dotnet test tests/NeoSTP.Tests.Integration/NeoSTP.Tests.Integration.csproj`.
- Reporte de endpoints cubiertos comparado contra `api_endpoints.dart`.

## AM-3 - NeoScan/Gemini Productivo para Movil

Entregables:

- Enviar API key de Gemini por header `x-goog-api-key`, no query string.
- Whitelist real de MIME/extension: `image/jpeg`, `image/png`, `application/pdf`.
- Umbral configurable `Scan:ConfianzaMinimaProcesado`.
- Estado asincrono recomendado: guardar `RECIBIDO/PROCESANDO`, encolar OCR y permitir polling.
- Trazabilidad: proveedor, modelo, duracion, error resumido, fecha de intento.
- Reintento seguro de OCR sin duplicar documento.

Validacion:

- Tests con provider `Mock` y `Gemini` simulado.
- Subida movil responde dentro del timeout aunque el OCR falle.

## AM-4 - Datos Demo Mobile-first

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

Validacion:

- Dashboard movil no queda vacio.
- DTE, Cobros, POS, NeoScan y Alertas muestran datos sin preparacion manual.

## AM-5 - POS, Cobros y Alertas para Demo

Entregables:

- Checklist de venta POS: abrir caja, vender, ticket, enviar, promover a DTE, cerrar.
- Checklist de cobro: pendiente, registrar pago, confirmar/anular, generar QR.
- Checklist de alertas: generar, listar, leer, resolver, registrar dispositivo.
- Mensajes controlados para modulo no contratado o permiso faltante.

Validacion:

- Smoke API y demo manual con usuario `OPERADOR` y `ADMIN`.

## AM-6 - Runbook y Versionado API Movil

Entregables:

- URL demo estable o procedimiento para publicar tunnel y configurar `API_BASE_URL`.
- Checklist de ambiente: API, DB, seed, providers, health, Scalar, logs.
- Politica de compatibilidad para `/api/*`: campos nuevos compatibles, cambios breaking documentados.
- Matriz de permisos por pantalla movil.
- Registro de evidencias por demo.

Validacion:

- Cualquier miembro tecnico puede preparar una demo movil/API siguiendo el runbook sin tocar Flutter.

## Matriz de Pruebas Minimas

| ID | Endpoint | Usuario | Esperado |
|---|---|---|---|
| MAPI-01 | `GET /health` | anon | 200 con `data.status=ok`. |
| MAPI-02 | `POST /api/auth/login` | ADMIN | Tokens, `empresaId`, roles y permisos. |
| MAPI-03 | `GET /api/auth/me` | ADMIN | Perfil consistente con login. |
| MAPI-04 | `POST /api/auth/refresh` | refresh token | Nuevo access token. |
| MAPI-05 | `GET /api/dashboard/empresa` | ADMIN | KPIs. |
| MAPI-06 | `GET /api/lookups/clientes?search=` | ADMIN | Lista para autocomplete. |
| MAPI-07 | `GET /api/clientes` | ADMIN | `PagedResult`. |
| MAPI-08 | `GET /api/productos` | ADMIN | `PagedResult`. |
| MAPI-09 | `GET /api/dte/configuracion` | ADMIN | Config sin secretos. |
| MAPI-10 | `POST /api/dte/emitir/factura` | ADMIN | DTE o error fiscal controlado con `traceId`. |
| MAPI-11 | `GET /api/dte/documentos` | DTE.Consultar | 200; no debe exigir emision si es lectura. |
| MAPI-12 | `GET /api/dte/documentos/{id}/pdf` | DTE.Consultar | Bytes PDF. |
| MAPI-13 | `POST /api/dte/documentos/{id}/reenviar` | DTE.Reenviar | `ApiResponse` exitoso o error email controlado. |
| MAPI-14 | `GET /api/cobros/resumen` | Cobros.Ver | CxC visible. |
| MAPI-15 | `POST /api/cobros/qr` | Cobros.Ver | `qrPngBase64`. |
| MAPI-16 | `POST /api/scanai/documentos` | ScanAI.Ver | Documento creado; no expone secretos ni bloquea por OCR externo. |
| MAPI-17 | `GET /api/scanai/documentos/{id}/archivo` | ScanAI.Ver | Bytes del archivo. |
| MAPI-18 | `GET /api/alertas/resumen` | ADMIN | Conteos para badge. |
| MAPI-19 | `POST /api/alertas/dispositivos` | ADMIN | Token registrado. |
| MAPI-20 | `GET /api/pos/caja/estado` | OPERADOR | `data` puede ser null si no hay caja. |
| MAPI-21 | `POST /api/pos/caja/abrir` | OPERADOR | Caja abierta o conflicto controlado. |
| MAPI-22 | `POST /api/pos/ventas` | OPERADOR | Venta creada. |
| MAPI-23 | `GET /api/pos/ventas/{id}/ticket` | OPERADOR | Bytes PDF ticket. |

## No Alcance

- No modificar UI, estado, routing ni dependencias Flutter desde este repositorio.
- No publicar Play Store.
- No mover logica fiscal al cliente.
- No crear backend separado para mobile.
- No romper contratos actuales de la app sin versionar y coordinar.
