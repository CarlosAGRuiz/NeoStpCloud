# Runbook HB-8 - Demo y Release

> Fecha: 2026-06-19. Alcance: preparar, ejecutar y cerrar demos comerciales/tecnicas y releases
> de NeoSTP Cloud sin depender de conocimiento tribal ni exponer secretos.

## Objetivo

Una corrida es repetible cuando queda asociada a branch, commit, ambiente, providers, roles,
resultado de build/tests, health checks y decision final. Este runbook complementa:

- `docs/Plan-Pruebas-Web-Api-Demos.md` para la matriz extensa Web/API.
- `docs/Runbook-Api-Mobile-Demo.md` para la app Android existente.
- `docs/Runbook-Storage-Secretos-Retencion.md` para secretos, storage y retencion.
- `docs/Runbook-V2.md` para backup, restore, migraciones y operacion.

## Preflight Ejecutable

Script: `scripts/demo-preflight.ps1`.

Valida:

- .NET SDK 10 y raiz de solucion.
- Branch, commit y estado del worktree.
- Ausencia de `appsettings.Local.json`, certificados y llaves privadas tracked por Git.
- Providers soportados sin imprimir credenciales.
- Fortaleza minima de `Jwt:Key` sin mostrar su valor.
- Seed demo habilitado para perfil `Demo` y deshabilitado para `Release`.
- `Hacienda:Client=Http` y `Dte:Signer=HaciendaCert` para perfil `Release`.
- Build y suite completa, salvo que se use `-StaticOnly`.
- Restore NuGet solo cuando se solicita `-Restore`; por default usa assets ya restaurados para
  permitir preflight offline reproducible.
- Health API/Web y OpenAPI cuando se proporcionan URLs.
- Evidencia JSON sanitizada con decision y duraciones.

Decisiones posibles:

| Decision | Significado |
|---|---|
| `APTO_DEMO` | Sin fallos ni advertencias para demo. |
| `APTO_RELEASE` | Sin fallos ni advertencias para release. |
| `APTO_CON_ADVERTENCIAS` | No hay fallos, pero existen omisiones o riesgos que deben aceptarse. |
| `NO_APTO` | Existe al menos un bloqueo; no presentar ni desplegar. |

### Ensayo estatico

Util durante preparacion o CI documental. No sustituye build, tests ni smoke HTTP:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\demo-preflight.ps1 `
  -Profile Demo -StaticOnly -AllowDirtyWorktree `
  -EvidencePath tmp\demo-preflight-static.json
```

### Preflight completo sin servicios

Ejecutar con worktree limpio:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\demo-preflight.ps1 `
  -Profile Demo -EvidencePath tmp\demo-preflight.json
```

En un checkout nuevo agregar `-Restore`. Si restore o build fallan, el script no ejecuta tests y
devuelve `NO_APTO`.

### Preflight completo con API/Web

Con API en `http://localhost:5058` y Web en `http://localhost:5031`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\demo-preflight.ps1 `
  -Profile Demo -RequireServices `
  -ApiBaseUrl http://localhost:5058 `
  -WebBaseUrl http://localhost:5031 `
  -EvidencePath tmp\demo-preflight-online.json
