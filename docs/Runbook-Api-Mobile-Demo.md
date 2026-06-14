# Runbook API Mobile Demo

> Fecha: 2026-06-14. Alcance: preparar y validar una demo de la API que consume
> `manuelberganza-dev/neocloud_mobile_android`. No modifica Flutter.

## Objetivo

Dejar una corrida repetible para demostrar NeoCloud Mobile contra `NeoSTP.Api` con usuarios de empresa,
datos visibles y providers seguros por defecto.

## Estado de Validacion 2026-06-14

- Plan API mobile AM-0..AM-6 cerrado operativo al 100%.
- HB-1 cerrado sin cambios de contrato HTTP: solo limpieza de warnings en Billing, Billing Portal e
  Infrastructure.
- Validacion local: `dotnet build NeoSTP.slnx` = 0 warnings/0 errores.
- Validacion local: `dotnet test NeoSTP.slnx` = 697 unitarias + 9 integracion verdes.

## Configuracion Minima

Usar `src/NeoSTP.Api/appsettings.Local.json` o user-secrets. No commitear secretos.

```json
{
  "EmpresaPrueba": {
    "Enabled": true,
    "Nit": "06140000000000",
    "RazonSocial": "NeoSTP Pruebas, S.A. de C.V.",
    "PlanCodigo": "ENTERPRISE",
    "Admin": {
      "Username": "admin.prueba",
      "Password": "ChangeMe!2026"
    },
    "MobileDemo": {
      "Enabled": true,
      "Password": "MobileDemo!2026"
    }
  },
  "Scan": {
    "Provider": "Mock",
    "LimiteMensual": 0,
    "ConfianzaMinimaProcesado": 0.8,
    "OcrTimeoutSeconds": 25,
    "AllowedContentTypes": "image/jpeg,image/png,application/pdf"
  },
  "Push": { "Provider": "Mock" },
  "Email": { "Provider": "Mock" }
}
```

Para probar OCR real:

```json
{
  "Scan": {
    "Provider": "Gemini",
    "ConfianzaMinimaProcesado": 0.8,
    "OcrTimeoutSeconds": 25,
    "AllowedContentTypes": "image/jpeg,image/png,application/pdf",
    "Gemini": {
      "ApiKey": "GOOGLE_AI_STUDIO_KEY",
      "Model": "gemini-2.0-flash"
    }
  }
}
```

La API key de Gemini viaja por header `x-goog-api-key`; no debe aparecer en URL, logs ni capturas.

## Usuarios Demo

Todos se crean cuando `EmpresaPrueba:MobileDemo:Enabled=true`.

| Usuario | Rol | Uso |
|---|---|---|
| `mobile.admin` | `ADMIN` | Smoke completo. |
| `mobile.dte.consulta` | `CONTADOR` | DTE lectura sin `DTE.Emitir`. |
| `mobile.pos` | `OPERADOR` | POS, caja, ticket, promocion. |
| `mobile.cobros` | `OPERADOR` | Cobros, pagos y QR. |
| `mobile.scan` | `OPERADOR` | NeoScan bandeja/captura/confirmacion. |
| `mobile.limitado` | `READONLY` | Negativos de permisos/modulos. |

Password por defecto de esos usuarios: `EmpresaPrueba:MobileDemo:Password`.

## Datos Sembrados

El seeder mobile crea de forma idempotente:

| Dato | Cantidad | Validacion |
|---|---:|---|
| Clientes | 3 | Lookups, facturacion, CxC. |
| Productos | 5 | Lookups, POS, DTE. |
| DTE procesados | 2 | Lista, detalle, PDF/JSON. |
| DTE a credito | 1 | Cobros pendientes y QR. |
| Pago parcial | 1 | Saldo restante visible. |
| Cuenta de cobro | 1 | Generacion de QR. |
| Caja POS | 1 cerrada + 1 abierta | Estado, cierre, negativos. |
| Venta POS | 1 | Ticket PDF y resumen. |
| Scan con archivo | 1 | Bandeja, archivo, correccion. |
| Alertas | 2 | Una pendiente para badge y una resuelta para historial/filtros. |

## Preparacion

1. Restaurar y compilar:

```powershell
dotnet build NeoSTP.slnx
```

2. Ejecutar pruebas dirigidas o la suite completa antes de demo:

```powershell
dotnet test NeoSTP.slnx
dotnet test tests\NeoSTP.Tests.Unit\NeoSTP.Tests.Unit.csproj --filter "FullyQualifiedName~MobileApiContractCoverageTests|FullyQualifiedName~DteControllerMobileContractTests|FullyQualifiedName~EmpresaPruebaSeederTests|FullyQualifiedName~Scan"
dotnet test tests\NeoSTP.Tests.Integration\NeoSTP.Tests.Integration.csproj --filter "FullyQualifiedName~MobileApiContract"
```

3. Levantar API:

```powershell
dotnet run --project src\NeoSTP.Api
```

4. Validar:

```text
GET http://localhost:5058/health
GET http://localhost:5058/scalar/v1
GET http://localhost:5058/openapi/v1.json
```

5. Si se requiere prueba desde telefono, publicar solo la API:

```powershell
cloudflared tunnel --url http://localhost:5058
```

Configurar la app con `API_BASE_URL=https://xxxxx.trycloudflare.com`.

