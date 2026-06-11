# Plan Fase V2.5 — NeoSTP Cloud

> Inicio: 2026-06-10, inmediatamente despues del cierre de V2 (`docs/Plan-Cierre-Fase-V2.md`).
> Objetivo: convertir lo diferido del bloque E en capacidades reales de operacion/escala,
> y cerrar las dependencias externas que quedaron pluggables, sin romper el default local
> (todo lo nuevo se activa por configuracion; sin Redis/OTLP/Meta configurados, el sistema
> sigue funcionando igual que en V2).

## Alcance (heredado del cierre V2, apartado 6)

| Sprint | Contenido | Origen |
|---|---|---|
| S1 | Conciliacion bancaria **parcial N:1** (una linea del banco ↔ varios movimientos internos) | Nota de alcance D4 |
| S2 | **WhatsApp Business real** (Meta Cloud API) detras de `IWhatsAppSender` | D3 pendiente externo |
| S3 | **Observabilidad E1**: OpenTelemetry (OTLP opcional), metricas por empresa, health ready ampliado, panel operativo | E1 |
| S4 | **Escala E2**: cache distribuida Redis para lookups/licencias con invalidacion + storage externo de blobs (FileSystem) | E2 |
| S5 | **Operacion E3/E4**: purga programada de auditoria por retencion + pipeline CI (GitHub Actions) | E3/E4 automatizacion |
| S6 | **UX E5**: a11y base (skip-link, aria, foco, labels) + i18n es/en (plumbing + layout/login) | E5 |
| S7 | Cierre: suites + pruebas tipo cliente de lo nuevo + docs | — |

Quedan **fuera** de V2.5 (V3 o cuando exista el insumo externo): OCR/FCM ya entregados en V2;
verificacion NIT en linea con MH (no hay API publica), SSO/SAML, white label avanzado,
marketplace, multi-moneda avanzada, app Flutter (repo aparte).

## Reglas

1. Compatibilidad: defaults = comportamiento V2 (Memory cache, storage en BD, sin OTLP, WhatsApp mock).
2. Todo proveedor externo entra como implementacion de una interfaz existente + toggle de config.
3. Cada sprint: build verde + tests + migracion si toca esquema + actualizacion de este plan.
4. Secretos solo en `appsettings.Local.json`/entorno (tokens Meta, Redis connection string, OTLP endpoint).

## Estado — FASE V2.5 CERRADA (2026-06-10)

| Sprint | Estado | Entregado |
|---|---|---|
| S1 Conciliacion N:1 | ✅ | `ConciliacionDetalle` (`Tes_ConciliacionDetalles`, indice unico por movimiento interno), estado PARCIAL, `SugerirCombinaciones` (pares/trios que suman exacto, acotado a 12 candidatos), conciliar-combinacion/quitar detalle en API y UI, migracion `V25_S1_ConciliacionParcial` con backfill de las 1:1 existentes. +4 tests (14 de conciliacion). |
| S2 WhatsApp real | ✅ | `MetaWhatsAppSender` (Meta Cloud API, `POST /{ver}/{phoneId}/messages` tipo text), normalizacion E.164 (+503 por defecto), toggle `WhatsApp:Provider=Meta` con resiliencia HTTP; mock sigue de default. Nota: fuera de la ventana de 24 h Meta exige plantillas aprobadas. 9 tests. |
| S3 Observabilidad | ✅ | OpenTelemetry opcional (`Observability:Otlp:Endpoint`): trazas+metricas ASP.NET Core/HttpClient/runtime + Meter `NeoSTP` (DTE emitidos/errores MH/recordatorios/accesos portal) en API/Web/Worker; health ready ampliado (BD+correo+storage); panel SuperAdmin `/Soporte/Operacion` con metricas cross-tenant desde BD. |
| S4 Redis + storage | ✅ | `Cache:Provider=Memory|Redis` (StackExchangeRedis) con L1 por peticion + L2 distribuida versionada para lookups e invalidacion al mutar catalogos (`ILookupCacheInvalidator`); storage externo de escaneos `Scan:Storage:Provider=FileSystem` (`ArchivoPath`, bytes fuera de BD), migracion `V25_S4_ScanArchivoPath`. +4 tests. |
| S5 Purga + CI | ✅ | `LimpiezaAuditoriaService` (lotes, retencion minima 30 dias) + `LimpiezaAuditoriaWorker` (`Worker:LimpiezaAuditoria`, off por defecto). El pipeline CI ya existia (M5.3); verificado vigente. +3 tests. |
| S6 a11y + i18n | ✅ | `AddLocalization` + `RequestLocalization` (es default, en), `SharedResource` es/en, selector ES/EN en el menu de usuario (cookie de cultura, accion `Home/CambiarIdioma`), `<html lang>` dinamico, skip-link, `:focus-visible`, aria-labels en botones de icono y landmarks. |
| S7 Cierre | ✅ | Pruebas en vivo (abajo) + suites **673 unit + 7 integracion, 0 fallos**. |

## Pruebas tipo cliente (2026-06-10, API 5058 + Web 7098 reales)

- `health/ready` en API y Web devuelve los 3 checks: database, correo (SMTP real detectado), storage. ✔
- Conciliacion N:1 en vivo: 2 egresos internos ($300 + $200) + cargo agrupado del banco (-$500) →
  la sugerencia devolvio la **combinacion [3,4] exacta**; `conciliar-combinacion` la aplico;
  quitar un detalle dejo la linea **PARCIAL $300/$500**; reaplicar la completo; resumen cuadrado. ✔
- i18n con sesion real: shell en `lang="es"` con "Cerrar sesión"/"Saltar al contenido principal" y,
  con cookie de cultura `en`, `lang="en"` con "Sign out"/"Skip to main content". ✔
- `/Soporte/Operacion` como ADMIN de empresa → acceso denegado (redirect estandar de cookie auth). ✔
- Quedan activables por config (sin tocar codigo): OTLP endpoint, Redis, storage FileSystem,
  WhatsApp Meta y purga de auditoria.
