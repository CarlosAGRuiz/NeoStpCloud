# API - Contratos y Versionado

> Estado HB-6: cerrado operativo 2026-06-15. Este documento es la politica fuente para mantener
> compatibles la app Android, las demos Web/API y las integraciones NeoConnect.

## Objetivo

Evitar cambios accidentales en rutas, envelopes, descargas binarias y DTOs que rompan clientes ya
construidos. La API de NeoSTP tiene dos superficies con reglas distintas:

- **Tier A - API interna/mobile/demo (`/api/*`)**: rutas usadas por la Web, la app Android y demos.
- **Tier B - API publica NeoConnect (`/api/v1/*`)**: rutas servidor-a-servidor para integradores con API Key.
- **Tier C - soporte/admin**: endpoints administrativos internos; siguen el mismo envelope, pero pueden
  evolucionar junto con la Web si se actualiza README/API y pruebas.

## Politica de Versionado

### Tier A - `/api/*`

Las rutas internas/mobile no llevan version explicita por ahora. Se consideran estables para la app
Android existente y para demos. Cambios permitidos sin nueva ruta:

- Agregar campos opcionales a responses.
- Agregar filtros query opcionales.
- Agregar endpoints nuevos bajo el mismo recurso.
- Agregar codigos de error mas especificos manteniendo HTTP status razonable.

Cambios que requieren version, alias o coordinacion previa:

- Renombrar o remover campos existentes.
- Cambiar tipo JSON de una propiedad.
- Cambiar `ApiResponse<T>` o `PagedResult<T>`.
- Envolver como JSON una descarga binaria existente.
- Cambiar permisos, modulo requerido o semantica de estado de una ruta consumida por mobile/demo.
- Mover una ruta `/api/*` a `/api/v1/*`.

Si hay que hacer un breaking change interno, se debe crear una ruta paralela o un endpoint nuevo,
mantener la ruta anterior durante una ventana de migracion y actualizar app/docs/tests en el mismo
commit.

### Tier B - `/api/v1/*`

NeoConnect publica la version en path. La version actual es `/api/v1`. Reglas:

- Cambios compatibles entran en `/api/v1`: campos nuevos, filtros opcionales, endpoints nuevos y
  errores mas especificos.
- Un breaking change crea `/api/v2`; `/api/v1` queda operativo durante una ventana de deprecacion.
- La deprecacion debe documentar fecha, reemplazo, cambios de request/response y ejemplos.
- API Key, scopes, cuotas y webhooks no cambian de contrato sin documentacion y pruebas.

## Contratos de Respuesta

Todo endpoint JSON usa `ApiResponse<T>`:

```json
{
  "success": true,
  "message": null,
  "data": {},
  "errors": [],
  "traceId": "0HM..."
}
```

Errores JSON:

```json
{
  "success": false,
  "message": "Descripcion legible",
  "data": null,
  "errors": ["VALIDATION"],
  "traceId": "0HM..."
}
```

Listas paginadas usan `PagedResult<T>` dentro de `data`:

```json
{
  "success": true,
  "data": {
    "items": [],
    "total": 0,
    "page": 1,
    "pageSize": 20,
    "totalPages": 0
  }
}
```

Las descargas binarias no usan envelope JSON en 200 OK. Deben publicar su `Content-Type` en OpenAPI:

| Tipo | Content-Type esperado |
|---|---|
| PDF DTE, tickets, recibos | `application/pdf` |
| JSON DTE sellado | `application/json` como bytes crudos |
| CSV fiscales/contables | `text/csv` |
| Archivo NeoScan | `application/pdf`, `image/jpeg`, `image/png` u `application/octet-stream` |
| Catalogos exportados | `text/csv`, `application/json` o XLSX |

En errores de descarga se mantiene `ApiResponse` con status 4xx/5xx.

## Matriz de Contratos Estables

| Superficie | Base | Cliente principal | Auth | Versionado |
|---|---|---|---|---|
| Auth | `/api/auth` | Web/mobile | JWT | Tier A |
| Dashboard | `/api/dashboard` | Web/mobile/demo | JWT | Tier A |
| DTE config | `/api/dte/configuracion` | Web/mobile | JWT + permisos | Tier A |
| DTE emision/consulta | `/api/dte` | Web/mobile/demo | JWT + permisos | Tier A |
| Clientes/productos/lookups | `/api/clientes`, `/api/productos`, `/api/lookups` | Web/mobile/demo | JWT + permisos | Tier A |
| Cobros/CxC | `/api/cobros` | Web/mobile/demo | JWT + permisos | Tier A |
| NeoScan | `/api/scanai/documentos` | Web/mobile/demo | JWT + modulo | Tier A |
| Alertas/POS | `/api/alertas`, `/api/pos` | Web/mobile/demo | JWT + permisos/modulos | Tier A |
| Compras/inventario/tesoreria | `/api/compras`, `/api/inventario`, `/api/tesoreria` | Web/demo | JWT + permisos/modulos | Tier A |
| Reportes/conta/profit/CRM/RRHH/portal | `/api/reportes/fiscal`, `/api/conta`, `/api/profit`, `/api/crm`, `/api/rrhh`, `/api/portal` | Web/demo/mobile futuro | JWT + permisos/modulos | Tier A |
| NeoConnect publica | `/api/v1` | Integradores | `X-Api-Key` + scopes | Tier B |

## Proceso de Cambio

1. Si se toca un controller, revisar si la ruta esta en la matriz estable.
2. Si cambia request/response, decidir si es compatible o breaking change.
3. Actualizar `src/NeoSTP.Api/README.md`, documento mobile o `docs/NeoConnect-API-v1.md` segun aplique.
4. Ejecutar pruebas contractuales:

```bash
dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj --filter "DemoReadinessContractTests|MobileApiContractCoverageTests|ApiVersioningContractTests"
dotnet test tests/NeoSTP.Tests.Integration/NeoSTP.Tests.Integration.csproj --filter MobileApiContractOperationalTests
```

5. Ejecutar `dotnet build NeoSTP.slnx` y `dotnet test NeoSTP.slnx` antes de cerrar sprint.

## Guardrails Automatizados

- `DemoReadinessContractTests`: rutas API/Web criticas de demo, permisos, modulos y vistas.
- `MobileApiContractCoverageTests`: rutas exactas consumidas por la app Android.
- `MobileApiContractOperationalTests`: fixture demo mobile y shape camelCase.
- `ApiVersioningContractTests`: politica HB-6, ruta publica `/api/v1`, rutas Tier A sin version
  explicita, content-types de descargas binarias y enlaces documentales.

## Criterio de Deprecacion

Una ruta se puede deprecar solo si existe reemplazo documentado, test de contrato actualizado y ventana de
migracion definida. Durante la ventana:

- La ruta vieja sigue respondiendo.
- La respuesta puede incluir headers informativos de deprecacion si se implementan.
- La documentacion indica fecha objetivo de retiro.
- Mobile/integradores tienen ruta de migracion clara.