## Checklist API Mobile

| ID | Endpoint | Usuario | Esperado |
|---|---|---|---|
| MAPI-01 | `GET /health` | anon | 200, `data.status=ok`. |
| MAPI-02 | `POST /api/auth/login` | `mobile.admin` | Tokens y `empresaId`. |
| MAPI-03 | `GET /api/auth/me` | `mobile.admin` | Roles/permisos efectivos. |
| MAPI-04 | `GET /api/dashboard/empresa` | `mobile.admin` | KPIs no vacios o ceros controlados. |
| MAPI-05 | `GET /api/clientes` | `mobile.admin` | `PagedResult`. |
| MAPI-06 | `GET /api/productos` | `mobile.admin` | `PagedResult`. |
| MAPI-07 | `GET /api/dte/documentos` | `mobile.dte.consulta` | 200 sin exigir `DTE.Emitir`. |
| MAPI-08 | `GET /api/dte/documentos/{id}` | `mobile.dte.consulta` | Detalle. |
| MAPI-09 | `GET /api/dte/documentos/{id}/pdf` | `mobile.dte.consulta` | Bytes PDF, sin `ApiResponse`. |
| MAPI-10 | `GET /api/dte/documentos/{id}/json` | `mobile.dte.consulta` | JSON crudo, sin envelope. |
| MAPI-11 | `GET /api/cobros/resumen` | `mobile.cobros` | CxC visible. |
| MAPI-12 | `GET /api/cobros/pendientes` | `mobile.cobros` | DTE credito con saldo. |
| MAPI-13 | `POST /api/cobros/qr` | `mobile.cobros` | `qrPngBase64`. |
| MAPI-14 | `GET /api/pos/caja/estado` | `mobile.pos` | Caja abierta o estado controlado. |
| MAPI-15 | `GET /api/pos/ventas/{id}/ticket` | `mobile.pos` | Bytes PDF. |
| MAPI-16 | `GET /api/scanai/documentos` | `mobile.scan` | Bandeja con documento demo. |
| MAPI-17 | `GET /api/scanai/documentos/{id}/archivo` | `mobile.scan` | Bytes PDF/imagen. |
| MAPI-18 | `POST /api/scanai/documentos` | `mobile.scan` | Rechaza MIME no permitido o crea documento valido. |
| MAPI-19 | `POST /api/scanai/documentos/{id}/reprocesar` | `mobile.scan` | Reintento OCR sin duplicar documento. |
| MAPI-20 | `GET /api/alertas/resumen` | `mobile.admin` | Badge con pendiente. |
| MAPI-21 | `POST /api/alertas/dispositivos` | `mobile.admin` | Token registrado. |
| MAPI-22 | Endpoint protegido | sin token | 401. |
| MAPI-23 | Accion sin permiso | `mobile.limitado` | 403/402 legible, sin 500. |

## Flujo POS

1. Login con `mobile.pos`.
2. `GET /api/pos/caja/estado`.
3. Si no hay caja abierta, `POST /api/pos/caja/abrir`.
4. `POST /api/pos/ventas` con producto `MOB-CAFE`.
5. `GET /api/pos/ventas/{id}/ticket`.
6. `POST /api/pos/ventas/{id}/promover` solo si se quiere validar DTE; aceptar error fiscal controlado si Hacienda esta en Mock/credenciales incompletas.
7. `POST /api/pos/caja/{id}/cerrar`.

## Flujo Cobros

1. Login con `mobile.cobros`.
2. `GET /api/cobros/resumen`.
3. `GET /api/cobros/pendientes`.
4. `POST /api/cobros/qr` usando el DTE a credito demo.
5. `POST /api/cobros/dte/{id}/pagos` con monto menor o igual al saldo.
6. Validar duplicados, monto mayor al saldo y anulacion con mensajes `ApiResponse` legibles.

## Flujo Alertas

1. Login con `mobile.admin`.
2. `GET /api/alertas/resumen`.
3. `GET /api/alertas`.
4. `POST /api/alertas/{id}/leer`.
5. `POST /api/alertas/{id}/resolver`.
6. `POST /api/alertas/dispositivos` con token dummy `demo-mobile-token`.

FCM real queda fuera de la demo salvo que existan credenciales `Push:Fcm`. El polling de alertas debe funcionar siempre.

## Evidencia

Registrar por corrida:

```text
Fecha:
Branch:
Commit:
Base URL:
Empresa:
Usuario:
Provider Hacienda:
Provider Scan:
Provider Push:

Casos:
- MAPI-01: status=200 traceId= duracionMs=
- MAPI-02: status=200 traceId= duracionMs=

Errores:
- endpoint:
  status:
  code:
  traceId:
  resumen:

Decision: apto demo / apto con advertencias / no apto
```

No guardar tokens, passwords, certificados, connection strings ni API keys.

## Politica de Compatibilidad Mobile

- Campos nuevos en DTOs son compatibles.
- No renombrar ni eliminar campos usados por la app sin versionar.
- JSON operativo debe seguir en `ApiResponse<T>`.
- Descargas binarias deben seguir como bytes crudos.
- Listados deben seguir con `PagedResult<T>`.
- Mobile no envia `empresaId`; el tenant sale del JWT.
- SuperAdmin no es usuario mobile.
- Cambios breaking requieren version nueva o coordinacion con la app Android.
