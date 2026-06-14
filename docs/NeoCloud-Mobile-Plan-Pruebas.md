# NeoCloud Mobile - Plan de pruebas con API local

Este plan cubre la validacion de NeoCloud Mobile consumiendo `NeoSTP.Api` desde una URL HTTPS temporal, mientras la API apunta a la base SQL Server local ya configurada en `appsettings`.

> Nota 2026-06-14: la app Android vive en
> `https://github.com/manuelberganza-dev/neocloud_mobile_android`. En este repositorio este plan se usa para
> validar API, ambiente, usuarios, datos demo y contrato; no para modificar Flutter.

## Objetivo

Probar la app Flutter contra datos reales/demo de NeoSTP Cloud sin exponer SQL Server, certificados DTE, claves de Hacienda, passwords ni connection strings.

Arquitectura esperada:

```text
NeoCloud Mobile
        |
        v
URL HTTPS temporal
Cloudflare Tunnel / ngrok
        |
        v
NeoSTP.Api local
        |
        v
SQL Server local con DTE/configuracion MH/certificado
```

## Sprint recomendado

El siguiente sprint debe ser un sprint corto de validacion end-to-end: conectar Flutter contra la API local expuesta por HTTPS, confirmar autenticacion real, lectura de datos, consulta DTE y emision rapida con una empresa demo preparada.

Entregables del sprint:

1. API local levantada y validada con `/health`, `/scalar/v1` y login JWT.
2. URL HTTPS temporal funcionando contra la API local.
3. Usuario demo de empresa con permisos minimos.
4. Matriz de pruebas ejecutada y evidencias registradas.
5. Lista de hallazgos separados en backend, app Flutter, datos/configuracion y ambiente.

Resultado esperado: el desarrollador Flutter puede probar desde otra ubicacion usando solo `API_BASE_URL`, usuario demo y password demo.

## Alcance

Incluido:

- Health check y disponibilidad de API.
- Login real con JWT.
- Bloqueo de SuperAdmin en movil.
- Refresh token y reintento ante 401.
- Dashboard con datos reales.
- Consulta/listado/detalle/PDF de DTE.
- Emision rapida por `POST /api/dte/emitir`.
- Busqueda de clientes y productos.
- Resumen de cobros y alertas.
- Validacion de errores con `traceId`.

Fuera de alcance para esta ronda:

- Exponer SQL Server.
- Compartir certificados, passwords MH o connection strings.
- Firma DTE desde Flutter.
- Configurar hardening productivo de OCR Gemini, FCM real o NIT en linea de MH.
- Publicacion en Play Store.

## Requisitos previos

### Backend local

- `appsettings.Development.json`, `appsettings.Local.json`, variables de entorno o user-secrets apuntan a la base local correcta.
- La base tiene una empresa con configuracion DTE completa:
  - Datos fiscales.
  - Credenciales MH.
  - Certificado cargado.
  - Sucursal/punto de venta configurados.
  - Clientes y productos de prueba.
- La API puede arrancar localmente:

```powershell
dotnet run --project src/NeoSTP.Api
```

### Usuario demo

Crear o confirmar un usuario de empresa. No usar SuperAdmin.

Permisos minimos:

- `DTE.Emitir`
- `DTE.Consultar`
- `Clientes.Ver`
- `Productos.Ver`
- `Cobros.Ver`
- `Alertas.Ver`

Permisos opcionales segun alcance:

- `Clientes.Crear`
- `Productos.Crear`
- `DTE.Reenviar`
- `DTE.Configurar`
- `ScanAI.Ver`
- `ScanAI.Confirmar`

### App Flutter

La app debe poder configurar `baseUrl` por ambiente, idealmente:

```text
API_BASE_URL=https://xxxxx.trycloudflare.com
```

Reglas que la app debe respetar:

- Usar JWT Bearer.
- Guardar tokens en almacenamiento seguro.
- No enviar `empresaId`.
- Bloquear `tipoUsuarioCodigo == "SUPERADMIN"` o `empresaId == null`.
- Mostrar `traceId` cuando ocurra un error de API.

## Preparacion del ambiente

### 1. Compilar backend

```powershell
dotnet build NeoSTP.slnx
```

Criterio de aceptacion:

- Build exitoso.
- Sin cambios de configuracion sensible agregados al repo.

### 2. Validar pruebas automatizadas disponibles

```powershell
dotnet test tests/NeoSTP.Tests.Unit
dotnet test tests/NeoSTP.Tests.Integration
```

Criterio de aceptacion:

- Unit tests verdes.
- Integration tests verdes o con fallas documentadas por dependencia local.

### 3. Levantar API

