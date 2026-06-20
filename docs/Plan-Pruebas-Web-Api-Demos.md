# Plan de Pruebas Web y API para Demos

> Fecha: 2026-06-14. Objetivo: tener una bateria repetible de pruebas para preparar demos
> comerciales y tecnicas de NeoSTP Cloud. Complementa `docs/Analisis-Pruebas-Cliente-V2.md`
> y convierte ese aprendizaje en un procedimiento recurrente.

## Alcance

Incluye:

- API REST interna/mobile.
- Contrato API para la app Android existente (`manuelberganza-dev/neocloud_mobile_android`).
- API publica NeoConnect `/api/v1`.
- Web MVC/Razor.
- Portal publico del receptor.
- Worker solo cuando el flujo de demo dependa de jobs: alertas, recordatorios, webhooks, backups.

No incluye:

- Pruebas de carga formales.
- Certificacion real nueva contra Hacienda.
- Nuevos modulos/proyectos.

## Objetivo de Calidad para Demo

Una demo esta lista cuando:

- `dotnet test NeoSTP.slnx` pasa completo.
- API y Web levantan con la misma base de datos demo.
- Los usuarios demo tienen permisos reales, sin depender de `SUPERADMIN`.
- Dashboard, DTE, POS, Cobros, Compras, Inventario, NeoProfit, Scan, Portal y Tesoreria tienen datos visibles.
- El recorrido Web principal termina sin errores 500, 403 inesperados, selects vacios criticos ni pantallas sin accion.
- Los endpoints API criticos devuelven contratos consistentes y errores esperados en negativos.

Ultima validacion tecnica registrada (2026-06-20):

- `dotnet build NeoSTP.slnx`: 0 warnings / 0 errores.
- `dotnet test NeoSTP.slnx`: 725 unitarias + 9 integracion verdes.
- HB-0..HB-8 cerrados operativos; API mobile AM-0..AM-6 cerrado operativo al 100%.
- `DemoReadinessContractTests`: 4/4 pruebas verdes para rutas API criticas, permisos, modulos,
  NeoConnect v1, rutas Web, portal publico y vistas Razor.
- `EmpresaPruebaSeederTests`: 5/5 pruebas verdes para seed demo idempotente API/mobile/comercial.
- `ApiVersioningContractTests`: 4/4 pruebas verdes para politica `/api/*`, `/api/v1`, docs y descargas binarias.
- `Hb7StorageSecretRetentionTests`: 4/4 pruebas verdes para readiness de storage, provider invalido,
  runbook HB-7 y enlaces documentales.
- `Hb8DemoReleaseTests`: 4/4 pruebas verdes para preflight, decisiones, sanitizacion, runbook,
  plantilla de evidencia y enlaces HB-8.
- V3-S1: `OrdenCompraServiceTests` + `V3OrdenCompraContractTests` cubren calculo, tenant, estados,
  conversion a CxP/inventario, rutas, modulo y permisos.
- V3-S2: las suites de ordenes y demo readiness cubren recepciones parciales, limite pendiente,
  idempotencia, no duplicacion de inventario, CxP consolidada y rutas/vistas Web.

## Ambientes

| Ambiente | Uso | Proveedores |
|---|---|---|
| Local demo | Ensayo tecnico y desarrollo | Mock por defecto; Gemini/FCM/Meta opcionales |
| Demo comercial | Presentacion a cliente | Mock para dependencias no contratadas; Hacienda apitest si aplica |
| Staging | Ensayo pre-release | Configuracion cercana a produccion, sin datos reales sensibles |

Variables criticas:

- `ConnectionStrings:NeoStpDb`
- `Jwt:Key`
- `Hacienda:Client`
- `Dte:Signer`
- `Email:Provider`
- `Scan:Provider`
- `Scan:Storage`
- `Push:Provider`
- `WhatsApp:Provider`
- `Cache:Provider`
- `Observability:Otlp:Endpoint`

## Usuarios de Prueba

