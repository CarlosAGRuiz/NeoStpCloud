# Análisis integral del sistema — julio 2026

**Fecha:** 2026-07-18 · **Alcance:** todo NeoSTP Cloud (`main` @ `ff624e3`) · **Propósito:** base de decisión antes de preparar los proyectos verticales (farmacias, ferreterías, salones de belleza, tiendas).

---

## 1. Resumen ejecutivo

El sistema está en un estado **sólido y listo para crecer hacia verticales**. La arquitectura por capas es consistente, el aislamiento multi-tenant por empresa + módulos + planes ya es el mecanismo de venta por segmento, y la calidad operativa (tests, CI, seguridad, observabilidad) está por encima de lo típico para un producto de este tamaño.

| Área | Estado | Nota |
|---|---|---|
| Arquitectura | 🟢 | Clean Architecture consistente en 7 proyectos |
| Multi-tenancy / módulos | 🟢 | 17 módulos, 7 planes, permisos granulares |
| Seguridad | 🟡 | Buena base; 1 paquete vulnerable y providers Mock sin guard de producción |
| Tests | 🟡 | 786 unit + 9 integración; 15 servicios sin cobertura directa |
| Deuda técnica | 🟡 | 1 god-service fiscal (1,800 líneas), mojibake en 5 archivos, paquetes atrasados |
| Preparación multi-vertical | 🟡 | Inventario/POS/categorías listos; faltan lotes-vencimientos y agenda |

## 2. Inventario

- **Proyectos:** Domain (93 archivos), Application (159), Infrastructure (179), Web MVC (64 + 149 vistas), Api (49), Worker (10, 8 jobs), Shared — **~55,500 líneas C#** escritas a mano (sin migraciones).
- **Base de datos:** 96 tablas, 66 migraciones, 25 áreas de dominio (DTE, Cobranza, Inventario, POS, Compras, CRM, RRHH, Tesorería, Conta, Profit, Scan, Connect, Billing, Legal, Ops…).
- **Módulos comerciales:** CORE, NEODTE, NEOPOS, NEOSCANAI, NEOPROFIT, NEOBI, NEOCONNECT, NEOPORTAL, CONTINGENCIA, EVENTOSDTE, INVENTARIO, COMPRAS, GASTOS, NEORRHH, NEOCRM, NEOTESORERIA, NEOCONTA.
- **Planes:** Starter $15 → Enterprise $400, más Integrador API y Contador; matriz plan-módulo sembrada.
- **Superficies:** Web MVC (backoffice), API REST con JWT (app móvil NeoCloud), NeoConnect API v1 con API keys/scopes/webhooks (integradores), portal receptor, Worker (8 jobs de fondo).

## 3. Fortalezas confirmadas

1. **Patrón de capas disciplinado**: interfaces en Application, implementación en Infrastructure, controllers delgados con chequeo de permisos uniforme (`Has(...)` / `RequirePermiso`). Cero `TODO/HACK/FIXME` en el código.
2. **Aislamiento por empresa** consistente: todos los servicios operan con `empresaId` explícito; los índices compuestos parten de `EmpresaId`.
3. **Seguridad**: JWT con validación completa, security headers + HSTS en ambas superficies, CORS estricto (deny-by-default en producción), cuotas de API con 429/`X-RateLimit-*` (`ApiQuotaMiddleware`), secretos templados fuera del repo, ZAP baseline en CI, contenedores por servicio.
4. **Operación**: OpenTelemetry + Redis por toggles, auditoría transversal por módulo, backups/limpiezas/retransmisiones como workers, runbooks en `docs/`.
5. **Calidad**: 786 tests unitarios + 9 de integración en CI (build + test en GitHub Actions), carga masiva y catálogos con dry-run.

## 4. Hallazgos y deuda (priorizados)

### Alto
- **A1 — Paquete vulnerable:** `Microsoft.OpenApi 2.0.0` con vulnerabilidad alta conocida (NU1903, GHSA-v5pm-xwqc-g5wc) en Api y Tests. *Acción: subir de versión (chip ya creado).*
- **A2 — God-service fiscal:** [DteDocumentosService.cs](src/NeoSTP.Infrastructure/Services/DteDocumentosService.cs) tiene **1,804 líneas** y concentra borrador, correlativos, validación, firma, transmisión, eventos, PDF y correo. Es el corazón fiscal del negocio y el archivo con más riesgo de regresión. *Acción: partirlo en 3-4 servicios (creación/validación, transmisión, consulta/mapeo) manteniendo la interfaz; la extracción de `AplicarDatosExportacionAsync` de esta semana marca el patrón.*

