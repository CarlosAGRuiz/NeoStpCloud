# Plan V2 consolidado — NeoSTP como ERP + CRM-mini

> Une **lo que ya estaba pendiente** (ejes M y módulos N del `Plan-V2.md`) con **lo nuevo**
> (ERP interno: nómina/planilla, tesorería, CxP, contabilidad). Objetivo doble: producto SaaS
> **y** operar la propia empresa sin pagar herramientas externas (nómina, planillas, admin).
> El Salvador-first. Decisiones tomadas: **planilla quincenal** · **tablas ISSS/AFP/Renta 2026** (parametrizables).

---

## 1. Ya cerrado en V2 (no re-hacer)

- **M1** Paridad Web↔API (Cobros, Cuentas/QR, NeoScan, Alertas, DTE recibidos).
- **M2** Integraciones reales: OCR **Gemini** + Push **FCM** (toggle, secretos externos).
- **M3.2** Health checks `/health/{live,ready}` · **M3.4** Auditoría consultable + export CSV.
- **M4.3** Índices revisados (+`Cobros_Pagos`) · **M4.4** Cola de trabajo en proceso.
- **M5.1** Tests de integración · **M5.3** CI (GitHub Actions).
- **M6.1** User Secrets · **M6.2** Política de contraseña + bloqueo configurables.
- **M7** Design system unificado (`ns-page-head` en todas las grids, 0 emojis, CsvExporter).
- **M8.1** Dockerización (Web/Api/Worker + compose).

## 2. Base reutilizable (≈60-70% de un ERP-mini ya existe)

Empresas/Sucursales/Usuarios/RBAC · **Clientes** (→CRM) · **Productos** (→inventario/POS) ·
**DTE** (8 tipos, JSON listo) · **Cobros/CxC** (→espejo CxP) · **NeoProfit** (P&L) ·
**Gastos/Compras** (informal) · Auditoría · **Cola de trabajo** · **CsvExporter** · **PDF** (QuestPDF) ·
Alertas+FCM · Billing+pasarelas.

---

## 3. Backlog consolidado

### 3.1 Ejes transversales (M) pendientes
| Eje | Pendiente |
|---|---|
| M3.1 | OpenTelemetry (tracing/métricas → OTLP) |
| M3.3 | Panel SaaS de métricas (uso por empresa, DTE/min, errores MH) |
| M4.1 | Blobs a storage externo (`IStorageService` S3/Azure) |
| M4.2 | Caché distribuida (Redis) para catálogos/lookups/licencias |
| M5.2 | Tests de contrato OpenAPI · M5.4 cobertura/reporte |
| M6.3 | Rotación claves JWT/firma · retención/borrado GDPR · backups restaurables |
| M6.4 | RBAC más granular + auditoría de acciones críticas |
| M7.2 | Accesibilidad (a11y) + i18n (es/en) |
| M8.2 | Pipeline de despliegue por entorno · M8.3 seed demo reproducible |

### 3.2 Módulos del producto (N) pendientes
> **Alcance de este repo (backend/API + web).** La **app Flutter** (`neocloud_mobile_android`)
> y la UI de **NeoScan** NO se desarrollan aquí: viven en la app y consumen **esta misma API**.
> Aquí mantenemos el **backend** que la app necesita (incl. `/api/scanai` con OCR Gemini ya hecho)
> y agregamos **módulos de consulta** (endpoints + web) que app y web reutilizan.

| N | Módulo | Estado / Alcance aquí |
|---|---|---|
| N1 | **NeoPOS** (`NEOPOS` 102) | pendiente (backend + web; POS táctil principalmente en app) |
| N2 | **Inventario** (`INVENTARIO` 110) | pendiente |
| N3 | **Compras/Proveedores + CxP** (`COMPRAS` 111) | pendiente |
| N4 | **NeoBI / Reportes fiscales** (Libro IVA, F-07) (`NEOBI` 105) | pendiente |
| N5 | **NeoPortal** receptor (`NEOPORTAL` 107) | pendiente |
| N6 | ~~NeoScanAI~~ | **Fuera de alcance aquí.** Backend `/api/scanai` (OCR Gemini) ya hecho; la UI vive en la app |
| N7 | **Conciliación bancaria** | pendiente |
| N8 | ~~App Flutter~~ | **Fuera de alcance aquí** — repo `neocloud_mobile_android` |
| N9 | **Multi-moneda / exportación** | pendiente |
| N10 | **NeoConnect avanzado** | pendiente |
| N11 | **Recordatorios automáticos + WhatsApp** | pendiente |
| N12 | **SuperAdmin operativo avanzado** | pendiente |

> **Módulos de consulta (transversal):** endpoints de lectura/reportes (DTE, cobros, profit,
> nómina, inventario) que la app (`dte_query`, `dashboard`) y la web consumen sin lógica nueva.