```

Para produccion usar `-Profile Release`, URLs reales y un archivo de evidencia fuera del repo.
`-AllowDirtyWorktree` nunca se acepta para release.

## Preparacion del Ambiente

### 1. Codigo y artefactos

- Worktree limpio y commit identificado.
- `src/NeoSTP.Api/README.md`, README raiz y contexto alineados.
- No usar binarios de una compilacion anterior.
- Para release, etiqueta/version definida despues de aprobar el preflight.

### 2. Configuracion y secretos

- Secretos en variables de entorno, User Secrets o `appsettings.Local.json` ignorado.
- API y Web comparten `Jwt:Key`.
- API/Web usan la misma BD y configuracion DTE coherente.
- Declarar si Hacienda, firma, correo, Scan, push, WhatsApp, cache y Billing estan en Mock o real.
- No mostrar passwords, tokens, certificados, connection strings ni API keys durante la demo.
- Seguir `docs/Runbook-Storage-Secretos-Retencion.md` si NeoScan usa filesystem.

### 3. Base de datos y migraciones

- Backup FULL reciente antes de release o migracion.
- Revisar migraciones pendientes y SQL generado; no aplicar a produccion a ciegas.
- API/Web deben devolver `/health/ready` verde despues de migrar.
- Tener identificado backup y procedimiento de rollback antes de iniciar release.

### 4. Datos demo

Para demo local/staging:

```json
{
  "EmpresaPrueba": {
    "Enabled": true,
    "MobileDemo": { "Enabled": true }
  }
}
```

El seed debe dejar DTE, CxC, POS/caja, orden de compra parcialmente recibida, compras/CxP, inventario, tesoreria, portal, NeoScan, CRM,
RRHH y Profit. Es idempotente; dos arranques no deben duplicar escenarios.

Para release productivo, `EmpresaPrueba.Enabled` y `MobileDemo.Enabled` deben estar deshabilitados.

### 5. Usuarios y roles

| Perfil | Uso |
|---|---|
| `ADMIN` | Recorrido comercial principal sin bypass de permisos. |
| `OPERADOR` / `mobile.pos` | POS, caja, ticket y promocion a DTE. |
| `CONTADOR` | Compras, libros, tesoreria, conciliacion y contabilidad. |
| `mobile.dte.consulta` | Lectura DTE sin permiso de emision. |
| `mobile.limitado` | Negativos de permisos/modulos. |
| `SUPERADMIN` | Solo soporte, operacion y seleccion explicita de empresa. |
| Receptor publico | Portal por token sin login. |
| API Key NeoConnect | Integracion por scopes y cuotas. |

No usar `SUPERADMIN` para demostrar el flujo normal de empresa.

## Arranque Local

Consolas separadas:

```powershell
dotnet run --project src\NeoSTP.Api
dotnet run --project src\NeoSTP.Web
dotnet run --project src\NeoSTP.Worker
```

El Worker solo es obligatorio si se demostraran recordatorios, alertas programadas, backups o
webhooks. Confirmar:

- API: `http://localhost:5058/health/live`, `/health/ready`, `/openapi/v1.json`, `/scalar/v1`.
- Web: `http://localhost:5031/health/live`, `/health/ready`.

## Guion Comercial Recomendado

El guion sigue problema -> flujo -> resultado -> valor. Duracion objetivo: 45 a 60 minutos.

| Bloque | Problema del cliente | Flujo | Resultado/valor |
|---|---|---|---|
| 1. Dashboard | Informacion dispersa | Login `ADMIN`, KPIs, alertas, onboarding | Estado del negocio en una pantalla. |
| 2. DTE | Facturacion y cumplimiento | Cliente/producto -> emitir -> PDF/JSON -> estado MH | Documento fiscal trazable y listo para entregar. |
| 3. POS/caja | Venta diaria desconectada | Abrir caja -> vender -> ticket -> promover DTE -> corte | Venta, inventario y facturacion integrados. |
| 4. Cobros/portal | Cartera y consultas manuales | Saldo -> pago parcial -> QR -> portal receptor | Menos seguimiento manual y autoservicio. |
| 5. Compras/inventario | CxP y stock separados | Orden -> emitir -> recibir parcial/completa -> kardex -> CxP consolidada -> pago | Compra autorizada, costo y existencia actualizados. |
| 6. NeoScan | Digitacion de comprobantes | Subir -> OCR Mock/Gemini -> corregir -> confirmar | Captura asistida sin perder control humano. |
| 7. Tesoreria/fiscal | Cierre lento | Importar banco -> conciliar -> libros/F-07 -> balanza | Evidencia financiera y fiscal conectada. |
| 8. Integraciones/mobile | Sistemas aislados | API Key/scopes/webhook + contrato mobile | Plataforma extensible sin backend paralelo. |