### Medio
- **M1 — Servicios sin tests directos (15):** EmpresasService, UsuariosService, RolesService, PlanesService, PuntosVentaService, DteConfiguracionService, ReporteFiscalService, DiagnosticoHaciendaService, ContingenciaLoteService, ConnectWebhookService, LegalDocumentService, OperacionPanelService, StorageServices, TabularParser, CobroPdfBuilder. El núcleo administrativo (usuarios/roles/planes) es lo más delicado de esta lista. *Acción: una tanda de tests estilo in-memory como los existentes.*
- **M2 — Providers Mock sin guard de producción:** correo, pagos, push, OCR y firma arrancan en `Provider: "Mock"` por configuración. No hay verificación de arranque que impida que un ambiente productivo quede en Mock silenciosamente. *Acción: guard en `Program.cs` (fail-fast si `Production` && algún provider crítico == Mock, con lista blanca explícita).*
- **M3 — Mojibake (docs/UI):** 5 archivos con acentos doblemente codificados que llegan a mensajes de usuario: DependencyInjection, NeoStpDbContext, CatalogosService, RecordatorioCobroService, CobrosController. *Acción: chip ya creado; ampliado a los 5.*
- **M4 — Paquetes atrasados:** Stripe.net 47→52 (major, revisar breaking changes de billing), ClosedXML, mercadopago-sdk, Scalar, y parches .NET 10.0.8→10.0.10. *Acción: tanda de actualización con suite completa.*
- **M5 — Integraciones reales pendientes** (por diseño, hooks listos): OCR real de NeoScan, FCM push, verificación de NIT contra MH. Son compromisos de producto para la app móvil.

### Bajo
- **B1 — Seeds gigantes:** EmpresaPruebaSeeder (1,587 líneas) y SeedData (829+500). Funcionan, pero cada sprint los engorda; considerar datos de demo en JSON embebido.
- **B2 — Integración con poca cobertura:** solo 9 tests de integración contra SQL real; los flujos DTE end-to-end dependen de pruebas manuales con el runbook.
- **B3 — CI sin métricas de cobertura ni analizadores** (`dotnet format --verify` / analyzers en build) — barato de agregar.

## 5. Preparación multi-vertical

**Lo que ya está y sirve tal cual** (la estrategia "un producto, módulos por cliente" es viable hoy):

| Capacidad | Estado |
|---|---|
| Módulos/planes por empresa | ✅ mecanismo comercial listo |
| Inventario (existencias, kardex, costo promedio, stock mínimo) | ✅ módulo INVENTARIO |
| POS con ticket térmico | ✅ NEOPOS |
| Compras, órdenes y recepciones | ✅ V3 S1-S2 |
| Categorías de producto por empresa | ✅ (mejora 6, jul-2026) |
| Catálogos propios por rubro | ✅ (mejora 4, jul-2026) |
| Carga masiva de clientes/productos | ✅ |
| RRHH/nómina, tesorería, conta básica | ✅ módulos opcionales |

**Brechas por vertical:**

1. **Farmacia** — *la brecha más importante*: lotes y fechas de vencimiento. Hoy `ExistenciaProducto`/`MovimientoInventario` no modelan lote. Se necesita: entidad `LoteProducto` (número, vencimiento, cantidad por lote), consumo FEFO en POS/DTE, alertas de vencimiento (encaja en el `AlertaGeneracionWorker` existente). Opcional segunda fase: medicamentos controlados.
2. **Ferretería**: conversión de unidades (caja/docena/unidad) y precios por volumen o por cliente. Hoy hay una sola unidad y un solo precio por producto.
3. **Salón de belleza**: agenda/citas (módulo nuevo NEOAGENDA: servicios con duración, calendario por empleado, recordatorios reutilizando la infraestructura de notificaciones) y comisiones por empleado (sobre RRHH existente).
4. **Tiendas**: cubierto con POS + inventario + categorías; solo faltaría plantilla de onboarding.
5. **Transversal**: **plantillas de vertical en onboarding** — al crear la empresa, elegir rubro y sembrar categorías típicas + módulos del plan. Barato: se apoya en catálogos custom + onboarding existente.

## 6. Plan recomendado (orden propuesto)

| # | Entrega | Contenido | Tamaño |
|---|---|---|---|
| 1 | Saneamiento | A1 OpenApi, M3 mojibake, M4 paquetes, M2 guard de providers en producción, B3 cobertura en CI | S |
| 2 | Blindaje del núcleo | A2 partir DteDocumentosService + M1 tests de servicios administrativos | M |
| 3 | Inventario avanzado | Lotes + vencimientos + FEFO + alertas (habilita **farmacia**) | M |
| 4 | Plantillas de vertical | Onboarding por rubro con seeds de categorías y módulos (habilita **tiendas** y acelera todas) | S |
| 5 | Unidades y precios | Conversión de unidades + precios por volumen (habilita **ferretería**) | M |
| 6 | NEOAGENDA | Citas/calendario + comisiones (habilita **salón de belleza**) | L |

Los pasos 1-2 protegen lo que ya vende; 3-6 abren mercados en orden de esfuerzo/beneficio. Cada vertical termina siendo **configuración + 1 módulo**, no un fork — la arquitectura actual lo sostiene.