### 3.3 Módulos nuevos ERP/Interno (códigos libres ≥113)
| Cód | Módulo | Alcance |
|---|---|---|
| **113** | **NEORRHH** (RRHH + Nómina) | Empleados, contratos, planilla **quincenal** ES (ISSS/AFP/Renta 2026), recibos PDF, exportes ISSS/AFP, pago→gasto PLANILLA |
| **114** | **NEOCRM** (CRM-mini) | Contactos, oportunidades/pipeline, actividades+recordatorio (Alertas), Lead→Cotización→DTE→Cobro |
| **115** | **NEOTESORERIA** | Cuentas (banco/caja), movimientos; concilia CxC+CxP+planilla+gastos (habilita N7) |
| **116** | **NEOCONTA** | Asientos básicos, libros IVA compras/ventas, balanza (se solapa con N4 NeoBI fiscal) |

---

## 4. Roadmap unificado (por fases, valor temprano + bajo riesgo)

### Fase A — ERP interno (lo que pediste primero)
1. **NEORRHH base** (113): módulo+permisos seed, `Empleado`/`ContratoLaboral`, `NominaCalculator` puro (ISSS/AFP/Renta 2026 parametrizables) + tests, CRUD empleados `ns-*`.
2. **NEORRHH planilla quincenal**: corrida de período (1-15 / 16-fin), `PlanillaDetalle`, recibo PDF, exportes ISSS/AFP (CsvExporter), **pago→gasto PLANILLA** (alimenta NeoProfit). Cálculo masivo por la cola (M4.4).
3. **NEOTESORERIA mínima** (115): cuentas + movimientos, para registrar pagos de planilla/gastos.

### Fase B — Ciclo de egresos/stock
4. **N3 Compras/CxP** (111): proveedores + órdenes + cuentas por pagar (espejo de Cobros/CxC) + integra NeoScan (DTE recibido→compra).
5. **N2 Inventario** (110): existencias + kardex + costo promedio (mejora costo en NeoProfit) + stock bajo (Alertas).

### Fase C — Comercial
6. **NEOCRM** (114): contactos + pipeline + actividades + cotización→DTE.
7. **N1 NeoPOS** (102) · **N5 NeoPortal** (107).

### Fase D — Cierre fiscal/contable
8. **N4 NeoBI fiscal** (Libro IVA ventas/compras, F-07, retenciones) + **NEOCONTA** (116) asientos/balanza.
9. **N11 Recordatorios** de cobro (correo/WhatsApp) · **N7 Conciliación bancaria**.

### Fase E — Escala/enterprise (ejes M restantes)
10. M4.1/M4.2 (storage externo, Redis) · M6.3/M6.4 (seguridad/cumplimiento) · M3.1/M3.3 (observabilidad/panel SaaS) · M8.2/M8.3 · N9/N10/N12.

> **Nota de alcance:** la app Flutter y NeoScan UI quedan fuera (otro repo). Cada módulo ERP/CRM
> que construyamos aquí debe exponer **API REST** (consumible por la app) además de su UI web.

---

## 5. NEORRHH — detalle de nómina (El Salvador, quincenal, 2026)

**Entidades:** `Empleado`, `ContratoLaboral`, `ConceptoNomina` (devengo/deducción),
`PlanillaPeriodo` (quincena), `PlanillaDetalle` (por empleado), `PagoPlanilla` (egreso).

**`NominaCalculator` (clase pura testeable, tasas/tramos en config `Nomina:2026`):**
- **ISSS**: 3% empleado / 7.5% patronal, tope salarial (config).
- **AFP**: ~7.25% empleado / ~8.75% patronal, tope (config).
- **Renta**: tabla de retención **quincenal/mensual** MH por tramos (parametrizable).
- Aguinaldo, vacación (+30%), horas extra, indemnización: calculadoras puras.
- En quincena: ISSS/AFP/Renta se aplican según política (p.ej. retención en 2ª quincena o prorrateo) → configurable.

**Salidas:** recibo PDF por empleado (QuestPDF), planilla ISSS/AFP (CSV), resumen patronal.
**Integración:** `PagoPlanilla` → gasto categoría **PLANILLA** (ya existe) → P&L correcto sin doble captura. Recordatorios de pago → Alertas.

## 6. NEOCRM — detalle

**Entidades:** `Contacto` (persona de una cuenta/Cliente), `Oportunidad` (etapa, monto,
probabilidad, cierre estimado), `ActividadCrm` (llamada/correo/visita/nota + recordatorio),
`EtapaPipeline` (config). **Flujo:** Lead→Oportunidad→Cotización→**DTE**→Cobro. Embudo +
próximas actividades en dashboard. Recordatorios reusan `IAlertaService`+FCM.

---

## 7. Decisiones
- ✅ Planilla **quincenal** (1-15 / 16-fin).
- ✅ Tablas **ISSS/AFP/Renta 2026** publicadas, **parametrizables** por año.
- Pendiente menor: nº de empleados (define síncrono vs cola; por defecto la cola lo cubre).

## 8. Sugerencia de arranque
**Fase A.1 — NEORRHH Sprint 1:** módulo+permisos, `Empleado`/`ContratoLaboral`,
`NominaCalculator` (ISSS/AFP/Renta 2026) con batería de tests, CRUD web de empleados `ns-*`.
Aislado del resto, alto valor interno inmediato; deja lista la base para la corrida quincenal.