Reglas del presentador:

- Declarar proveedores Mock/reales antes de mostrar cada integracion.
- No improvisar datos fiscales ni transmitir a produccion durante una demo.
- Aceptar solo errores de negocio previamente documentados; un 500 bloquea el flujo.
- Conservar `traceId` de cualquier error, sin guardar el token o payload sensible.

## Demo Tecnica API

Orden corto:

1. `/health/live`, `/health/ready`, OpenAPI y Scalar.
2. Login de usuario de empresa y `/api/auth/me`.
3. Dashboard/lookups/clientes/productos.
4. DTE listado/emision/descargas binarias.
5. POS, Cobros, orden -> recepciones -> inventario -> factura/CxP, NeoScan y Alertas.
6. NeoConnect `/api/v1/ping` con scope minimo.
7. Negativos: sin token, permiso faltante, modulo no contratado, tenant cruzado y conversion duplicada de orden.

La matriz completa esta en `docs/Plan-Pruebas-Web-Api-Demos.md` y el contrato Android en
`docs/Runbook-Api-Mobile-Demo.md`.

## Criterios de NO DEMO / NO RELEASE

Bloqueos absolutos:

- Build o tests rojos.
- `/health/ready` rojo en API o Web.
- Migraciones pendientes/no revisadas o backup inexistente antes de produccion.
- Login `ADMIN` falla o hay 403 inesperados en el guion.
- DTE no puede completar el modo declarado (Mock/apitest/produccion).
- Provider real sin credenciales verificadas o provider Mock presentado como real.
- Portal valido no abre, descargas binarias regresan envelope JSON o hay errores 500.
- Datos demo criticos vacios o seed productivo habilitado.
- Worktree sucio para release.
- Secretos, certificados o datos reales visibles en repo, pantalla, logs o evidencia.

Una decision `NO_APTO` del preflight no se puede convertir manualmente en apto sin corregir el
check que fallo y generar evidencia nueva.

## Release

1. Congelar commit y changelog.
2. Ejecutar preflight `Release` con URLs de staging y evidencia fuera del repo.
3. Confirmar backup FULL, RPO/RTO y rollback.
4. Revisar/aplicar migraciones en staging; ejecutar smoke.
5. Publicar API, Web y Worker del mismo commit.
6. Aplicar migraciones productivas una sola vez.
7. Validar health, login, DTE de control y logs.
8. Etiquetar release solo despues de health/smoke verdes.
9. Si falla health o flujo critico, detener despliegue y ejecutar rollback documentado.

## Post-demo / Post-release

- Revocar tokens de portal y API keys temporales.
- Eliminar dispositivos push/token dummy y tunnels temporales.
- Limpiar archivos/exportes con datos sensibles.
- Revertir providers reales activados solo para ensayo.
- Registrar hallazgos con ruta, rol, empresa, `traceId`, severidad y responsable.
- Guardar evidencia JSON y completar `docs/templates/Evidencia-Demo-Release.md` fuera del repo si
  contiene nombres de clientes o URLs privadas.
- Rotar cualquier secreto mostrado o compartido accidentalmente.

## Evidencia Minima

Cada corrida debe guardar:

- Fecha/hora UTC, perfil, branch y commit.
- Ambiente y URLs, sin query strings sensibles.
- Providers activos, nunca sus credenciales.
- Build, tests, health y OpenAPI.
- Empresa/roles usados, sin passwords.
- Casos ejecutados, status, duracion y `traceId` de errores.
- Capturas clave sanitizadas.
- Hallazgos y decision final.

Plantilla: `docs/templates/Evidencia-Demo-Release.md`.

## Cierre HB-8

- Preflight ejecutable genera evidencia JSON sanitizada y codigo de salida no cero ante fallos.
- Runbook cubre demo comercial, tecnica API, mobile, release y post-demo.
- Criterios de no demo/release son explicitos.
- Plantilla de evidencia queda versionada.
- Pruebas HB-8, build y suite completa quedan verdes.