```powershell
dotnet run --project src/NeoSTP.Api
```

Registrar:

- URL HTTP local.
- URL HTTPS local.
- Ambiente ASP.NET usado.
- Base de datos objetivo, sin copiar credenciales.

### 4. Probar disponibilidad local

Abrir o consultar:

```text
GET http://localhost:5058/health
GET https://localhost:7043/health
GET http://localhost:5058/scalar/v1
GET https://localhost:7043/scalar/v1
```

Criterio de aceptacion:

- `/health` responde `status=ok`.
- Scalar carga correctamente.
- OpenAPI esta disponible en `/openapi/v1.json`.

### 5. Exponer solo la API

Opcion recomendada:

```powershell
cloudflared tunnel --url http://localhost:5058
```

Si la API esta escuchando por HTTPS local y el tunnel lo soporta en tu ambiente:

```powershell
cloudflared tunnel --url https://localhost:7043
```

Criterio de aceptacion:

- Se obtiene una URL `https://*.trycloudflare.com`.
- `GET {API_BASE_URL}/health` responde desde otra red.
- No se expone SQL Server ni otro puerto interno.

## Matriz de pruebas manuales/API

| ID | Escenario | Endpoint | Resultado esperado |
|---|---|---|---|
| API-01 | Health local | `GET /health` | API responde `status=ok`. |
| API-02 | Health via tunnel | `GET /health` | Respuesta correcta desde URL publica temporal. |
| API-03 | Login demo | `POST /api/auth/login` | Devuelve `accessToken`, `refreshToken`, `user.empresaId` y permisos. |
| API-04 | Login SuperAdmin | `POST /api/auth/login` | La app debe bloquearlo por `SUPERADMIN` o `empresaId == null`. |
| API-05 | Perfil | `GET /api/auth/me` | Devuelve usuario de empresa y permisos vigentes. |
| API-06 | Dashboard | `GET /api/dashboard/empresa` | Devuelve KPIs reales de la empresa. |
| API-07 | Cobros resumen | `GET /api/cobros/resumen` | Devuelve resumen o ceros controlados. |
| API-08 | Alertas resumen | `GET /api/alertas/resumen` | Devuelve pendientes/criticas/advertencias. |
| API-09 | Lista DTE | `GET /api/dte/documentos?page=1&pageSize=20` | Devuelve lista paginada. |
| API-10 | Detalle DTE | `GET /api/dte/documentos/{id}` | Devuelve detalle completo del documento. |
| API-11 | PDF DTE | `GET /api/dte/documentos/{id}/pdf` | Devuelve `application/pdf`. |
| API-12 | Clientes lookup | `GET /api/lookups/clientes?search=` | Devuelve resultados ligeros. |
| API-13 | Productos lookup | `GET /api/lookups/productos?search=` | Devuelve resultados ligeros. |
| API-14 | Emitir DTE | `POST /api/dte/emitir` | Devuelve documento final o error fiscal con `traceId`. |
| API-15 | Error controlado | Request invalido | Respuesta `success=false`, `message`, `errors`, `traceId`. |
| API-16 | Token vencido | Llamada con access token invalido | App intenta refresh una sola vez y reintenta. |

## Flujo de prueba Flutter

### Fase 1 - Conectividad

1. Configurar `API_BASE_URL` con la URL HTTPS temporal.
2. Abrir app.
3. Splash llama `GET /health`.
4. Mostrar estado de API disponible o error de red.

Criterios de aceptacion:

- La app muestra la URL base usada en modo debug/diagnostico.
- Los errores de red distinguen API caida, URL incorrecta y timeout.

### Fase 2 - Autenticacion

1. Login con usuario demo de empresa.
2. Guardar `accessToken` y `refreshToken`.
3. Cargar `/api/auth/me`.
4. Navegar al dashboard.

Criterios de aceptacion:

- El usuario demo entra correctamente.
- SuperAdmin queda bloqueado.
- La app no envia `empresaId`.
- Logout revoca/limpia sesion local.

### Fase 3 - Dashboard

1. Cargar `/api/dashboard/empresa`.
2. Cargar `/api/cobros/resumen`.
3. Cargar `/api/alertas/resumen`.

Criterios de aceptacion:

- Los KPIs se renderizan con datos reales o ceros validos.
- 403/402 se manejan con UI controlada, sin crash.
- Cada error muestra mensaje util y `traceId`.

### Fase 4 - Consulta DTE

1. Listar DTE paginados.
2. Filtrar por estado/tipo/fecha si la UI ya lo permite.
3. Abrir detalle.
4. Descargar/abrir/compartir PDF.

Criterios de aceptacion:

- La lista no carga todos los documentos de una vez.
- El detalle muestra estado, cliente, fecha, total y tipo DTE.
- PDF abre o se comparte correctamente.

### Fase 5 - Emision rapida DTE

1. Buscar cliente.
2. Buscar producto.
3. Crear payload de factura consumidor final o credito fiscal.
4. Ejecutar `POST /api/dte/emitir`.
5. Mostrar resultado final.

Criterios de aceptacion:

- Si Hacienda procesa, mostrar `PROCESADO` y permitir PDF.
- Si Hacienda rechaza, mostrar `RECHAZADO`, mensaje y `traceId`.
- Si hay contingencia, mostrar `CONTINGENCIA`.
- La app no calcula impuestos como fuente de verdad; muestra totales del backend.

### Fase 6 - Pruebas de permisos

Probar al menos dos usuarios:

- Usuario con permisos completos.
- Usuario con permisos limitados.

Criterios de aceptacion:

- Acciones sin permiso se ocultan o deshabilitan.
- Si el backend responde 403, la app lo maneja sin crash.

## Datos de prueba sugeridos

Crear o identificar:

- Empresa demo con DTE completo en ambiente `PRUEBAS`.
- Usuario demo de empresa.
- Cliente consumidor final.
- Cliente contribuyente para credito fiscal.
- Producto gravado.
- Producto exento/no sujeto si aplica.
- DTE previamente procesado para probar consulta/PDF.
- DTE a credito para probar cobros.
- Certificado vigente y credenciales MH validas.

No registrar en este documento:

- Passwords.
- Tokens.
- Connection strings.
- Certificados.
- Password MH.

## Evidencias a guardar

Por cada corrida:

- Fecha y hora.
- Commit/backend version.
- Version/build de la app Flutter.
- `API_BASE_URL` sin credenciales.
- Usuario usado, sin password.
- Lista de casos ejecutados.
- Resultado: aprobado/fallido/bloqueado.
- `traceId` de cada error.
- Capturas solo si no muestran secretos.

Formato recomendado:

```text
Fecha:
Backend commit:
Flutter commit/build:
API_BASE_URL:
Empresa:
Usuario demo:

Casos:
- API-01: OK
- API-02: OK
- API-03: OK
- API-14: FAIL - HACIENDA_AUTH_FAILED - traceId=...

Hallazgos:
- Backend:
- Flutter:
- Datos/configuracion:
- Ambiente:
```

## Criterios de cierre

La ronda de pruebas se considera aprobada cuando:

1. La API local responde por URL HTTPS temporal.
2. Flutter hace login real con usuario de empresa.
3. Flutter bloquea SuperAdmin.
4. El refresh token funciona ante 401.
5. Dashboard carga datos reales o estados vacios correctos.
6. Consulta DTE lista, abre detalle y descarga PDF.
7. Emision rapida genera un resultado fiscal controlado.
8. Clientes/productos se buscan con paginacion o lookups.
9. Permisos limitan las acciones visibles.
10. Los errores muestran `traceId`.
11. No se compartieron ni registraron secretos.
12. No se expuso SQL Server.

## Riesgos y mitigaciones

| Riesgo | Impacto | Mitigacion |
|---|---|---|
| Tunnel temporal cambia URL | Flutter deja de conectar | Configurar `API_BASE_URL` por ambiente/build. |
| Certificado/MH mal configurado | Emision falla | Probar `dte/configuracion/probar-conexion` antes de emitir. |
| Usuario demo sin permisos | 403 en app | Validar `/api/auth/me` y permisos antes de la demo. |
| Base local con datos inconsistentes | Pruebas no reproducibles | Preparar empresa demo y datos minimos antes de la ronda. |
| Logs/capturas con secretos | Riesgo de seguridad | No registrar tokens, passwords, certificados ni connection strings. |
| API local se duerme o cambia puerto | Tunnel apunta mal | Validar `/health` local y remoto antes de cada sesion. |

## Comandos utiles

```powershell
dotnet build NeoSTP.slnx
dotnet test tests/NeoSTP.Tests.Unit
dotnet test tests/NeoSTP.Tests.Integration
dotnet run --project src/NeoSTP.Api
cloudflared tunnel --url http://localhost:5058
```

## Informacion para entregar al desarrollador Flutter

Entregar solo:

- `API_BASE_URL=https://xxxxx.trycloudflare.com`
- Usuario demo.
- Password demo por canal seguro.
- Link o copia de `docs/NeoCloud-Mobile-API.md`.
- Este plan de pruebas.

No entregar:

- SQL Server host/usuario/password.
- Connection string.
- Certificado DTE.
- Password MH.
- JWT o refresh token.
- Archivos `appsettings` con secretos.