| Rol | Uso en pruebas | Riesgo que cubre |
|---|---|---|
| `SUPERADMIN` | Soporte, operacion, seleccion de empresa | Paneles globales y modo soporte |
| `ADMIN` | Recorrido principal de empresa | Permisos reales sin bypass |
| `OPERADOR` | POS, clientes, productos, DTE basico | Operacion diaria |
| `CONTADOR` | Libros, compras, reportes, contabilidad | Cierre fiscal/contable |
| Receptor publico | Portal sin login | Tokens publicos y no filtracion |
| API Key NeoConnect | Integrador externo | Scopes, cuotas, webhooks |

Regla: toda pantalla de empresa debe probarse al menos con `ADMIN`. `SUPERADMIN` solo valida soporte.

## Datos Minimos de Demo

La empresa demo debe tener:

- Perfil fiscal completo: NIT, NRC, actividad, direccion, sucursal y punto de venta.
- Configuracion DTE valida para el modo elegido.
- 3 clientes: consumidor final, contribuyente, cliente con credito.
- 5 productos: gravado con costo, servicio, exento/no sujeto si aplica, producto con stock bajo, producto POS.
- 1 factura consumidor final procesada.
- 1 CCF procesado.
- 1 nota de credito o debito.
- 1 venta POS con ticket y cierre de caja.
- 1 factura a credito con saldo pendiente.
- 1 pago parcial y 1 pago confirmado.
- 1 proveedor, 1 orden de compra parcialmente recibida y 1 compra del mes.
- Inventario con entrada, salida y ajuste.
- 1 scan con archivo y campos extraidos/corregidos.
- 1 enlace de portal para DTE y 1 estado de cuenta.
- 1 cuenta bancaria, movimientos y CSV de conciliacion.
- 1 empleado y 1 planilla cerrada o calculada.
- 1 oportunidad CRM y 1 cotizacion convertible.

Cobertura automatica HB-5:

- `EmpresaPrueba:MobileDemo:Enabled=true` cubre esos datos desde el seed idempotente de empresa demo.
- El fixture comercial incluye DTE consumidor/CCF/notas, CxC, POS/caja, compra/CxP, inventario,
  tesoreria/conciliacion, portal, NeoScan, CRM, RRHH y Profit.
- Antes de una demo comercial, reiniciar desde BD limpia o ejecutar migraciones + seed; correr el
  seed dos veces no debe duplicar escenarios.

## Cadencia de Pruebas

| Tipo | Duracion | Cuando correrlo |
|---|---:|---|
| Smoke tecnico | 15-20 min | Antes de empezar a preparar demo |
| Smoke comercial | 30-45 min | Antes de cada presentacion |
| Regresion demo completa | 4-6 h | Antes de release, demo ejecutiva o entrega a cliente |
| Regresion automatizada | Segun CI | Cada push/PR |

## Baseline Automatizada HB-3/HB-4/HB-5/HB-6/HB-7/HB-8

La prueba `tests/NeoSTP.Tests.Unit/Api/DemoReadinessContractTests.cs` es obligatoria para aceptar
cambios que toquen controllers API, controllers Web, permisos, modulos o vistas de demo.

Cubre:

- API interna de alto valor: dashboard, DTE, cobros, POS/caja, NeoScanAI, compras, inventario,
  tesoreria, reportes fiscales, NeoConta, NeoProfit, CRM y portal interno.
- API publica NeoConnect v1: ping, DTE, PDF, clientes y productos.
- Web demo: Home, DTE, POS, caja, cobros, compras, inventario, NeoProfit, Scan, tesoreria,
  integraciones, soporte y portal publico.
- Metadata de seguridad: `[Authorize]`, `[AllowAnonymous]`, `[RequireModule]` y `[RequirePermiso]`.
- Existencia de vistas Razor criticas para evitar rutas que compilan pero rompen la demo.
- Politica HB-6 de versionado: Tier A `/api/*`, NeoConnect `/api/v1`, descargas binarias con
  content-type real y documentacion enlazada.
- Politica HB-7 de storage: readiness `Database`/`FileSystem`, provider invalido, runbook de
  secretos/retencion y guardrail de NeoScan contra rutas absolutas.
- Politica HB-8 de demo/release: preflight de codigo/secretos/providers/build/tests/health,
  decisiones `APTO_*`/`NO_APTO`, runbook y evidencia sanitizada.

Comando enfocado:

```bash
dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj --filter DemoReadinessContractTests
```

Comando enfocado para datos demo:

```bash
dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj --filter EmpresaPruebaSeederTests
```

Comando enfocado para versionado/contratos:

```bash
dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj --filter ApiVersioningContractTests
```

Comando enfocado para storage/secretos/retencion:

```bash
dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj --filter "Hb7StorageSecretRetentionTests|ScanBlobStorageTests"
```

Comando enfocado para demo/release:

```bash
dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj --filter Hb8DemoReleaseTests
```

## Preflight HB-8

Fuente operativa: `docs/Runbook-Demo-Release.md`.

Antes del smoke manual ejecutar con API/Web levantadas:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\demo-preflight.ps1 `
  -Profile Demo -RequireServices `
  -ApiBaseUrl http://localhost:5058 -WebBaseUrl http://localhost:5031 `
  -EvidencePath tmp\demo-preflight-online.json
```

- `NO_APTO`: bloquea demo/release.
- `APTO_CON_ADVERTENCIAS`: cada advertencia debe aceptarse y registrarse.
- `APTO_DEMO` / `APTO_RELEASE`: habilita continuar con la matriz manual.
- La evidencia JSON no sustituye capturas, roles ni casos de negocio; los complementa.

## Preflight Tecnico

1. Ejecutar `scripts/demo-preflight.ps1`; no continuar con decision `NO_APTO`.
2. Revisar branch, commit y evidencia generada.
3. Levantar API: `dotnet run --project src/NeoSTP.Api`.
4. Levantar Web: `dotnet run --project src/NeoSTP.Web`.
5. Levantar Worker si se prueban jobs: `dotnet run --project src/NeoSTP.Worker`.
6. Repetir preflight con `-RequireServices` y validar:
   - API `/health/live`
  - API `/health/ready` (BD, correo y storage)
   - Web `/health/live`
  - Web `/health/ready` (BD, correo y storage)
   - API `/openapi/v1.json`
   - Scalar `/scalar/v1`

## Smoke API

| Paso | Endpoint | Usuario | Esperado |
|---|---|---|---|
| API-01 | `POST /api/auth/login` | ADMIN | 200, JWT |
| API-02 | `GET /api/auth/me` | ADMIN | Empresa, roles, permisos |
| API-03 | `GET /api/dashboard/empresa` | ADMIN | KPIs con datos |
| API-04 | `GET /api/lookups/departamentos` | ADMIN | Lista no vacia |
| API-05 | `GET /api/clientes` | ADMIN | Lista paginada |
| API-06 | `GET /api/productos` | ADMIN | Lista paginada |
| API-07 | `GET /api/dte/documentos` | ADMIN | DTE existentes |
| API-08 | `POST /api/dte/emitir` | ADMIN | DTE creado o error de validacion controlado |
| API-09 | `GET /api/pos/resumen` | OPERADOR | Resumen del dia |
| API-10 | `GET /api/cobros/resumen` | ADMIN | CxC visible |
| API-11 | `GET /api/compras/resumen` | CONTADOR | CxP visible |
| API-12 | `POST /api/compras/ordenes` | CONTADOR | Orden BORRADOR con totales recalculados por servidor |
| API-13 | `POST /api/compras/ordenes/{id}/emitir` | CONTADOR | Orden EMITIDA e inmutable |
| API-14 | `POST /api/compras/ordenes/{id}/recepciones` | CONTADOR | Primera entrega deja PARCIAL y registra kardex con idempotency key |
| API-15 | `POST /api/compras/ordenes/{id}/recepciones` | CONTADOR | Segunda entrega completa deja RECIBIDA sin exceder pendientes |
| API-16 | `POST /api/compras/ordenes/{id}/convertir-factura` | CONTADOR | CxP consolidada creada una vez, sin duplicar inventario |
| API-17 | `GET /api/inventario/resumen` | ADMIN | Stock y alertas |
| API-18 | `GET /api/profit/dashboard` | ADMIN | P&L |
| API-19 | `GET /api/scanai/documentos` | ADMIN | Bandeja |
| API-20 | `GET /api/tesoreria/resumen` | CONTADOR | Bancos/caja |
| API-21 | `GET /api/reportes/fiscal/f07` | CONTADOR | Resumen fiscal |
| API-22 | `GET /api/conta/balanza` | CONTADOR | Balanza |
| API-23 | `GET /api/crm/resumen` | ADMIN | Pipeline |
| API-24 | `GET /api/alertas/resumen` | ADMIN | Badges |
| API-25 | `GET /api/v1/ping` | API Key | Key y scopes validos |

## Smoke API Mobile

Este bloque valida el contrato que consume la app Android ya construida. No prueba Flutter ni cambia el
repo movil; prueba que la API de este repositorio siga entregando lo que la app espera.
La matriz completa `MAPI-01` a `MAPI-43` vive en `docs/Plan-Hallazgos-Api-Mobile.md`; esta tabla es el
smoke corto para decidir si una demo puede continuar.

Reglas:

- Usar usuarios de empresa, nunca `SUPERADMIN`.
- No enviar `?empresaId` desde el cliente movil.
- Mantener `ApiResponse<T>` para JSON y bytes crudos para descargas.
- Ejecutar con `Scan:Provider=Mock` por defecto; repetir con Gemini solo si hay credenciales de demo.
- Confirmar que las respuestas clave entran en el timeout movil: conexion 20s, respuesta 30s.

| Paso | Endpoint | Usuario | Esperado |
|---|---|---|---|
| SMAPI-01 | `GET /health` | anon | 200, `data.status=ok`, `data.service=NeoSTP.Api` |
| SMAPI-02 | `POST /api/auth/login` | `MOBILE_ADMIN` | Tokens, `empresaId`, roles y permisos |
| SMAPI-03 | `GET /api/auth/me` | `MOBILE_ADMIN` | Perfil igual al login |
| SMAPI-04 | `GET /api/dashboard/empresa` | `MOBILE_ADMIN` | KPIs visibles |
| SMAPI-05 | `GET /api/clientes?page=1&pageSize=20` | `MOBILE_ADMIN` | `PagedResult` |
| SMAPI-06 | `GET /api/productos?page=1&pageSize=20` | `MOBILE_ADMIN` | `PagedResult` |
| SMAPI-07 | `POST /api/dte/emitir/factura` | `MOBILE_ADMIN` | DTE o error fiscal controlado con `traceId` |
| SMAPI-08 | `GET /api/dte/documentos` | `MOBILE_DTE_CONSULTA` | Lista paginada sin exigir `DTE.Emitir` |
| SMAPI-09 | `GET /api/dte/documentos/{id}/pdf` | `MOBILE_DTE_CONSULTA` | Bytes PDF, no `ApiResponse` |
| SMAPI-10 | `GET /api/cobros/resumen` | `MOBILE_COBROS` | Totales de cartera |
| SMAPI-11 | `POST /api/cobros/qr` | `MOBILE_COBROS` | `qrPngBase64` |
| SMAPI-12 | `POST /api/scanai/documentos` | `MOBILE_SCAN` | Documento creado o error controlado |
| SMAPI-13 | `GET /api/alertas/resumen` | `MOBILE_ADMIN` | Conteos para badge |
| SMAPI-14 | `GET /api/pos/caja/estado` | `MOBILE_OPERADOR_POS` | `data` puede ser null si no hay caja |
| SMAPI-15 | `GET /api/scanai/documentos` | usuario sin `NEOSCANAI` | 402/403 legible, sin 500 |

## Regresion API por Flujo

### Auth, Tenant y Seguridad

- Login exitoso.
- Login invalido.
- MFA si esta activo.
- `GET /api/auth/me`.
- Endpoint de empresa sin token -> 401.
- Usuario sin permiso -> 403.
- `SUPERADMIN` sin `empresaId` en endpoint de empresa -> error controlado.
- Intento de leer recurso de otra empresa -> 404/403 sin filtrar existencia.

### Core y Onboarding

- Empresas, sucursales, puntos de venta.
- Usuarios, roles, permisos.
- Licencia y modulos activos.
- Checklist de onboarding cambia segun datos reales.

### DTE

- Crear factura.
- Generar JSON.
- Validar.
- Firmar.
- Enviar a Hacienda/mock.
- Descargar PDF.
- Descargar JSON.
- Reenviar correo.
- Invalidar o registrar evento si aplica.
- Diagnostico de error MH.

Negativos:

- Cliente incompleto.
- Producto sin codigo requerido.
- Firma mock contra cliente real.
- DTE en estado invalido.

### POS y Caja

- Abrir caja.
- Crear venta POS.
- Generar ticket PDF.
- Enviar ticket por correo mock/SMTP.
- Promover venta a DTE.
- Cerrar caja.

Negativos:

- Abrir caja duplicada.
- Venta sin stock.
- Promover venta anulada.

### Cobros y Portal

- Listar pendientes.
- Registrar pago.
- Confirmar pago.
- Generar QR/enlace.
- Ejecutar recordatorio.
- Crear enlace de portal para DTE.
- Abrir portal sin sesion.
- Revocar token y confirmar 404.

Negativos:

- Pago mayor al saldo.
- Recordatorios activos sin canal.
- Token inventado.

### Compras, Inventario y Tesoreria

- Crear proveedor.
- Crear orden de compra en BORRADOR y comprobar el calculo server-side de subtotal, IVA y total.
- Editar la orden y emitirla.
- Confirmar que una orden EMITIDA no admite edicion y una CANCELADA no admite emision.
- Registrar dos recepciones parciales y verificar acumulado/pendiente por linea.
- Repetir la primera idempotency key y confirmar que no cambia inventario.
- Convertir la orden completamente recibida a factura consolidada una sola vez.
- Confirmar que solo las lineas de bienes generan kardex y la factura no repite entradas.
- Registrar factura de compra.
- Confirmar pago proveedor.
- Ver CxP.
- Ver entrada de inventario si aplica.
- Ver kardex.
- Registrar movimiento bancario.
- Importar CSV de banco.
- Ver sugerencias.
- Conciliar y desconciliar.

Negativos:

- Usar proveedor o producto de otra empresa.
- Recibir mas que la cantidad pendiente o usar una linea de otra orden.
- Facturar una orden parcialmente recibida.
- Convertir dos veces la misma orden.
- Reimportar CSV duplicado.
- Conciliar contra signo incorrecto.
- Salida de inventario sin stock.

### NeoScanAI

- Subir imagen/PDF base64.
- Descargar archivo.
- Corregir campos.
- Guardar resultado OCR externo.
- Registrar como gasto.
- Registrar como compra financiera.
- Registrar como DTE recibido.
- Rechazar.

Negativos:

- Base64 invalido.
- Archivo vacio.
- Archivo sobre limite.
- MIME no permitido cuando se implemente whitelist.
- Segundo intento sobre documento confirmado.

### NeoConnect

- Crear API key.
- Validar raw key una sola vez.
- `GET /api/v1/ping`.
- Crear cliente/producto por scope.
- Emitir DTE por API key.
- Consultar y descargar PDF/JSON.
- Webhook test firmado.
- Log de deliveries.

Negativos:

- Key invalida.
- Key revocada.
- Scope faltante.
- Rate limit.

## Smoke Web

| Paso | Ruta | Rol | Esperado |
|---|---|---|---|
| WEB-01 | `/Account/Login` | anon | Login carga |
| WEB-02 | `/Dashboard` o home autenticado | ADMIN | KPIs y onboarding |
| WEB-03 | `/DteDocumentos` | ADMIN | Lista DTE |
| WEB-04 | `/DteDocumentos/Create` | ADMIN | Formulario con selects completos |
| WEB-05 | `/Clientes` | ADMIN | Lista y crear/editar |
| WEB-06 | `/Productos` | ADMIN | Lista y crear/editar |
| WEB-07 | `/Pos` | OPERADOR | Venta POS |
| WEB-08 | `/PosCaja` o caja POS | OPERADOR | Abrir/cerrar caja |
| WEB-09 | `/Cobros` | ADMIN | CxC |
| WEB-10 | `/Compras` | CONTADOR | Compras/CxP |
| WEB-11 | `/Compras/Ordenes` | CONTADOR | Crear, emitir, recibir parcialmente y convertir orden |
| WEB-12 | `/Inventario` | ADMIN | Existencias/kardex |
| WEB-13 | `/Profit` | ADMIN | P&L |
| WEB-14 | `/Scan` | ADMIN | Bandeja y preview |
| WEB-15 | `/DteRecibidos` | CONTADOR | DTE recibidos |
| WEB-16 | `/Tesoreria` | CONTADOR | Cuentas/movimientos |
| WEB-17 | `/Tesoreria/Conciliacion` | CONTADOR | Import/sugerencias |
| WEB-18 | `/NeoBi` | CONTADOR | Libros/F-07 |
| WEB-19 | `/Conta` | CONTADOR | Asientos/balanza |
| WEB-20 | `/Crm` | ADMIN | Pipeline |
| WEB-21 | `/Portal/{token}` | anon | DTE/estado publico |
| WEB-22 | `/Integraciones` | ADMIN | API keys/webhooks |
| WEB-23 | `/Soporte/Operacion` | SUPERADMIN | Panel operativo |

## Regresion Web por Rol

### ADMIN

- Login.
- Cambiar empresa si aplica.
- Ver dashboard con KPIs.
- Completar o revisar onboarding.
- Crear/editar cliente.
- Crear/editar producto.
- Emitir DTE.
- Revisar DTE procesado.
- Abrir NeoScan, corregir y confirmar documento.
- Generar enlace de portal.
- Ver integraciones y API keys.
- Revisar alertas.

### OPERADOR / POS

- Login.
- Abrir caja.
- Crear venta.
- Imprimir/descargar ticket.
- Enviar ticket por correo.
- Promover a DTE.
- Cerrar caja.
- Confirmar que no ve superficies contables no autorizadas.

### CONTADOR

- Login.
- Crear, editar y emitir una orden de compra.
- Registrar dos recepciones y abrir su historial/kardex.
- Convertir la orden completa a CxP consolidada.
- Ver compras/CxP.
- Revisar libro IVA.
- Generar F-07.
- Generar asientos.
- Ver balanza cuadrada.
- Importar estado bancario.
- Conciliar movimientos.

### SUPERADMIN

- Login con MFA si esta obligado.
- Panel operativo.
- Modo soporte con empresa seleccionada.
- Ver salud, rechazos MH, recordatorios, portal y API keys.
- Confirmar que no opera datos de empresa sin contexto seleccionado.

### Receptor Publico

- Abrir token valido.
- Descargar PDF.
- Descargar JSON si aplica.
- Ver estado de cuenta.
- Confirmar que token revocado o inventado devuelve 404.

## Pruebas Responsive y UX

Pantallas minimas:

- Dashboard.
- DTE create.
- POS.
- Scan detalle.
- Portal publico.
- Tesoreria conciliacion.
- Ordenes de compra: listado, formulario y detalle/recepcion.

Viewports:

- Desktop 1440x900.
- Laptop 1366x768.
- Tablet 768x1024.
- Mobile 390x844.

Criterios:

- Sin texto sobrepuesto.
- Sidebar/menu usable.
- Botones criticos visibles.
- Formularios no cortan labels ni inputs.
- Estados vacios tienen CTA.
- Tablas permiten escaneo o scroll correcto.

## Evidencia de Demo

Guardar por corrida:

- Fecha, branch, commit, ambiente.
- Resultado de `dotnet test NeoSTP.slnx`.
- URLs API/Web usadas.
- Usuario/rol usado.
- Lista de endpoints/pantallas recorridas.
- Capturas de pantallas clave.
- Errores con ruta, rol, empresa, request resumido y prioridad.

## Criterios de Bloqueo

No hacer demo si:

- Build o tests estan rojos.
- Login ADMIN falla.
- API `/health/ready` falla por BD, correo o storage configurado.
- Web no carga dashboard.
- DTE no puede generar al menos un documento en modo mock/apitest.
- Hay 403 inesperado en rutas comerciales del ADMIN.
- Portal publico no abre token valido.
- Hay secretos reales en logs o documentos, o `Scan:Storage:Root` no esta respaldado cuando se usa filesystem.

## Cierre de Corrida

Al terminar:

1. Registrar hallazgos en `docs/Plan-Hallazgos-Bugs-Demo.md` o issue tracker.
2. Clasificar prioridad: bloqueo demo, alto, medio, bajo.
3. Indicar si se requiere codigo, datos demo, config o documentacion.
4. Limpiar tokens temporales y revocar enlaces de portal de prueba si contenian datos sensibles.
5. Dejar commit de documentacion/codigo cuando aplique; no pushear sin instruccion explicita.
